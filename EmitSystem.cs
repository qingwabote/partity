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

    /// The particle's main component: everything fixed at birth. Carries the emitter's
    /// transform frozen at emit — the frame the particle was born in; LocalSpace motion
    /// interprets Direction in it, VelocityOverLifetime captures its orbit center from it —
    /// plus Lerp, the per-particle random that MinMax curves blend with. Never updated after emit.
    public struct Particle : IComponentData
    {
        public float3 Position;
        public quaternion Rotation;
        public float Scale;
        public float Lerp;
    }

    [UpdateInGroup(typeof(ShapeSystemGroup), OrderLast = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct EmitSystem : ISystem
    {
        private Random m_Random;

        public void OnCreate(ref SystemState state)
        {
            m_Random = new Random(456789123);
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var em = state.EntityManager;

            foreach (var (buffer, emitter, world, entity) in
                SystemAPI.Query<DynamicBuffer<Emission>, Emitter, LocalToWorld>().WithEntityAccess())
            {
                if (buffer.Length == 0) continue;

                var hasStartLifetimeOverride = em.HasComponent<LifetimeOverride>(entity);
                var startLifetimeOverride = hasStartLifetimeOverride ? em.GetComponentData<LifetimeOverride>(entity) : default;

                var hasPtm = em.HasComponent<PostTransformMatrix>(emitter.ParticlePrefab);
                var prefabPtm = hasPtm ? em.GetComponentData<PostTransformMatrix>(emitter.ParticlePrefab).Value : float4x4.identity;

                var hasDirection = em.HasComponent<Direction>(emitter.ParticlePrefab);
                var hasLocalSpace = em.HasComponent<LocalSpace>(emitter.ParticlePrefab);

                var offset = em.GetComponentData<LocalTransform>(emitter.ParticlePrefab);

                var emitterScale = math.length(world.Value.c0.xyz);
                var emitterRotation = world.Value.Rotation();

                var particleBase = new Particle { Position = world.Value.c3.xyz, Rotation = emitterRotation, Scale = emitterScale };

                foreach (var emission in buffer)
                {
                    var p = ecb.Instantiate(emitter.ParticlePrefab);

                    var scale = emitterScale * offset.Scale;
                    var size = math.lerp(emitter.StartSize.Min, emitter.StartSize.Max, m_Random.NextFloat());
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
                        Rotation = math.mul(math.mul(rotation, offset.Rotation), quaternion.EulerZXY(m_Random.NextFloat3(emitter.StartRotation.Min, emitter.StartRotation.Max))),
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
                    }
                    if (hasStartLifetimeOverride)
                    {
                        if (em.HasComponent<StartLifetime>(emitter.ParticlePrefab))
                        {
                            ecb.SetComponent(p, new StartLifetime { Curve = startLifetimeOverride.Curve });
                        }
                        else
                        {
                            ecb.AddComponent(p, new StartLifetime { Curve = startLifetimeOverride.Curve });
                        }
                    }
                    // ParticleBakingSystem guarantees the component on every emitter's prefab;
                    // a missing one means a stale bake and fails loudly here.
                    particleBase.Lerp = m_Random.NextFloat();
                    ecb.SetComponent(p, particleBase);
                }

                buffer.Clear();
            }
        }
    }

#if UNITY_EDITOR
    /// Marks every prefab referenced by an Emitter as a particle, so instances are born with
    /// their final archetype and EmitSystem only writes values (SetComponent) at spawn.
    /// Same mechanism as NudgeBakingSystem: components added to prefab entities persist in
    /// the bake; AddComponent on an already-marked prefab is a no-op.
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct ParticleBakingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var prefabs = new Unity.Collections.NativeList<Entity>(Unity.Collections.Allocator.Temp);
            foreach (var emitter in SystemAPI.Query<RefRO<Emitter>>()
                         .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
            {
                if (emitter.ValueRO.ParticlePrefab != Entity.Null)
                    prefabs.Add(emitter.ValueRO.ParticlePrefab);
            }
            state.EntityManager.AddComponent<Particle>(prefabs.AsArray());
        }
    }
#endif
}
