using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    public struct Emitter : IComponentData
    {
        public Entity ParticlePrefab;
        public float Size;
        public int Payload;
    }

    public class EmitterAuthoring : MonoBehaviour
    {
        public GameObject ParticlePrefab;
        public float Size = 1f;

        class Baker : Baker<EmitterAuthoring>
        {
            public override void Bake(EmitterAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Emitter
                {
                    ParticlePrefab = GetEntity(authoring.ParticlePrefab, TransformUsageFlags.Dynamic),
                    Size = authoring.Size,
                });
            }
        }
    }

    [UpdateInGroup(typeof(ShapeSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ShapePointSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var em = state.EntityManager;

            foreach (var (emitterRef, world) in
                SystemAPI.Query<RefRW<Emitter>, LocalToWorld>()
                    .WithOptions(EntityQueryOptions.FilterWriteGroup))
            {
                var emitter = emitterRef.ValueRO;
                if (emitter.Payload <= 0) continue;

                var particlePrefab = emitter.ParticlePrefab;
                var offset = em.GetComponentData<LocalTransform>(particlePrefab);
                var transform = LocalTransform.FromPositionRotationScale(
                    world.Value.Translation() + offset.Position,
                    offset.Rotation,
                    offset.Scale * emitter.Size);

                for (int i = 0; i < emitter.Payload; i++)
                {
                    var p = ecb.Instantiate(particlePrefab);
                    ecb.SetComponent(p, transform);
                }

                emitterRef.ValueRW.Payload = 0;
            }
        }
    }
}
