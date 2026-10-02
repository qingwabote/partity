using Unity.Collections;
using Unity.Entities;

namespace Partity
{
    /// <summary>
    /// The dieWithParticles=false semantics for lifetime deaths. Death is a two-phase protocol
    /// (LifetimeStep disables the component, LifetimeEndSystem destroys later in the frame), so
    /// <c>WithDisabled&lt;Lifetime&gt;</c> inside that window is the only queryable "dying"
    /// state — this system must live between the two. Placing it after TrailRecordSystem
    /// copies the freshest recorded state into the linger entity.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrailRecordSystem))]
    [UpdateBefore(typeof(LifetimeEndSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TrailAutoDetachSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dying = SystemAPI.QueryBuilder()
                .WithAll<TrailRenderer, TrailCleanup>()
                .WithDisabled<Lifetime>()
                .Build()
                .ToEntityArray(Allocator.Temp);

            var em = state.EntityManager;
            foreach (var entity in dying)
            {
                if (em.GetComponentData<TrailRenderer>(entity).DieWithParticles)
                    continue;
                TrailRenderer.Detach(em, entity);
            }
            dying.Dispose();
        }
    }
}
