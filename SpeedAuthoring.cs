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

    public struct LocalSpace : IComponentData
    {
        public quaternion Rotation;
        public float Scale;
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
                    AddComponent(entity, new LocalSpace { Rotation = quaternion.identity, Scale = 1f });
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

            foreach (var (transform, speed, direction, localSpace) in
                SystemAPI.Query<RefRW<LocalTransform>, Speed, Direction, LocalSpace>())
            {
                transform.ValueRW.Position += math.rotate(localSpace.Rotation, speed.Value * localSpace.Scale * dt * direction.Value);
            }

            foreach (var (transform, speed, direction) in
                SystemAPI.Query<RefRW<LocalTransform>, Speed, Direction>().WithNone<LocalSpace>())
            {
                transform.ValueRW.Position += speed.Value * dt * direction.Value;
            }
        }
    }
}
