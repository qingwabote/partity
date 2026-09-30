using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    /// Shuriken Velocity over Lifetime. The particle's physical lineage velocity stays in
    /// Speed + Direction; this module's linear term is a per-frame accumulator, so the
    /// integrating velocity is (lineage + linear) * SpeedModifier + radial — cocos's
    /// velocity / animatedVelocity / ultimateVelocity split (particle-system-renderer-cpu.ts),
    /// with ultimate kept as a per-frame local instead of a stored component. The component
    /// sits in LocalTransform's write group, so MovementSystem (FilterWriteGroup) skips these
    /// particles and VelocityOverLifetimeSystem owns their integration. Orbital has no cocos
    /// counterpart and rotates position analytically instead.
    [WriteGroup(typeof(LocalTransform))]
    public struct VelocityOverLifetime : IComponentData
    {
        public MinMaxCurve X;
        public MinMaxCurve Y;
        public MinMaxCurve Z;
        public MinMaxCurve OrbitalX;
        public MinMaxCurve OrbitalY;
        public MinMaxCurve OrbitalZ;
        public MinMaxCurve OrbitalOffsetX;
        public MinMaxCurve OrbitalOffsetY;
        public MinMaxCurve OrbitalOffsetZ;
        public MinMaxCurve Radial;
        public MinMaxCurve SpeedModifier;
    }

    /// Orbit center, captured at emit: emitter world position + emitter-rotated OrbitalOffset.
    /// (Shuriken re-centers live as the system moves; capture-at-emit is the accepted divergence.)
    public struct OrbitCenter : IComponentData
    {
        public float3 Value;
    }

#if UNITY_EDITOR
    public class VelocityOverLifetimeAuthoring : MonoBehaviour
    {
        [Header("Linear Velocity")]
        public ParticleSystem.MinMaxCurve X;
        public ParticleSystem.MinMaxCurve Y;
        public ParticleSystem.MinMaxCurve Z;
        [Header("Orbital Velocity (rad/s)")]
        public ParticleSystem.MinMaxCurve OrbitalX;
        public ParticleSystem.MinMaxCurve OrbitalY;
        public ParticleSystem.MinMaxCurve OrbitalZ;
        [Header("Orbital Offset (emitter-local)")]
        public ParticleSystem.MinMaxCurve OrbitalOffsetX;
        public ParticleSystem.MinMaxCurve OrbitalOffsetY;
        public ParticleSystem.MinMaxCurve OrbitalOffsetZ;
        [Header("Radial Speed")]
        public ParticleSystem.MinMaxCurve Radial;
        [Header("Speed Modifier")]
        public ParticleSystem.MinMaxCurve SpeedModifier = new ParticleSystem.MinMaxCurve(1f);

        class Baker : Baker<VelocityOverLifetimeAuthoring>
        {
            public override void Bake(VelocityOverLifetimeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new VelocityOverLifetime
                {
                    X = authoring.X,
                    Y = authoring.Y,
                    Z = authoring.Z,
                    OrbitalX = authoring.OrbitalX,
                    OrbitalY = authoring.OrbitalY,
                    OrbitalZ = authoring.OrbitalZ,
                    OrbitalOffsetX = authoring.OrbitalOffsetX,
                    OrbitalOffsetY = authoring.OrbitalOffsetY,
                    OrbitalOffsetZ = authoring.OrbitalOffsetZ,
                    Radial = authoring.Radial,
                    SpeedModifier = authoring.SpeedModifier,
                });
            }
        }
    }
#endif

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ForceOverLifetimeSystem))]
    [UpdateBefore(typeof(LimitVelocityOverLifetimeSystem))]
    [UpdateBefore(typeof(MovementSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct VelocityOverLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            foreach (var (transform, vol, speed, direction, center, lifetime) in
                SystemAPI.Query<RefRW<LocalTransform>, VelocityOverLifetime, RefRO<Speed>, RefRO<Direction>, RefRO<OrbitCenter>, Lifetime>().WithOptions(EntityQueryOptions.FilterWriteGroup))
            {
                transform.ValueRW.Position += LinearTotal(in vol, speed.ValueRO.Value * direction.ValueRO.Value, transform.ValueRO.Position, center.ValueRO.Value, lifetime.Time / lifetime.Life, lifetime.Lerp) * dt;
            }

            foreach (var (transform, vol, speed, direction, center) in
                SystemAPI.Query<RefRW<LocalTransform>, VelocityOverLifetime, RefRO<Speed>, RefRO<Direction>, RefRO<OrbitCenter>>().WithNone<Lifetime>().WithOptions(EntityQueryOptions.FilterWriteGroup))
            {
                transform.ValueRW.Position += LinearTotal(in vol, speed.ValueRO.Value * direction.ValueRO.Value, transform.ValueRO.Position, center.ValueRO.Value, 0f, 0f) * dt;
            }
        }

        // cocos velocity-overtime.ts animate(): the module's linear term is a per-frame
        // accumulator there (animatedVelocity, reset every update) — here a per-frame local,
        // evaluated for integration and again by VelocityAlignmentSystem through this helper.
        public static float3 LinearTotal(in VelocityOverLifetime vol, float3 lineageVelocity, float3 position, float3 center, float t, float lerp)
        {
            var linear = new float3(vol.X.Evaluate(t, lerp), vol.Y.Evaluate(t, lerp), vol.Z.Evaluate(t, lerp));
            var ultimate = (lineageVelocity + linear) * vol.SpeedModifier.Evaluate(t, lerp);
            var radial = vol.Radial.Evaluate(t, lerp);
            if (radial != 0f)
            {
                ultimate += radial * math.normalizesafe(position - center, float3.zero);
            }
            return ultimate;
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct OrbitalSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            foreach (var (transform, vol, center, lifetime) in
                SystemAPI.Query<RefRW<LocalTransform>, VelocityOverLifetime, RefRO<OrbitCenter>, Lifetime>())
            {
                Rotate(ref transform.ValueRW, in vol, center.ValueRO.Value, lifetime.Time / lifetime.Life, lifetime.Lerp, dt);
            }

            foreach (var (transform, vol, center) in
                SystemAPI.Query<RefRW<LocalTransform>, VelocityOverLifetime, RefRO<OrbitCenter>>().WithNone<Lifetime>())
            {
                Rotate(ref transform.ValueRW, in vol, center.ValueRO.Value, 0f, 0f, dt);
            }
        }

        // Shuriken's orbital integrates analytically (measured: radius exact over a full orbit);
        // Euler-stepping an omega-cross-r term instead drifts ~16%/orbit at 60 fps.
        static void Rotate(ref LocalTransform transform, in VelocityOverLifetime vol, float3 center, float t, float lerp, float dt)
        {
            var omega = new float3(vol.OrbitalX.Evaluate(t, lerp), vol.OrbitalY.Evaluate(t, lerp), vol.OrbitalZ.Evaluate(t, lerp));
            var magnitude = math.length(omega);
            if (magnitude == 0f)
                return;
            var rotation = quaternion.AxisAngle(omega / magnitude, magnitude * dt);
            transform.Position = center + math.rotate(rotation, transform.Position - center);
        }
    }
}
