using Bastard;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace Partity
{
    public struct StartColor : IComponentData
    {
        public MinMaxGradient Gradient;
    }

#if UNITY_EDITOR
    public class ColorAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxGradient Color = new ParticleSystem.MinMaxGradient(UnityEngine.Color.white);

        public float Intensity = 1.0f;

        class Baker : Baker<ColorAuthoring>
        {
            public override void Bake(ColorAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Renderable);
                var baseColor = authoring.Color.mode == ParticleSystemGradientMode.Color ? authoring.Color.color.ToFloat4() : new float4(1.0f, 1.0f, 1.0f, 1.0f);
                AddComponent(entity, new URPMaterialPropertyBaseColor
                {
                    Value = new float4(baseColor.xyz * authoring.Intensity, baseColor.w),
                });
                if (authoring.Color.mode != ParticleSystemGradientMode.Color)
                {
                    AddComponent(entity, new StartColor { Gradient = authoring.Color.ToPartity(authoring.Intensity) });
                }
            }
        }
    }
#endif

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LifetimeLerpSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct StartColorSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (baseColor, start, lifetime) in
                SystemAPI.Query<RefRW<URPMaterialPropertyBaseColor>, StartColor, Lifetime>().WithAll<Nudge>())
            {
                baseColor.ValueRW.Value = start.Gradient.Evaluate(0f, lifetime.Lerp);
            }
        }
    }
}
