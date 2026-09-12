using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace Partity
{
    public struct StartColor : IComponentData
    {
        public MinMaxGradient Gradient;
    }

#if UNITY_EDITOR
    public class StartColorAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxGradient Color = new ParticleSystem.MinMaxGradient(UnityEngine.Color.white);

        class Baker : Baker<StartColorAuthoring>
        {
            public override void Bake(StartColorAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponent(entity, new StartColor { Gradient = authoring.Color });
                AddComponent<URPMaterialPropertyBaseColor>(entity);
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
