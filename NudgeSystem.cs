using Unity.Entities;

namespace Partity
{
    public struct Nudge : IComponentData
    {
    }

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
