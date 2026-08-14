using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace Partity
{
    public struct ProgressAnimation : IComponentData
    {
        public BlobAssetReference<MinMaxCurveBlob> Curve;
    }

    [MaterialProperty("_Progress")]
    public struct MaterialPropertyProgress : IComponentData
    {
        public float Value;
    }

#if UNITY_EDITOR
    public class ProgressAnimationAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve Curve;

        class Baker : Baker<ProgressAnimationAuthoring>
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
    }
#endif
}
