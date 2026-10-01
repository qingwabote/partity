using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    public struct Speed : IComponentData
    {
        public float Value;
    }

    public struct Direction : IComponentData
    {
        public float3 Value;
    }

    /// Tag: Direction is interpreted in the emit-time emitter frame (see Particle).
    public struct LocalSpace : IComponentData { }

    /// Shuriken Particle.totalVelocity equivalent: the velocity the solver integrates and the
    /// renderer aligns to. Seeded from the lineage (Speed × Direction) by VelocitySystem;
    /// VelocityOverLifetimeSystem layers the module terms on top for module particles.
    public struct Velocity : IComponentData
    {
        public float3 Value;
    }

    public struct StartSpeed : IComponentData
    {
        public MinMaxCurve Curve;
    }

#if UNITY_EDITOR
    public class VelocityAuthoring : MonoBehaviour
    {
        public bool LocalSpace;
        public ParticleSystem.MinMaxCurve StartSpeed = new ParticleSystem.MinMaxCurve(0f);

        class Baker : Baker<VelocityAuthoring>
        {
            public override void Bake(VelocityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Speed
                {
                    Value = authoring.StartSpeed.mode == ParticleSystemCurveMode.Constant
                        ? authoring.StartSpeed.constant
                        : 0f,
                });
                AddComponent(entity, new Direction { Value = new float3(0f, 0f, 1f) });
                if (authoring.LocalSpace)
                    AddComponent(entity, new LocalSpace());
                if (authoring.StartSpeed.mode != ParticleSystemCurveMode.Constant)
                    AddComponent(entity, new StartSpeed { Curve = authoring.StartSpeed });
            }
        }
    }
#endif

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(VelocitySystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct StartSpeedSystem : ISystem
    {
        private Unity.Mathematics.Random m_Random;

        public void OnCreate(ref SystemState state)
        {
            m_Random = new Unity.Mathematics.Random(0x45d9f3bu);
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (speed, start) in SystemAPI.Query<RefRW<Speed>, StartSpeed>().WithAll<Nudge>())
            {
                speed.ValueRW.Value = start.Curve.Evaluate(0f, m_Random.NextFloat());
            }
        }
    }

    /// Lineage → Velocity: the emitter decides Direction, modules decide magnitude — the single
    /// place the two become a velocity. cocos seeds ultimateVelocity from velocity before the
    /// module chain runs (particle-system-renderer-cpu.ts); VelocityOverLifetimeSystem layers
    /// its terms on top of this seed.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LimitVelocityOverLifetimeSystem))]
    [UpdateBefore(typeof(MovementSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct VelocitySystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (velocity, speed, direction) in
                SystemAPI.Query<RefRW<Velocity>, Speed, Direction>())
            {
                velocity.ValueRW.Value = speed.Value * direction.Value;
            }
        }
    }

    /// The sim-space → world boundary. Direction is authored in the emit-time emitter frame
    /// (LocalSpace) and VelocitySystem composes the lineage there; this pass applies the
    /// frozen frame — rotation and scale together — so everything downstream (modules, the
    /// integrator, alignment, collision) reads a world-space Velocity and never adapts.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(VelocitySystem))]
    [UpdateBefore(typeof(VelocityOverLifetimeSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct VelocityWorldSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (velocity, particle) in
                SystemAPI.Query<RefRW<Velocity>, RefRO<Particle>>().WithAll<LocalSpace>())
            {
                velocity.ValueRW.Value = math.rotate(particle.ValueRO.Rotation, velocity.ValueRO.Value) * particle.ValueRO.Scale;
            }
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct MovementSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            // The sole position integrator: modules produce Velocity (frame-exact — a module
            // like VelocityOverLifetime folds analytic motion into it), this applies it.
            foreach (var (transform, velocity) in
                SystemAPI.Query<RefRW<LocalTransform>, RefRO<Velocity>>())
            {
                transform.ValueRW.Position += velocity.ValueRO.Value * dt;
            }
        }
    }

#if UNITY_EDITOR
    /// Pre-adds Velocity wherever the lineage pair (Speed + Direction) is baked, so instances
    /// are born with their final archetype and VelocitySystem only writes values. Bulk query
    /// add, like NudgeBakingSystem.
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct VelocityBakingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var query = SystemAPI.QueryBuilder().WithAll<Speed, Direction>()
                .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)
                .Build();
            state.EntityManager.AddComponent(query, typeof(Velocity));
        }
    }
#endif
}
