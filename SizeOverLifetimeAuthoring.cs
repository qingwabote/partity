using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    public struct SizeOverLifetime : IComponentData
    {
        public BlobAssetReference<MinMaxCurveBlob> Curve;
        public float Evaluate(float t, float lerpFactor) => Curve.Value.Evaluate(t, lerpFactor);
    }

    public struct SizeRaw : IComponentData
    {
        public float Value;
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
    public partial struct SizeRawSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<SizeOverLifetime>().WithNone<SizeRaw>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new SizeRaw { Value = transform.ValueRO.Scale });
            }
            ecb.Playback(state.EntityManager);
        }
    }

    [RequireMatchingQueriesForUpdate]
    public partial struct SizeOverLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (lifetime, size, raw, factor, transform) in
                SystemAPI.Query<Lifetime, SizeOverLifetime, SizeRaw, AnimationFactor, RefRW<LocalTransform>>())
            {
                transform.ValueRW.Scale = size.Evaluate(lifetime.Time / lifetime.Life, factor.Value) * raw.Value;
            }
        }
    }
}
