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

                var hasLifetimeOverride = em.HasComponent<LifetimeOverride>(entity);
                var lifetimeOverride = hasLifetimeOverride ? em.GetComponentData<LifetimeOverride>(entity) : default;

                var hasPtm = em.HasComponent<PostTransformMatrix>(emitter.ParticlePrefab);
                var prefabPtm = hasPtm ? em.GetComponentData<PostTransformMatrix>(emitter.ParticlePrefab).Value : float4x4.identity;

                var hasDirection = em.HasComponent<Direction>(emitter.ParticlePrefab);
                var hasLocalSpace = em.HasComponent<LocalSpace>(emitter.ParticlePrefab);

                var offset = em.GetComponentData<LocalTransform>(emitter.ParticlePrefab);

                var emitterScale = math.length(world.Value.c0.xyz);
                var emitterRotation = world.Value.Rotation();

                foreach (var emission in buffer)
                {
                    var p = ecb.Instantiate(emitter.ParticlePrefab);

                    var scale = emitterScale * offset.Scale;
                    var size = math.lerp(emitter.StartSize.Min, emitter.StartSize.Max, rng.NextFloat());
                    if (size.x == size.y && size.y == size.z)
                    {
                        scale *= size.x;
                    }
                    else
                    {
                        var ptm = math.mul(prefabPtm, float4x4.Scale(size));
                        if (hasPtm)
                            ecb.SetComponent(p, new PostTransformMatrix { Value = ptm });
                        else
                            ecb.AddComponent(p, new PostTransformMatrix { Value = ptm });
                    }

                    var rotation = math.mul(emitterRotation, emission.Rotation);
                    ecb.SetComponent(p, new LocalTransform
                    {
                        Position = math.transform(world.Value, emission.Position) + math.rotate(rotation, offset.Position),
                        Rotation = math.mul(math.mul(rotation, offset.Rotation), quaternion.EulerZXY(rng.NextFloat3(emitter.StartRotation.Min, emitter.StartRotation.Max))),
                        Scale = scale
                    });

                    if (hasDirection)
                    {
                        ecb.SetComponent(p, new Direction
                        {
                            Value = hasLocalSpace
                                ? math.rotate(emission.Rotation, new float3(0f, 0f, 1f))
                                : math.rotate(rotation, new float3(0f, 0f, 1f))
                        });
                        if (hasLocalSpace)
                        {
                            ecb.SetComponent(p, new LocalSpace { Rotation = emitterRotation, Scale = emitterScale });
                        }
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
