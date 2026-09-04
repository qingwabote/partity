using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace Partity
{
    public struct ThresholdOverLifetime : IComponentData
    {
        public BlobAssetReference<MinMaxCurveBlob> Curve;
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
                    Curve = authoring.Curve.ToBlob()
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
            foreach (var (lifetime, animation, property) in SystemAPI.Query<Lifetime, ThresholdOverLifetime, RefRW<MaterialPropertyThreshold>>())
            {
                property.ValueRW.Value = animation.Curve.Value.Evaluate(lifetime.Time / lifetime.Life, lifetime.Lerp);
            }
        }
    }
}
