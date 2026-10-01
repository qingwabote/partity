using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    /// Shuriken renderer alignment = Velocity (P14's Mesh mode). Simulation never rotates a
    /// particle for its motion; this render-parity pass points local +Z at the total velocity,
    /// recomputed per frame via VelocityOverLifetimeSystem.LinearTotal plus the orbital
    /// tangential term (no stored velocity component). Stretch along that axis, when needed,
    /// comes from the prefab's calibrated PostTransformMatrix — not from here.
    public struct VelocityAlignment : IComponentData
    {
    }

#if UNITY_EDITOR
    public class VelocityAlignmentAuthoring : MonoBehaviour
    {
        class Baker : Baker<VelocityAlignmentAuthoring>
        {
            public override void Bake(VelocityAlignmentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new VelocityAlignment());
            }
        }
    }
#endif

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OrbitalSystem))]
    [UpdateBefore(typeof(RotationOverLifetimeSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct VelocityAlignmentSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (transform, vol, speed, direction, center, particle, lifetime) in
                SystemAPI.Query<RefRW<LocalTransform>, VelocityOverLifetime, RefRO<Speed>, RefRO<Direction>, RefRO<OrbitCenter>, RefRO<Particle>, Lifetime>().WithAll<VelocityAlignment>())
            {
                var t = lifetime.Time / lifetime.Life;
                var lerp = particle.ValueRO.Lerp;
                var omega = new float3(vol.OrbitalX.Evaluate(t, lerp), vol.OrbitalY.Evaluate(t, lerp), vol.OrbitalZ.Evaluate(t, lerp));
                var total = VelocityOverLifetimeSystem.LinearTotal(in vol, speed.ValueRO.Value * direction.ValueRO.Value, transform.ValueRO.Position, center.ValueRO.Value, t, lerp)
                    + math.cross(omega, transform.ValueRO.Position - center.ValueRO.Value);
                Align(ref transform.ValueRW, total);
            }

            foreach (var (transform, alignment, speed, direction) in
                SystemAPI.Query<RefRW<LocalTransform>, VelocityAlignment, RefRO<Speed>, RefRO<Direction>>().WithNone<VelocityOverLifetime>())
            {
                Align(ref transform.ValueRW, speed.ValueRO.Value * direction.ValueRO.Value);
            }
        }

        // RotationOverLifetime's spin post-multiplies on top of the aligned base; its accumulated
        // angle does not survive this overwrite, so alignment + spin combined needs accumulated
        // spin state — no current asset combines them.
        static void Align(ref LocalTransform transform, float3 velocity)
        {
            var forward = math.normalizesafe(velocity, float3.zero);
            if (math.all(forward == 0f))
                return; // zero velocity: keep the current orientation
            var up = math.abs(forward.y) > 0.999f ? new float3(0f, 0f, 1f) : new float3(0f, 1f, 0f);
            var right = math.normalizesafe(math.cross(up, forward), new float3(1f, 0f, 0f));
            transform.Rotation = new quaternion(new float3x3(right, math.cross(forward, right), forward));
        }
    }
}
