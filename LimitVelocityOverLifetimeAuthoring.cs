using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Partity
{
    public struct LimitVelocityOverLifetime : IComponentData
    {
        public BlobAssetReference<MinMaxCurveBlob> Limit;
        public float InterpSpeed;
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
                    // Shuriken scales the excess velocity by (1-dampen) per 1/30 s sub-step;
                    // -30*ln(1-dampen) is the equivalent per-second interp speed
                    InterpSpeed = -30f * math.log(math.max(1f - authoring.Dampen, 1e-30f)),
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
            var deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (limitVelocity, lifetime, speed) in
                SystemAPI.Query<LimitVelocityOverLifetime, Lifetime, RefRW<Speed>>())
            {
                var t = lifetime.Time / lifetime.Life;
                var limit = limitVelocity.Limit.Value.Evaluate(t, lifetime.Lerp);

                var current = speed.ValueRO.Value;
                // Target: the speed clamped to the limit; FInterpTo leaves it unchanged while within the limit
                var target = math.clamp(current, -limit, limit);
                speed.ValueRW.Value = FInterpTo(current, target, deltaTime, limitVelocity.InterpSpeed);
            }
        }

        // Port of UE FMath::FInterpTo
        // (Engine/Source/Runtime/Core/Public/Math/UnrealMathUtility.h)
        // Interpolate from Current to Target. Scaled by distance to Target, so it has a strong start speed and ease out.
        static float FInterpTo(float Current, float Target, float DeltaTime, float InterpSpeed)
        {
            // If no interp speed, keep Current (UE returns Target; Shuriken dampen=0 is a no-op)
            if (InterpSpeed <= 0f)
            {
                return Current;
            }

            // Distance to reach
            var Dist = Target - Current;

            // If distance is too small, just set the desired location
            if (Dist * Dist < 1e-8f)
            {
                return Target;
            }

            // Delta Move, Clamp so we do not over shoot.
            var DeltaMove = Dist * math.clamp(DeltaTime * InterpSpeed, 0f, 1f);

            return Current + DeltaMove;
        }
    }
}
