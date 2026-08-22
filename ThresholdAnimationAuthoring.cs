using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace Partity
{
    public struct ThresholdAnimation : IComponentData
    {
        public BlobAssetReference<MinMaxCurveBlob> Curve;
    }

    [MaterialProperty("_Threshold")]
    public struct MaterialPropertyThreshold : IComponentData
    {
        public float Value;
    }

#if UNITY_EDITOR
    public class ThresholdAnimationAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve Curve;

        class Baker : Baker<ThresholdAnimationAuthoring>
        {
            public override void Bake(ThresholdAnimationAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponent(entity, new ThresholdAnimation
                {
                    Curve = authoring.Curve.ToBlob()
                });
                AddComponent<MaterialPropertyThreshold>(entity);
            }
        }
    }
#endif
}
