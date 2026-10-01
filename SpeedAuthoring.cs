using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    public struct Speed : IComponentData
    {
        public float Value;
    }

    public struct Direction : IComponentData
    {
        public float3 Value;
    }

    /// Tag: Direction is interpreted in the emit-time emitter frame (see Particle).
    public struct LocalSpace : IComponentData
    {
    }

    public struct StartSpeed : IComponentData
    {
        public MinMaxCurve Curve;
    }

#if UNITY_EDITOR
    public class SpeedAuthoring : MonoBehaviour
    {
        public bool LocalSpace;
        public ParticleSystem.MinMaxCurve StartSpeed = new ParticleSystem.MinMaxCurve(0f);

        class Baker : Baker<SpeedAuthoring>
        {
            public override void Bake(SpeedAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Speed
                {
                    Value = authoring.StartSpeed.mode == ParticleSystemCurveMode.Constant
                        ? authoring.StartSpeed.constant
                        : 0f,
                });
                AddComponent(entity, new Direction { Value = new float3(0f, 0f, 1f) });
                if (authoring.LocalSpace)
                    AddComponent(entity, new LocalSpace());
                if (authoring.StartSpeed.mode != ParticleSystemCurveMode.Constant)
                    AddComponent(entity, new StartSpeed { Curve = authoring.StartSpeed });
            }
        }
    }
#endif

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MovementSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct StartSpeedSystem : ISystem
    {
        private Unity.Mathematics.Random m_Random;

        public void OnCreate(ref SystemState state)
        {
            m_Random = new Unity.Mathematics.Random(0x45d9f3bu);
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (speed, start) in SystemAPI.Query<RefRW<Speed>, StartSpeed>().WithAll<Nudge>())
            {
                speed.ValueRW.Value = start.Curve.Evaluate(0f, m_Random.NextFloat());
            }
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct MovementSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            // FilterWriteGroup: motion modules in LocalTransform's write group
            // (e.g. VelocityOverLifetime) integrate their own particles.
            foreach (var (transform, speed, direction, particle) in
                SystemAPI.Query<RefRW<LocalTransform>, Speed, Direction, RefRO<Particle>>().WithAll<LocalSpace>().WithOptions(EntityQueryOptions.FilterWriteGroup))
            {
                transform.ValueRW.Position += math.rotate(particle.ValueRO.Rotation, speed.Value * particle.ValueRO.Scale * dt * direction.Value);
            }

            foreach (var (transform, speed, direction) in
                SystemAPI.Query<RefRW<LocalTransform>, Speed, Direction>().WithNone<LocalSpace>().WithOptions(EntityQueryOptions.FilterWriteGroup))
            {
                transform.ValueRW.Position += speed.Value * dt * direction.Value;
            }
        }
    }
}
