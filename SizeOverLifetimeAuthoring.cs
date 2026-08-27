using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    public struct SizeOverLifetime : IComponentData
    {
        public BlobAssetReference<MinMaxCurveBlob> Curve;
        public float Base;
        public float Evaluate(float t, float lerpFactor) => Curve.Value.Evaluate(t, lerpFactor);
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
                    Curve = authoring.Size.ToBlob()
                });
            }
        }
    }
#endif

    [UpdateBefore(typeof(SizeOverLifetimeSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SizeBaseSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (size, transform) in SystemAPI.Query<RefRW<SizeOverLifetime>, RefRO<LocalTransform>>()
                         .WithAll<Nudge>())
            {
                size.ValueRW.Base = transform.ValueRO.Scale;
            }
        }
    }

    [RequireMatchingQueriesForUpdate]
    public partial struct SizeOverLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (lifetime, size, transform) in
                SystemAPI.Query<Lifetime, SizeOverLifetime, RefRW<LocalTransform>>())
            {
                transform.ValueRW.Scale = size.Evaluate(lifetime.Time / lifetime.Life, lifetime.Lerp) * size.Base;
            }
        }
    }
}
