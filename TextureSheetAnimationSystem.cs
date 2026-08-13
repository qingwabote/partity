using Unity.Entities;
using Unity.Rendering;

namespace Partity
{
    public struct Lifetime : IComponentData
    {
        public float Life;
        public float Time;
        public float Factor;
    }

    class LifetimeBaker : Baker<LifetimeAuthoring>
    {
        public override void Bake(LifetimeAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Renderable);
            AddComponent(entity, new Lifetime
            {
                Life = authoring.Life
            });
        }
    }

    public struct ProgressAnimation : IComponentData
    {
        public BlobAssetReference<MinMaxCurveBlob> Curve;
    }

    [MaterialProperty("_Progress")]
    public struct MaterialPropertyProgress : IComponentData
    {
        public float Value;
    }

    class TextureSheetAnimationBaker : Baker<ProgressAnimationAuthoring>
    {
        public override void Bake(ProgressAnimationAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Renderable);
            AddComponent(entity, new ProgressAnimation
            {
                Curve = MinMaxCurveBaker.Bake(authoring.Curve)
            });
            AddComponent<MaterialPropertyProgress>(entity);
        }
    }

    [RequireMatchingQueriesForUpdate]
    public partial struct TextureSheetAnimationLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (lifetime, animation, progress) in SystemAPI.Query<RefRO<Lifetime>, ProgressAnimation, RefRW<MaterialPropertyProgress>>())
            {
                progress.ValueRW.Value = animation.Curve.Value.Evaluate(lifetime.ValueRO.Time / lifetime.ValueRO.Life, lifetime.ValueRO.Factor);
            }
        }
    }

    [MaterialProperty("_Threshold")]
    public struct MaterialPropertyThreshold : IComponentData
    {
        public float Value;
    }

    public struct ThresholdAnimation : IComponentData
    {
        public BlobAssetReference<MinMaxCurveBlob> Curve;
    }

    class ThresholdAnimationBaker : Baker<ThresholdAnimationAuthoring>
    {
        public override void Bake(ThresholdAnimationAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Renderable);
            AddComponent(entity, new ThresholdAnimation
            {
                Curve = MinMaxCurveBaker.Bake(authoring.Curve)
            });
            AddComponent<MaterialPropertyThreshold>(entity);
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
                property.ValueRW.Value = animation.Curve.Value.Evaluate(lifetime.Time / lifetime.Life, lifetime.Factor);
            }
        }
    }
}
