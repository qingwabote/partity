using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    [WriteGroup(typeof(Emitter))]
    public struct ShapeLine : IComponentData
    {
        public quaternion Rotation;
    }

    public class ShapeLineAuthoring : MonoBehaviour
    {
        public Vector3 Rotation;

        class Baker : Baker<ShapeLineAuthoring>
        {
            public override void Bake(ShapeLineAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new ShapeLine
                {
                    Rotation = Quaternion.Euler(authoring.Rotation)
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

            foreach (var (emitterRef, world, line) in
                SystemAPI.Query<RefRW<Emitter>, LocalToWorld, ShapeLine>())
            {
                var emitter = emitterRef.ValueRO;
                if (emitter.Payload <= 0) continue;

                var emitterPosition = world.Value.Translation();
                var emitterRotation = math.mul(world.Value.Rotation(), line.Rotation);
                var emitterScale = world.Value.Scale().x;

                var particlePrefab = emitter.ParticlePrefab;
                var offset = em.GetComponentData<LocalTransform>(particlePrefab);
                var particleRotation = math.mul(emitterRotation, offset.Rotation);
                var particlePosition = emitterPosition + math.rotate(emitterRotation, offset.Position);
                var particleScale = offset.Scale * emitterScale * emitter.Size;

                var transform = LocalTransform.FromPositionRotationScale(particlePosition, particleRotation, particleScale);
                var direction = new Direction { Value = math.rotate(emitterRotation, new float3(0f, 0f, 1f)) };

                var setDirection = em.HasComponent<Direction>(particlePrefab);
                for (int i = 0; i < emitter.Payload; i++)
                {
                    var p = ecb.Instantiate(particlePrefab);
                    ecb.SetComponent(p, transform);
                    if (setDirection)
                    {
                        ecb.SetComponent(p, direction);
                    }
                }

                emitterRef.ValueRW.Payload = 0;
            }
        }
    }
}
