using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace Partity
{
    public struct ColorOverLifetime : IComponentData
    {
        public MinMaxGradient Gradient;
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
                    Gradient = authoring.Color
                });
                AddComponent<URPMaterialPropertyBaseColor>(entity);
            }
        }
    }
#endif

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LifetimeSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ColorOverLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (baseColor, lifetime, color) in
                SystemAPI.Query<RefRW<URPMaterialPropertyBaseColor>, Lifetime, ColorOverLifetime>().WithNone<Paused>())
            {
                baseColor.ValueRW.Value = color.Gradient.Evaluate(lifetime.Time / lifetime.Life, lifetime.Lerp);
            }
        }
    }
}
