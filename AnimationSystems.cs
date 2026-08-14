using Unity.Entities;

namespace Partity
{
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct ProgressAnimationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (lifetime, animation, factor, progress) in SystemAPI.Query<RefRO<Lifetime>, ProgressAnimation, RefRO<AnimationFactor>, RefRW<MaterialPropertyProgress>>())
            {
                progress.ValueRW.Value = animation.Curve.Value.Evaluate(lifetime.ValueRO.Time / lifetime.ValueRO.Life, factor.ValueRO.Value);
            }
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct ThresholdAnimationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (lifetime, animation, factor, property) in SystemAPI.Query<Lifetime, ThresholdAnimation, RefRO<AnimationFactor>, RefRW<MaterialPropertyThreshold>>())
            {
                property.ValueRW.Value = animation.Curve.Value.Evaluate(lifetime.Time / lifetime.Life, factor.ValueRO.Value);
            }
        }
    }
}
