using Unity.Collections;
using Unity.Entities;

namespace Partity
{
    /// <summary>
    /// Cleanup side of the cleanup-marker state machine: when a trail-carrying particle dies,
    /// entity destruction strips every non-cleanup component — the ribbon vanishes that same
    /// frame (dieWithParticles) — leaving a shell whose TrailCleanup carries the Slot. This
    /// system returns it to the pool and lets the shell die. Slot accounting is exact; shell
    /// disposal trails particle death by at most one frame.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrailRecordSystem))]
    public partial struct TrailCleanupSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // per-world registry (multi-world), owned by the render system; producers write into it
            var registry = state.World.GetExistingSystemManaged<TrailRenderSystem>().Registry;

            var shells = SystemAPI.QueryBuilder()
                .WithAll<TrailCleanup>()
                .WithNone<TrailParams>()
                .Build()
                .ToEntityArray(Allocator.Temp);
            foreach (var shell in shells)
            {
                registry.ReleaseSlot(SystemAPI.GetComponent<TrailCleanup>(shell).Slot);
                state.EntityManager.RemoveComponent<TrailCleanup>(shell);
            }
            shells.Dispose();
        }
    }
}
