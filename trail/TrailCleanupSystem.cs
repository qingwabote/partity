using Unity.Collections;
using Unity.Entities;

namespace Partity
{
    /// <summary>
    /// Destruction strips every non-cleanup component but keeps this one — the shell exists
    /// only so the Slot can reach the pool; removing it lets the shell die (dieWithParticles:
    /// the ribbon vanishes with its head, same frame).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrailRecordSystem))]
    public partial struct TrailCleanupSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var registry = state.World.GetExistingSystemManaged<TrailRenderSystem>().Registry;

            var shells = SystemAPI.QueryBuilder()
                .WithAll<TrailCleanup>()
                .WithNone<TrailRenderer>()
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
