using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    /// Shuriken Velocity over Lifetime. The particle's physical lineage velocity stays in
    /// Speed + Direction; this module's linear term is a per-frame accumulator — cocos's
    /// velocity / animatedVelocity / ultimateVelocity split (particle-system-renderer-cpu.ts),
    /// with ultimate kept as a per-frame local. The module writes Velocity only, never
    /// LocalTransform: MovementSystem is the sole position integrator, and the analytic
    /// orbital rotation reaches it folded into the frame-exact velocity (see Step).
    /// Center is per-instance runtime state: filled on the birth frame by OrbitCenterSystem
    /// (Nudge-gated) from the emit-time Particle frame — the prefab copy stays default, each
    /// instance's copy is its own. Shuriken re-centers live as the system moves;
    /// capture-at-emit is the accepted divergence.
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
        public float3 Center;
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
    [UpdateBefore(typeof(ForceOverLifetimeSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct OrbitCenterSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // Spawn frame only (Nudge): Center is baked as a default, so this pass is a pure
            // data write — no structural change, no archetype migration. Lerp comes from
            // Particle, so Lifetime is not needed here at all.
            foreach (var (vol, particle) in
                SystemAPI.Query<RefRW<VelocityOverLifetime>, RefRO<Particle>>().WithAll<Nudge>())
            {
                var offset = new float3(
                    vol.ValueRO.OrbitalOffsetX.Evaluate(0f, particle.ValueRO.Lerp),
                    vol.ValueRO.OrbitalOffsetY.Evaluate(0f, particle.ValueRO.Lerp),
                    vol.ValueRO.OrbitalOffsetZ.Evaluate(0f, particle.ValueRO.Lerp));
                vol.ValueRW.Center = particle.ValueRO.Position + math.rotate(particle.ValueRO.Rotation, offset);
            }
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(VelocitySystem))]
    [UpdateBefore(typeof(MovementSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct VelocityOverLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0f)
                return; // the emitted velocity is a frame delta; a zero step has none

            foreach (var (transform, velocity, vol, particle, lifetime) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRW<Velocity>, VelocityOverLifetime, RefRO<Particle>, Lifetime>())
            {
                velocity.ValueRW = Step(in vol, velocity.ValueRO.Value, transform.ValueRO.Position, particle.ValueRO.Lerp, lifetime.Time / lifetime.Life, dt);
            }

            foreach (var (transform, velocity, vol) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRW<Velocity>, VelocityOverLifetime>().WithNone<Lifetime>())
            {
                velocity.ValueRW = Step(in vol, velocity.ValueRO.Value, transform.ValueRO.Position, 0f, 0f, dt);
            }
        }

        // Velocity arrives seeded with the lineage (VelocitySystem); layer the module terms on
        // top — cocos's ultimate = (velocity + animated) * speedModifier, and the module never
        // touches position: MovementSystem integrates everyone. Orbital stays exact by folding
        // the analytic rotation into the frame delta — Velocity is the constant velocity whose
        // Euler step reproduces this frame's exact motion (Shuriken-measured: radius exact; an
        // Euler-stepped omega-cross-r term instead drifts ~16%/orbit at 60 fps). Semantically
        // this is a frame average: the tangent at the arc's midpoint, within omega*dt/2 of the
        // end-of-frame tangent.
        static Velocity Step(in VelocityOverLifetime vol, float3 seed, float3 position, float lerp, float t, float dt)
        {
            var linear = new float3(vol.X.Evaluate(t, lerp), vol.Y.Evaluate(t, lerp), vol.Z.Evaluate(t, lerp));
            var total = (seed + linear) * vol.SpeedModifier.Evaluate(t, lerp);
            var omega = new float3(vol.OrbitalX.Evaluate(t, lerp), vol.OrbitalY.Evaluate(t, lerp), vol.OrbitalZ.Evaluate(t, lerp));
            var magnitude = math.length(omega);

            var rotated = position;
            if (magnitude != 0f)
            {
                var rotation = quaternion.AxisAngle(omega / magnitude, magnitude * dt);
                rotated = vol.Center + math.rotate(rotation, position - vol.Center);
            }
            var radial = vol.Radial.Evaluate(t, lerp);
            if (radial != 0f)
            {
                total += radial * math.normalizesafe(rotated - vol.Center, float3.zero);
            }

            return new Velocity { Value = (rotated + total * dt - position) / dt };
        }
    }
}
