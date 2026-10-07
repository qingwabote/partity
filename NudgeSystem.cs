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
        private EntityQuery m_Query;

        public void OnCreate(ref SystemState state)
        {
            m_Query = state.GetEntityQuery(new EntityQueryDesc
            {
                Any = new ComponentType[]
                {
                    ComponentType.ReadOnly<StartSpeed>(),
                    ComponentType.ReadOnly<StartLifetime>(),
                    ComponentType.ReadOnly<StartColor>(),
                    ComponentType.ReadOnly<SizeOverLifetime>(),
                    ComponentType.ReadOnly<ColorOverLifetime>(),
                    ComponentType.ReadOnly<VelocityOverLifetime>(),
                    ComponentType.ReadOnly<LimitVelocityOverLifetime>(),
                    ComponentType.ReadOnly<ForceOverLifetime>(),
                    ComponentType.ReadOnly<RotationOverLifetime>(),
                    ComponentType.ReadOnly<ThresholdOverLifetime>(),
                    ComponentType.ReadOnly<ProgressOverLifetime>(),
                },
                Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.AddComponent(m_Query, typeof(Nudge));
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
