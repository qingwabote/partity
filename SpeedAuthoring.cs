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

#if UNITY_EDITOR
    public class SpeedAuthoring : MonoBehaviour
    {
        public bool LocalSpace;

        class Baker : Baker<SpeedAuthoring>
        {
            public override void Bake(SpeedAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Speed { Value = 0f });
                AddComponent(entity, new Direction { Value = new float3(0f, 0f, 1f) });
                if (authoring.LocalSpace)
                    AddComponent(entity, new LocalSpace { Rotation = quaternion.identity, Scale = 1f });
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

            foreach (var (transform, speed, direction, localSpace) in
                SystemAPI.Query<RefRW<LocalTransform>, Speed, Direction, LocalSpace>().WithNone<Paused>())
            {
                transform.ValueRW.Position += math.rotate(localSpace.Rotation, speed.Value * localSpace.Scale * dt * direction.Value);
            }

            foreach (var (transform, speed, direction) in
                SystemAPI.Query<RefRW<LocalTransform>, Speed, Direction>().WithNone<LocalSpace, Paused>())
            {
                transform.ValueRW.Position += speed.Value * dt * direction.Value;
            }
        }
    }
}
