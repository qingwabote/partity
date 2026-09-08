using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Partity
{
    public struct StartLifetime : IComponentData
    {
        public BlobAssetReference<MinMaxCurveBlob> Curve;
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
                lifetime.ValueRW.Life = start.Curve.Value.Evaluate(0f, lifetime.ValueRO.Lerp);
            }
        }
    }
}
