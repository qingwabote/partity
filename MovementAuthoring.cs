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

#if UNITY_EDITOR
    public class MovementAuthoring : MonoBehaviour
    {
        public float Speed;

        class Baker : Baker<MovementAuthoring>
        {
            public override void Bake(MovementAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Speed { Value = authoring.Speed });
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
