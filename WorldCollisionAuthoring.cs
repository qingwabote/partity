using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    public struct WorldCollision : IComponentData
    {
        public float Bounce;
        public float Dampen;
        public float RadiusScale;
    }

#if UNITY_EDITOR
    public class WorldCollisionAuthoring : MonoBehaviour
    {
        [Range(0f, 1f)] public float Bounce;
        [Range(0f, 1f)] public float Dampen;
        [Min(0f)] public float RadiusScale = 1f;

        class Baker : Baker<WorldCollisionAuthoring>
        {
            public override void Bake(WorldCollisionAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new WorldCollision
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
    public partial struct WorldCollisionSystem : ISystem
    {
        private BlobAssetReference<Unity.Physics.Collider> m_SphereCollider;

        public void OnCreate(ref SystemState state)
        {
            m_SphereCollider = Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = 1f }, CollisionFilter.Default);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (m_SphereCollider.IsCreated) m_SphereCollider.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            state.CompleteDependency();
            var collisionWorld = physicsWorld.CollisionWorld;
            var dt = SystemAPI.Time.DeltaTime;

            foreach (var (collision, transform, speed, direction) in
                SystemAPI.Query<WorldCollision, RefRW<LocalTransform>, RefRW<Speed>, RefRW<Direction>>())
            {
                var dir = direction.ValueRO.Value;
                var spd = speed.ValueRO.Value;
                if (spd == 0f) continue;

                var origin = transform.ValueRO.Position - dir * spd * dt;
                var radius = transform.ValueRO.Scale * 0.5f * collision.RadiusScale;

                var input = new ColliderCastInput(m_SphereCollider, origin, transform.ValueRO.Position, quaternion.identity, radius);
                if (!collisionWorld.CastCollider(input, out var hit))
                    continue;

                var n = hit.SurfaceNormal;
                var v = dir * spd;
                var vN = math.dot(v, n);
                var vT = v - vN * n;
                var reflected = vT * (1f - collision.Dampen) - vN * collision.Bounce * n;

                transform.ValueRW.Position = origin + (transform.ValueRO.Position - origin) * hit.Fraction + reflected * (1f - hit.Fraction) * dt;
                speed.ValueRW.Value = math.length(reflected);
                direction.ValueRW.Value = math.normalizesafe(reflected, dir);
            }
        }
    }
}
