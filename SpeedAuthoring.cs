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

    public struct SpaceScale : IComponentData
    {
        public float Value;
    }

#if UNITY_EDITOR
    public class SpeedAuthoring : MonoBehaviour
    {
        class Baker : Baker<SpeedAuthoring>
        {
            public override void Bake(SpeedAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Speed { Value = 0f });
                AddComponent(entity, new Direction { Value = new float3(0f, 0f, 1f) });
                AddComponent(entity, new SpaceScale { Value = 1f });
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

            foreach (var (transform, speed, direction, scale) in
                SystemAPI.Query<RefRW<LocalTransform>, Speed, Direction, SpaceScale>().WithNone<Paused>())
            {
                transform.ValueRW.Position += speed.Value * scale.Value * dt * direction.Value;
            }
        }
    }
}
