using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    public struct SizeOverLifetime : IComponentData
    {
        public BlobAssetReference<MinMaxCurveBlob> Curve;
    }

#if UNITY_EDITOR
    public class SizeOverLifetimeAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve Size = new ParticleSystem.MinMaxCurve(1f);

        class Baker : Baker<SizeOverLifetimeAuthoring>
        {
            public override void Bake(SizeOverLifetimeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new SizeOverLifetime
                {
                    Curve = MinMaxCurveBaker.Bake(authoring.Size)
                });
            }
        }
    }
#endif

    [RequireMatchingQueriesForUpdate]
    public partial struct SizeOverLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (lifetime, size, transform) in SystemAPI.Query<RefRO<Lifetime>, SizeOverLifetime, RefRW<LocalTransform>>())
            {
                transform.ValueRW.Scale = size.Curve.Value.Evaluate(lifetime.ValueRO.Time / lifetime.ValueRO.Life);
            }
        }
    }
}
