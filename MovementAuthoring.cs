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

#if UNITY_EDITOR
    public class MovementAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve Speed = new ParticleSystem.MinMaxCurve(1f);

        class Baker : Baker<MovementAuthoring>
        {
            public override void Bake(MovementAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                if (authoring.Speed.mode == ParticleSystemCurveMode.Constant)
                {
                    AddComponent(entity, new Speed { Value = authoring.Speed.constant });
                }
                else
                {
                    AddComponent(entity, new Speed { Value = 0f });
                    AddComponent(entity, new StartSpeed { Curve = authoring.Speed.ToBlob() });
                }
                AddComponent(entity, new Direction { Value = new float3(0f, 0f, 1f) });
            }
        }
    }
#endif

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct MovementSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            foreach (var (transform, speed, direction) in
                SystemAPI.Query<RefRW<LocalTransform>, Speed, Direction>())
            {
                transform.ValueRW.Position += speed.Value * dt * direction.Value;
            }
        }
    }
}
