using Unity.Entities;

namespace Partity
{
    public struct Nudge : IComponentData, IEnableableComponent
    {
    }

    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct NudgeCleanupSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (_, enabled) in SystemAPI.Query<RefRW<Nudge>, EnabledRefRW<Nudge>>())
            {
                enabled.ValueRW = false;
            }
        }
    }
}
