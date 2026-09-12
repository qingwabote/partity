using Unity.Entities;

namespace Partity
{
    public struct StartLifetime : IComponentData
    {
        public MinMaxCurve Curve;
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
}
