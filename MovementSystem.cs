using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct MovementSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            foreach (var (transform, speed, direction, scale) in
                SystemAPI.Query<RefRW<LocalTransform>, Speed, Direction, SpaceScale>())
            {
                transform.ValueRW.Position += speed.Value * scale.Value * dt * direction.Value;
            }
        }
    }
}
