using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace Partity
{
    public struct ColorOverLifetime : IComponentData
    {
        public MinMaxGradient Gradient;
        public float4 Base;
    }

#if UNITY_EDITOR
    [RequireComponent(typeof(ColorAuthoring))]
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
                    Gradient = authoring.Color.ToPartity(1.0f)
                });
            }
        }
    }
#endif

    [UpdateAfter(typeof(StartColorSystem)), UpdateBefore(typeof(ColorOverLifetimeSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ColorBaseSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (color, property) in SystemAPI.Query<RefRW<ColorOverLifetime>, RefRO<URPMaterialPropertyBaseColor>>().WithAll<Nudge>())
            {
                color.ValueRW.Base = property.ValueRO.Value;
            }
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LifetimeStepSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ColorOverLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (baseColor, lifetime, color) in SystemAPI.Query<RefRW<URPMaterialPropertyBaseColor>, Lifetime, ColorOverLifetime>())
            {
                baseColor.ValueRW.Value = color.Gradient.Evaluate(lifetime.Time / lifetime.Life, lifetime.Lerp) * color.Base;
            }
        }
    }
}
