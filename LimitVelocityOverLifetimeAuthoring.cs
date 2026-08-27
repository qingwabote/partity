using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Partity
{
    public struct LimitVelocityOverLifetime : IComponentData
    {
        public BlobAssetReference<MinMaxCurveBlob> Limit;
        public float Dampen;
    }

#if UNITY_EDITOR
    public class LimitVelocityOverLifetimeAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve Limit = new ParticleSystem.MinMaxCurve(1f);
        [Range(0f, 1f)] public float Dampen = 0.025f;

        class Baker : Baker<LimitVelocityOverLifetimeAuthoring>
        {
            public override void Bake(LimitVelocityOverLifetimeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new LimitVelocityOverLifetime
                {
                    Limit = authoring.Limit.ToBlob(),
                    Dampen = authoring.Dampen,
                });
            }
        }
    }
#endif

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MovementSystem))]
    [UpdateAfter(typeof(ForceOverLifetimeSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct LimitVelocityOverLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (limitVelocity, lifetime, speed) in
                SystemAPI.Query<LimitVelocityOverLifetime, Lifetime, RefRW<Speed>>())
            {
                var t = lifetime.Time / lifetime.Life;
                var limit = limitVelocity.Limit.Value.Evaluate(t, lifetime.Lerp);
                speed.ValueRW.Value = DampenBeyondLimit(speed.ValueRO.Value, limit, limitVelocity.Dampen);
            }
        }

        static float DampenBeyondLimit(float vel, float limit, float dampen)
        {
            var sgn = math.sign(vel);
            var abs = math.abs(vel);
            if (abs > limit)
            {
                var absToGive = abs - abs * dampen;
                abs = absToGive > limit ? absToGive : limit;
            }
            return abs * sgn;
        }
    }
}
