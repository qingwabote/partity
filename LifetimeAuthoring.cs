using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Partity
{
    public struct Lifetime : IComponentData
    {
        public float Life;
        public float Time;
        public float Lerp;
    }

    public class LifetimeAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve Life = new ParticleSystem.MinMaxCurve(1f);

        class Baker : Baker<LifetimeAuthoring>
        {
            public override void Bake(LifetimeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Renderable);
                if (authoring.Life.mode == ParticleSystemCurveMode.Constant)
                {
                    AddComponent(entity, new Lifetime
                    {
                        Life = authoring.Life.constant
                    });
                }
                else
                {
                    AddComponent(entity, new Lifetime
                    {
                        Life = 0f
                    });
                    AddComponent(entity, new StartLifetime
                    {
                        Curve = authoring.Life.ToBlob()
                    });
                }
                AddComponent<Nudge>(entity);
            }
        }
    }

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
