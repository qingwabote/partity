using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Partity
{
    public struct Lifetime : IComponentData
    {
        public float Life;
        public float Time;
    }

    public struct Direction : IComponentData
    {
        public float3 Value;
    }

    public class LifetimeAuthoring : MonoBehaviour
    {
        public float Life;

        class Baker : Baker<LifetimeAuthoring>
        {
            public override void Bake(LifetimeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponent(entity, new Lifetime
                {
                    Life = authoring.Life
                });
                AddComponent<AnimationFactor>(entity);
                SetComponentEnabled<AnimationFactor>(entity, false);
                AddComponent(entity, new Direction { Value = new float3(0f, 0f, 1f) });
            }
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct LifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (lifetime, entity) in SystemAPI.Query<RefRW<Lifetime>>().WithEntityAccess())
            {
                var lt = lifetime.ValueRO;
                if (lt.Time < lt.Life)
                {
                    lt.Time += dt;
                    lt.Time = math.min(lt.Time, lt.Life);
                }
                else
                {
                    ecb.DestroyEntity(entity);
                }

                lifetime.ValueRW.Time = lt.Time;
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
