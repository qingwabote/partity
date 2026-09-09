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

            foreach (var (buffer, emitter, world, entity) in
                SystemAPI.Query<DynamicBuffer<Emission>, Emitter, LocalToWorld>().WithNone<Paused>().WithEntityAccess())
            {
                if (buffer.Length == 0) continue;

                var offset = em.GetComponentData<LocalTransform>(emitter.ParticlePrefab);
                var hasDirection = em.HasComponent<Direction>(emitter.ParticlePrefab);
                var hasSpaceScale = em.HasComponent<SpaceScale>(emitter.ParticlePrefab);
                var hasLifetimeOverride = em.HasComponent<LifetimeOverride>(entity);
                var lifetimeOverride = hasLifetimeOverride ? em.GetComponentData<LifetimeOverride>(entity) : default;
                var emitterScale = world.Value.Scale().x;
                var emitterRotation = world.Value.Rotation();
                var uniform = emitter.StartSize.x == emitter.StartSize.y && emitter.StartSize.y == emitter.StartSize.z;
                var scale = emitterScale * offset.Scale * (uniform ? emitter.StartSize.x : 1f);
                var prefabPtm = uniform ? float4x4.identity
                    : em.GetComponentData<PostTransformMatrix>(emitter.ParticlePrefab).Value;

                foreach (var emission in buffer)
                {
                    var p = ecb.Instantiate(emitter.ParticlePrefab);
                    var rotation = math.mul(emitterRotation, emission.Rotation);
                    ecb.SetComponent(p, new LocalTransform
                    {
                        Position = math.transform(world.Value, emission.Position) + math.rotate(rotation, offset.Position),
                        Rotation = math.mul(math.mul(rotation, offset.Rotation), quaternion.EulerZXY(rng.NextFloat3(emitter.StartRotation.Min, emitter.StartRotation.Max))),
                        Scale = scale
                    });
                    if (!uniform)
                    {
                        ecb.SetComponent(p, new PostTransformMatrix
                        {
                            Value = math.mul(prefabPtm, float4x4.Scale(emitter.StartSize))
                        });
                    }
                    if (hasDirection)
                    {
                        ecb.SetComponent(p, new Direction { Value = math.rotate(rotation, new float3(0f, 0f, 1f)) });
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
