using Unity.Entities;

namespace Partity
{
    [RequireMatchingQueriesForUpdate]
    public partial struct ParticleLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (lifetime, entity) in SystemAPI.Query<RefRW<Lifetime>>().WithEntityAccess())
            {
                if (lifetime.ValueRO.Time >= lifetime.ValueRO.Life)
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }
                lifetime.ValueRW.Time += dt;
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
