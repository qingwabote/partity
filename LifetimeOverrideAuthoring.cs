using Unity.Entities;
using UnityEngine;

namespace Partity
{
    public struct LifetimeOverride : IComponentData
    {
        public BlobAssetReference<MinMaxCurveBlob> Curve;
    }

    public class LifetimeOverrideAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve Curve = new ParticleSystem.MinMaxCurve(1f);

        class Baker : Baker<LifetimeOverrideAuthoring>
        {
            public override void Bake(LifetimeOverrideAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new LifetimeOverride
                {
                    Curve = authoring.Curve.ToBlob()
                });
            }
        }
    }
}
