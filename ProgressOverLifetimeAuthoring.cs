using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace Partity
{
    public struct ProgressOverLifetime : IComponentData
    {
        public BlobAssetReference<MinMaxCurveBlob> Curve;
    }

    [MaterialProperty("_Progress")]
    public struct MaterialPropertyProgress : IComponentData
    {
        public float Value;
    }

#if UNITY_EDITOR
    public class ProgressOverLifetimeAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve Curve;

        class Baker : Baker<ProgressOverLifetimeAuthoring>
        {
            public override void Bake(ProgressOverLifetimeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponent(entity, new ProgressOverLifetime
                {
                    Curve = authoring.Curve.ToBlob()
                });
                AddComponent<MaterialPropertyProgress>(entity);
            }
        }
    }
#endif

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LifetimeSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ProgressOverLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (lifetime, animation, progress) in SystemAPI.Query<RefRO<Lifetime>, ProgressOverLifetime, RefRW<MaterialPropertyProgress>>())
            {
                progress.ValueRW.Value = animation.Curve.Value.Evaluate(lifetime.ValueRO.Time / lifetime.ValueRO.Life, lifetime.ValueRO.Lerp);
            }
        }
    }
}
