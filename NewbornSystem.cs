using Unity.Entities;

namespace Partity
{
    public struct Newborn : IComponentData
    {
        public Entity Emitter;
        public int EmissionIndex;
    }

#if UNITY_EDITOR
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct NewbornBakingSystem : ISystem
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
            state.EntityManager.AddComponent(m_Query, typeof(Newborn));
        }
    }
#endif

    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct NewbornCleanupSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.RemoveComponent(SystemAPI.QueryBuilder().WithAll<Newborn>().Build(), typeof(Newborn));
        }
    }
}
