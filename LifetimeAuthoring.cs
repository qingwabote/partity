using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Partity
{
    /// <summary>
    /// Per-entity age and expiry. ENABLED = alive and ticking (Time accumulates toward Life).
    /// DISABLED = destruction requested (expiry, or an explicit kill); the entity is destroyed by
    /// <see cref="LifetimeEndSystem"/>, unless another destroy authority claims it via a write group on
    /// this component (write group filtering is presence-based, so a claimant component excludes the
    /// entity regardless of the enabled state).
    /// </summary>
    public struct Lifetime : IComponentData, IEnableableComponent
    {
        public float Life;
        public float Time;
        /// <summary>
        /// Random min/max curve blend factor, fixed at birth and constant across frames.
        /// </summary>
        public float Lerp;
    }

    public struct StartLifetime : IComponentData
    {
        public MinMaxCurve Curve;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Authoring for a bare <see cref="Lifetime"/>: the entity tracks its own age and is destroyed when it
    /// expires. A Constant <see cref="StartLifetime"/> bakes directly into <see cref="Lifetime.Life"/>; any
    /// other mode bakes a <see cref="StartLifetime"/> curve that <see cref="StartLifetimeSystem"/> evaluates
    /// at birth. In the Constant case nothing overwrites Life at runtime, so a value set per spawn (e.g. per
    /// weapon level) survives.
    /// </summary>
    public class LifetimeAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve StartLifetime = new ParticleSystem.MinMaxCurve(1f);

        class Baker : Baker<LifetimeAuthoring>
        {
            public override void Bake(LifetimeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponent(entity, new Lifetime
                {
                    Life = authoring.StartLifetime.mode == ParticleSystemCurveMode.Constant ? authoring.StartLifetime.constant : 0f,
                });
                if (authoring.StartLifetime.mode != ParticleSystemCurveMode.Constant)
                    AddComponent(entity, new StartLifetime { Curve = authoring.StartLifetime });
            }
        }
    }
#endif

    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct LifetimeLerpSystem : ISystem
    {
        private Unity.Mathematics.Random m_Random;

        public void OnCreate(ref SystemState state)
        {
            m_Random = new Unity.Mathematics.Random(0x9E3779B1u);
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var lifetime in SystemAPI.Query<RefRW<Lifetime>>().WithAll<Nudge>())
            {
                lifetime.ValueRW.Lerp = m_Random.NextFloat();
            }
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LifetimeLerpSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct StartLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (lifetime, start) in SystemAPI.Query<RefRW<Lifetime>, StartLifetime>().WithAll<Nudge>())
            {
                lifetime.ValueRW.Life = start.Curve.Evaluate(0f, lifetime.ValueRO.Lerp);
            }
        }
    }

    /// <summary>
    /// Ticks <see cref="Lifetime.Time"/> toward <see cref="Lifetime.Life"/> for alive (enabled)
    /// entities and marks expiry by disabling the component. Destruction itself is
    /// <see cref="LifetimeEndSystem"/>'s job, so claimed entities can route to their own destroy path.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(StartLifetimeSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct LifetimeStepSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            foreach (var (lifetime, enabled) in SystemAPI.Query<RefRW<Lifetime>, EnabledRefRW<Lifetime>>())
            {
                var lt = lifetime.ValueRO;
                lt.Time = math.min(lt.Time + dt, lt.Life);
                lifetime.ValueRW.Time = lt.Time;

                if (lt.Time >= lt.Life)
                {
                    enabled.ValueRW = false;
                }
            }
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct LifetimeEndSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            // Destroy per entity rather than via a query capture: a query destroy requires every
            // entity of a destroyed entity's LinkedEntityGroup to be included in the query itself.
            ecb.DestroyEntity(SystemAPI.QueryBuilder().WithDisabledRW<Lifetime>().WithOptions(EntityQueryOptions.FilterWriteGroup).Build().ToEntityArray(Allocator.Temp));
        }
    }
}
