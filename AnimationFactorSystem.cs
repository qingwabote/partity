using Unity.Entities;

namespace Partity
{
    public struct AnimationFactor : IComponentData, IEnableableComponent
    {
        public float Value;
    }

    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct AnimationFactorSystem : ISystem
    {
        private Unity.Mathematics.Random m_Random;

        public void OnCreate(ref SystemState state)
        {
            m_Random = new Unity.Mathematics.Random(0x9E3779B1u);
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (factor, enabled) in SystemAPI.Query<RefRW<AnimationFactor>, EnabledRefRW<AnimationFactor>>().WithDisabled<AnimationFactor>())
            {
                factor.ValueRW.Value = m_Random.NextFloat();
                enabled.ValueRW = true;
            }
        }
    }
}
