using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    /// Shuriken renderer alignment = Velocity (P14's Mesh mode). Simulation never rotates a
    /// particle for its motion; this render-parity pass points local +Z at the total velocity,
    /// recomputed per frame via VelocityOverLifetimeSystem.LinearTotal plus the orbital
    /// tangential term (no stored velocity component). LengthScale stretches the mesh along
    /// that axis and is applied at emit (EmitSystem folds it into the spawn size).
    public struct VelocityAlignment : IComponentData
    {
        public float LengthScale;
    }

#if UNITY_EDITOR
    public class VelocityAlignmentAuthoring : MonoBehaviour
    {
        [Min(0f)] public float LengthScale = 1f;

        class Baker : Baker<VelocityAlignmentAuthoring>
        {
            public override void Bake(VelocityAlignmentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new VelocityAlignment { LengthScale = authoring.LengthScale });
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
            foreach (var (transform, alignment, vol, speed, direction, center, lifetime) in
                SystemAPI.Query<RefRW<LocalTransform>, VelocityAlignment, VelocityOverLifetime, RefRO<Speed>, RefRO<Direction>, RefRO<OrbitCenter>, Lifetime>())
            {
                var t = lifetime.Time / lifetime.Life;
                var lerp = lifetime.Lerp;
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
