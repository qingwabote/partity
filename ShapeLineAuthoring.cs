using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    public struct ShapeLine : IComponentData
    {
        public float Scale;
        public bool Rotate;
    }

    public class ShapeLineAuthoring : MonoBehaviour
    {
        public float Scale = 1f;
        public bool Rotate = true;

        class Baker : Baker<ShapeLineAuthoring>
        {
            public override void Bake(ShapeLineAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new ShapeLine
                {
                    Scale = authoring.Scale,
                    Rotate = authoring.Rotate
                });
            }
        }
    }

    [UpdateInGroup(typeof(ShapeSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ShapeLineSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var em = state.EntityManager;

            foreach (var (emitter, payload, world, line) in
                SystemAPI.Query<Emitter, RefRW<EmitterPayload>, LocalToWorld, ShapeLine>())
            {
                int count = payload.ValueRO.Value;
                if (count <= 0) continue;

                var emitterPosition = world.Position;
                var emitterRotation = line.Rotate ? world.Rotation : quaternion.identity;

                var offset = em.GetComponentData<LocalTransform>(emitter.ParticlePrefab);
                var particleRotation = math.mul(emitterRotation, offset.Rotation);
                var particlePosition = emitterPosition + math.rotate(emitterRotation, offset.Position);
                var particleScale = offset.Scale * line.Scale;

                var transform = LocalTransform.FromPositionRotationScale(particlePosition, particleRotation, particleScale);
                var direction = new Direction { Value = math.rotate(particleRotation, new float3(0f, 0f, 1f)) };

                var setDirection = em.HasComponent<Direction>(emitter.ParticlePrefab);
                for (int j = 0; j < count; j++)
                {
                    var p = ecb.Instantiate(emitter.ParticlePrefab);
                    ecb.SetComponent(p, transform);
                    if (setDirection)
                    {
                        ecb.SetComponent(p, direction);
                    }
                }

                payload.ValueRW.Value = 0;
            }
        }
    }
}
