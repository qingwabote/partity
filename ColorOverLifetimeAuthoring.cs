using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace Partity
{
    public struct ColorOverLifetime : IComponentData
    {
        public BlobAssetReference<MinMaxGradientBlob> Gradient;
    }

#if UNITY_EDITOR
    public class ColorOverLifetimeAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxGradient Color = new ParticleSystem.MinMaxGradient(UnityEngine.Color.white);

        class Baker : Baker<ColorOverLifetimeAuthoring>
        {
            public override void Bake(ColorOverLifetimeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponent(entity, new ColorOverLifetime
                {
                    Gradient = authoring.Color.ToBlob()
                });
                AddComponent<URPMaterialPropertyBaseColor>(entity);
            }
        }
    }
#endif

    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct ColorOverLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (lifetime, color, baseColor) in
                SystemAPI.Query<RefRO<Lifetime>, ColorOverLifetime, RefRW<URPMaterialPropertyBaseColor>>())
            {
                baseColor.ValueRW.Value = color.Gradient.Value.Evaluate(lifetime.ValueRO.Time / lifetime.ValueRO.Life, lifetime.ValueRO.Lerp);
            }
        }
    }
}
