using Unity.Entities;

namespace Partity
{
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct ProgressAnimationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (lifetime, animation, progress) in SystemAPI.Query<RefRO<Lifetime>, ProgressAnimation, RefRW<MaterialPropertyProgress>>())
            {
                progress.ValueRW.Value = animation.Curve.Value.Evaluate(lifetime.ValueRO.Time / lifetime.ValueRO.Life, lifetime.ValueRO.Lerp);
            }
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct ThresholdAnimationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (lifetime, animation, property) in SystemAPI.Query<Lifetime, ThresholdAnimation, RefRW<MaterialPropertyThreshold>>())
            {
                property.ValueRW.Value = animation.Curve.Value.Evaluate(lifetime.Time / lifetime.Life, lifetime.Lerp);
            }
        }
    }
}
