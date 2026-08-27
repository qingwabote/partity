using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    public struct ForceOverLifetime : IComponentData
    {
        public BlobAssetReference<MinMaxCurveBlob> X;
        public BlobAssetReference<MinMaxCurveBlob> Y;
        public BlobAssetReference<MinMaxCurveBlob> Z;
    }

#if UNITY_EDITOR
    public class ForceOverLifetimeAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve X;
        public ParticleSystem.MinMaxCurve Y;
        public ParticleSystem.MinMaxCurve Z;

        class Baker : Baker<ForceOverLifetimeAuthoring>
        {
            public override void Bake(ForceOverLifetimeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new ForceOverLifetime
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
    [UpdateBefore(typeof(MovementSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ForceOverLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            foreach (var (force, lifetime, speed, direction) in
                SystemAPI.Query<ForceOverLifetime, Lifetime, RefRW<Speed>, RefRW<Direction>>())
            {
                var dir = direction.ValueRO.Value;
                var spd = speed.ValueRO.Value;

                var t = lifetime.Time / lifetime.Life;
                var f = new float3(
                    force.X.Value.Evaluate(t, lifetime.Lerp),
                    force.Y.Value.Evaluate(t, lifetime.Lerp),
                    force.Z.Value.Evaluate(t, lifetime.Lerp));
                var v = dir * spd + f * dt;

                speed.ValueRW.Value = math.length(v);
                direction.ValueRW.Value = math.normalizesafe(v, dir);
            }
        }
    }
}
