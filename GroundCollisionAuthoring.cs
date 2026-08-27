using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    public struct GroundCollision : IComponentData
    {
        public float Bounce;
        public float Dampen;
        public float RadiusScale;
    }

#if UNITY_EDITOR
    public class GroundCollisionAuthoring : MonoBehaviour
    {
        [Range(0f, 1f)] public float Bounce;
        [Range(0f, 1f)] public float Dampen;
        [Min(0f)] public float RadiusScale = 1f;

        class Baker : Baker<GroundCollisionAuthoring>
        {
            public override void Bake(GroundCollisionAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new GroundCollision
                {
                    Bounce = authoring.Bounce,
                    Dampen = authoring.Dampen,
                    RadiusScale = authoring.RadiusScale,
                });
            }
        }
    }
#endif

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct GroundCollisionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (collision, transform, speed, direction) in
                SystemAPI.Query<GroundCollision, RefRW<LocalTransform>, RefRW<Speed>, RefRW<Direction>>())
            {
                var radius = 0.5f * transform.ValueRO.Scale * collision.RadiusScale;
                if (transform.ValueRO.Position.y >= radius) continue;

                var dir = direction.ValueRO.Value;
                var v = dir * speed.ValueRO.Value;
                var reflected = math.select(v,
                    new float3(v.x * (1f - collision.Dampen), -v.y * collision.Bounce, v.z * (1f - collision.Dampen)),
                    v.y < 0f);

                speed.ValueRW.Value = math.length(reflected);
                direction.ValueRW.Value = math.normalizesafe(reflected, dir);
                transform.ValueRW.Position.y = radius;
            }
        }
    }
}
