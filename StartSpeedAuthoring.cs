using Unity.Entities;
using UnityEngine;

namespace Partity
{
    public struct StartSpeed : IComponentData
    {
        public MinMaxCurve Curve;
    }

#if UNITY_EDITOR
    [RequireComponent(typeof(SpeedAuthoring))]
    public class StartSpeedAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve Speed = new ParticleSystem.MinMaxCurve(1f);

        class Baker : Baker<StartSpeedAuthoring>
        {
            public override void Bake(StartSpeedAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new StartSpeed { Curve = authoring.Speed });
            }
        }
    }
#endif

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MovementSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct StartSpeedSystem : ISystem
    {
        private Unity.Mathematics.Random m_Random;

        public void OnCreate(ref SystemState state)
        {
            m_Random = new Unity.Mathematics.Random(0x45d9f3bu);
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (speed, start) in SystemAPI.Query<RefRW<Speed>, StartSpeed>().WithAll<Nudge>())
            {
                speed.ValueRW.Value = start.Curve.Evaluate(0f, m_Random.NextFloat());
            }
        }
    }
}
