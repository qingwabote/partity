using Unity.Entities;

namespace Partity
{
    public struct Nudge : IComponentData
    {
    }

#if UNITY_EDITOR
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct NudgeBakingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var query = SystemAPI.QueryBuilder().WithAny<StartSpeed, StartLifetime, StartColor, SizeOverLifetime>()
                .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)
                .Build();
            state.EntityManager.AddComponent(query, typeof(Nudge));
        }
    }
#endif

    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct NudgeCleanupSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.RemoveComponent(SystemAPI.QueryBuilder().WithAll<Nudge>().Build(), typeof(Nudge));
        }
    }
}
