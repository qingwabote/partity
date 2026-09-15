using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Partity
{
    public struct Lifetime : IComponentData
    {
        public float Life;
        public float Time;
        /// <summary>
        /// Random min/max curve blend factor, fixed at birth and constant across frames.
        /// </summary>
        public float Lerp;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Authoring for a bare <see cref="Lifetime"/>: the entity tracks its own age and is destroyed when it expires.
    /// Unlike <see cref="StartLifetimeAuthoring"/>, there is no birth-time curve and no Nudge initialization, so a
    /// Life value set at runtime (e.g. per weapon level) is never overwritten.
    /// </summary>
    public class LifetimeAuthoring : MonoBehaviour
    {
        public float Life = 1f;

        class Baker : Baker<LifetimeAuthoring>
        {
            public override void Bake(LifetimeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponent(entity, new Lifetime
                {
                    Life = authoring.Life
                });
            }
        }
    }
#endif

    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct LifetimeLerpSystem : ISystem
    {
        private Unity.Mathematics.Random m_Random;

        public void OnCreate(ref SystemState state)
        {
            m_Random = new Unity.Mathematics.Random(0x9E3779B1u);
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var lifetime in SystemAPI.Query<RefRW<Lifetime>>().WithAll<Nudge>())
            {
                lifetime.ValueRW.Lerp = m_Random.NextFloat();
            }
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(StartLifetimeSystem))]
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
                lt.Time = math.min(lt.Time + dt, lt.Life);
                lifetime.ValueRW.Time = lt.Time;

                if (lt.Time >= lt.Life)
                    ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
