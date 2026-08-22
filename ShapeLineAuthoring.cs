using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    public struct ShapeLine : IComponentData
    {
        public bool Scale;
        public bool Rotate;
    }

    public class ShapeLineAuthoring : MonoBehaviour
    {
        public bool Scale = true;
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

            foreach (var (emitter, world, line) in
                SystemAPI.Query<RefRW<Emitter>, LocalToWorld, ShapeLine>())
            {
                int count = emitter.ValueRO.Payload;
                if (count <= 0) continue;

                var emitterPosition = world.Value.Translation();
                var emitterRotation = line.Rotate ? world.Value.Rotation() : quaternion.identity;
                var emitterScale = line.Scale ? world.Value.Scale().x : 1;

                var particlePrefab = emitter.ValueRO.ParticlePrefab;
                var offset = em.GetComponentData<LocalTransform>(particlePrefab);
                var particleRotation = math.mul(emitterRotation, offset.Rotation);
                var particlePosition = emitterPosition + math.rotate(emitterRotation, offset.Position);
                var particleScale = offset.Scale * emitterScale * emitter.ValueRO.Size;

                var transform = LocalTransform.FromPositionRotationScale(particlePosition, particleRotation, particleScale);
                var direction = new Direction { Value = math.rotate(particleRotation, new float3(0f, 0f, 1f)) };

                var setDirection = em.HasComponent<Direction>(particlePrefab);
                for (int j = 0; j < count; j++)
                {
                    var p = ecb.Instantiate(particlePrefab);
                    ecb.SetComponent(p, transform);
                    if (setDirection)
                    {
                        ecb.SetComponent(p, direction);
                    }
                }

                emitter.ValueRW.Payload = 0;
            }
        }
    }
}
