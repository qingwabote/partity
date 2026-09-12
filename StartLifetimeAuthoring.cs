using Unity.Entities;
using UnityEngine;

namespace Partity
{
    public struct StartLifetime : IComponentData
    {
        public MinMaxCurve Curve;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Authoring for the start lifetime module: bakes a <see cref="StartLifetime"/> curve that is
    /// evaluated at birth (via <see cref="StartLifetimeSystem"/>) into the entity's <see cref="Lifetime"/>.
    /// The <see cref="Lifetime"/> state itself is baked by the required <see cref="LifetimeAuthoring"/>.
    /// </summary>
    [RequireComponent(typeof(LifetimeAuthoring))]
    public class StartLifetimeAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve Life = new ParticleSystem.MinMaxCurve(1f);

        class Baker : Baker<StartLifetimeAuthoring>
        {
            public override void Bake(StartLifetimeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponent(entity, new StartLifetime
                {
                    Curve = authoring.Life
                });
            }
        }
    }
#endif

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
}
