using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    public struct RotationOverLifetime : IComponentData
    {
        public BlobAssetReference<MinMaxCurveBlob> X;
        public BlobAssetReference<MinMaxCurveBlob> Y;
        public BlobAssetReference<MinMaxCurveBlob> Z;
    }

#if UNITY_EDITOR
    public class RotationOverLifetimeAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve X;
        public ParticleSystem.MinMaxCurve Y;
        public ParticleSystem.MinMaxCurve Z;

        class Baker : Baker<RotationOverLifetimeAuthoring>
        {
            public override void Bake(RotationOverLifetimeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new RotationOverLifetime
                {
                    X = authoring.X.ToBlob(),
                    Y = authoring.Y.ToBlob(),
                    Z = authoring.Z.ToBlob(),
                });
            }
        }
    }
#endif

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LifetimeSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct RotationOverLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            foreach (var (transform, rotation, lifetime) in
                SystemAPI.Query<RefRW<LocalTransform>, RotationOverLifetime, Lifetime>().WithNone<Paused>())
            {
                var t = lifetime.Time / lifetime.Life;
                var r = new float3(
                    rotation.X.Value.Evaluate(t, lifetime.Lerp),
                    rotation.Y.Value.Evaluate(t, lifetime.Lerp),
                    rotation.Z.Value.Evaluate(t, lifetime.Lerp)) * dt;
                transform.ValueRW.Rotation = math.mul(transform.ValueRW.Rotation, quaternion.EulerZXY(r));
            }

            foreach (var (transform, rotation) in
                SystemAPI.Query<RefRW<LocalTransform>, RotationOverLifetime>().WithNone<Paused, Lifetime>())
            {
                var r = new float3(
                    rotation.X.Value.Evaluate(0f, 0f),
                    rotation.Y.Value.Evaluate(0f, 0f),
                    rotation.Z.Value.Evaluate(0f, 0f)) * dt;
                transform.ValueRW.Rotation = math.mul(transform.ValueRW.Rotation, quaternion.EulerZXY(r));
            }
        }
    }
}
