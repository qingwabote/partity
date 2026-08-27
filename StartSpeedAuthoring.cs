using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Partity
{
    public struct StartSpeed : IComponentData
    {
        public BlobAssetReference<MinMaxCurveBlob> Curve;
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LifetimeLerpSystem))]
    [UpdateBefore(typeof(MovementSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct StartSpeedSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (start, speed, lifetime) in
                SystemAPI.Query<StartSpeed, RefRW<Speed>, Lifetime>()
                    .WithAll<Nudge>())
            {
                speed.ValueRW.Value = start.Curve.Value.Evaluate(0f, lifetime.Lerp);
            }
        }
    }
}
