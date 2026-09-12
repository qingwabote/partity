using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    public struct RotationOverLifetime : IComponentData
    {
        public MinMaxCurve X;
        public MinMaxCurve Y;
        public MinMaxCurve Z;
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
                    X = authoring.X,
                    Y = authoring.Y,
                    Z = authoring.Z,
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
                    rotation.X.Evaluate(t, lifetime.Lerp),
                    rotation.Y.Evaluate(t, lifetime.Lerp),
                    rotation.Z.Evaluate(t, lifetime.Lerp)) * dt;
                transform.ValueRW.Rotation = math.mul(transform.ValueRW.Rotation, quaternion.EulerZXY(r));
            }

            foreach (var (transform, rotation) in
                SystemAPI.Query<RefRW<LocalTransform>, RotationOverLifetime>().WithNone<Paused, Lifetime>())
            {
                var r = new float3(
                    rotation.X.Evaluate(0f, 0f),
                    rotation.Y.Evaluate(0f, 0f),
                    rotation.Z.Evaluate(0f, 0f)) * dt;
                transform.ValueRW.Rotation = math.mul(transform.ValueRW.Rotation, quaternion.EulerZXY(r));
            }
        }
    }
}
