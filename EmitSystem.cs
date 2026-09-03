using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Partity
{
    public struct Emission : IBufferElementData
    {
        public float3 Position;
        public quaternion Rotation;
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

            foreach (var (emitter, world, buffer, entity) in
                SystemAPI.Query<Emitter, LocalToWorld, DynamicBuffer<Emission>>().WithEntityAccess())
            {
                if (buffer.Length == 0) continue;

                var offset = em.GetComponentData<LocalTransform>(emitter.ParticlePrefab);
                var hasDirection = em.HasComponent<Direction>(emitter.ParticlePrefab);
                var hasSpaceScale = em.HasComponent<SpaceScale>(emitter.ParticlePrefab);
                var hasLifetimeOverride = em.HasComponent<LifetimeOverride>(entity);
                var lifetimeOverride = hasLifetimeOverride ? em.GetComponentData<LifetimeOverride>(entity) : default;
                var emitterScale = world.Value.Scale().x;
                var uniform = emitter.Size.x == emitter.Size.y && emitter.Size.y == emitter.Size.z;
                var scale = emitterScale * offset.Scale * (uniform ? emitter.Size.x : 1f);
                var prefabPtm = uniform ? float4x4.identity
                    : em.GetComponentData<PostTransformMatrix>(emitter.ParticlePrefab).Value;

                foreach (var emission in buffer)
                {
                    var p = ecb.Instantiate(emitter.ParticlePrefab);
                    var rotation = math.mul(math.mul(emission.Rotation, offset.Rotation),
                        quaternion.EulerZXY(rng.NextFloat3(emitter.Rotation.Min, emitter.Rotation.Max)));
                    ecb.SetComponent(p, new LocalTransform
                    {
                        Position = emission.Position + math.rotate(rotation, offset.Position),
                        Rotation = rotation,
                        Scale = scale
                    });
                    if (!uniform)
                    {
                        ecb.SetComponent(p, new PostTransformMatrix
                        {
                            Value = math.mul(prefabPtm, float4x4.Scale(emitter.Size))
                        });
                    }
                    if (hasDirection)
                    {
                        ecb.SetComponent(p, new Direction { Value = math.rotate(emission.Rotation, new float3(0f, 0f, 1f)) });
                    }
                    if (hasSpaceScale)
                    {
                        ecb.SetComponent(p, new SpaceScale { Value = emitterScale });
                    }
                    if (hasLifetimeOverride)
                    {
                        ecb.SetComponent(p, new StartLifetime { Curve = lifetimeOverride.Curve });
                    }
                }

                buffer.Clear();
            }
        }
    }
}
