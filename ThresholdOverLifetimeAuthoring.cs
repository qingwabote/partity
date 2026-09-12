using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace Partity
{
    public struct ThresholdOverLifetime : IComponentData
    {
        public MinMaxCurve Curve;
    }

    [MaterialProperty("_Threshold")]
    public struct MaterialPropertyThreshold : IComponentData
    {
        public float Value;
    }

#if UNITY_EDITOR
    public class ThresholdOverLifetimeAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve Curve;

        class Baker : Baker<ThresholdOverLifetimeAuthoring>
        {
            public override void Bake(ThresholdOverLifetimeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponent(entity, new ThresholdOverLifetime
                {
                    Curve = authoring.Curve
                });
                AddComponent<MaterialPropertyThreshold>(entity);
            }
        }
    }
#endif

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LifetimeSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ThresholdOverLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (threshold, lifetime, animation) in SystemAPI.Query<RefRW<MaterialPropertyThreshold>, Lifetime, ThresholdOverLifetime>().WithNone<Paused>())
            {
                threshold.ValueRW.Value = animation.Curve.Evaluate(lifetime.Time / lifetime.Life, lifetime.Lerp);
            }
        }
    }
}
