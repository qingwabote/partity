using Unity.Collections;
using Unity.Entities;

namespace Partity
{
    /// <summary>
    /// Linger shell terminus: destroy once the history has fully unwound (fewer than two
    /// points = no renderable polyline, matching the render system's cull). The resulting
    /// TrailCleanup shell releases the slot through the existing TrailCleanupSystem, which
    /// runs right after this system — no new slot bookkeeping.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrailRecordSystem))]
    [UpdateBefore(typeof(TrailCleanupSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TrailLingerEndSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var shells = SystemAPI.QueryBuilder()
                .WithAll<TrailLinger>()
                .Build()
                .ToEntityArray(Allocator.Temp);

            var em = state.EntityManager;
            foreach (var shell in shells)
            {
                if (em.GetComponentData<TrailData>(shell).LiveCount < 2f)
                    em.DestroyEntity(shell);
            }
            shells.Dispose();
        }
    }
}
