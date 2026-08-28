using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Partity
{
    public struct Emission : IBufferElementData
    {
        public float3 Position;
        public float3 Direction;
    }

    [UpdateInGroup(typeof(ShapeSystemGroup), OrderLast = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct EmitSystem : ISystem
    {
        private Unity.Mathematics.Random rng;

        public void OnCreate(ref SystemState state)
        {
            rng = new Unity.Mathematics.Random(456789123);
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var em = state.EntityManager;

            foreach (var (emitter, world, buffer) in
                SystemAPI.Query<Emitter, LocalToWorld, DynamicBuffer<Emission>>())
            {
                if (buffer.Length == 0) continue;

                var offset = em.GetComponentData<LocalTransform>(emitter.ParticlePrefab);
                var hasDirection = em.HasComponent<Direction>(emitter.ParticlePrefab);
                var hasSpaceScale = em.HasComponent<SpaceScale>(emitter.ParticlePrefab);
                var emitterScale = world.Value.Scale().x;

                foreach (var emission in buffer)
                {
                    var p = ecb.Instantiate(emitter.ParticlePrefab);
                    var rotation = math.mul(math.mul(quaternion.LookRotationSafe(emission.Direction, math.up()), offset.Rotation),
                        quaternion.EulerZXY(rng.NextFloat3(emitter.Rotation.Min, emitter.Rotation.Max)));
                    ecb.SetComponent(p, new LocalTransform
                    {
                        Position = emission.Position + math.rotate(rotation, offset.Position),
                        Rotation = rotation,
                        Scale = emitterScale * offset.Scale * emitter.Size
                    });
                    if (hasDirection)
                    {
                        ecb.SetComponent(p, new Direction { Value = emission.Direction });
                    }
                    if (hasSpaceScale)
                    {
                        ecb.SetComponent(p, new SpaceScale { Value = emitterScale });
                    }
                }

                buffer.Clear();
            }
        }
    }
}
