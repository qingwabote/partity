using Unity.Entities;
using UnityEngine;

namespace Partity
{
    public struct EmitOnTime : IComponentData
    {
        public float Time;
        public int Emits;
    }

    public struct EmitOnTimeTimer : IComponentData
    {
        public float Elapsed;
    }

    public class EmitOnTimeAuthoring : MonoBehaviour
    {
        [Min(0f)] public float Time = 0f;
        public int Emits = 1;

        class Baker : Baker<EmitOnTimeAuthoring>
        {
            public override void Bake(EmitOnTimeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new EmitOnTime
                {
                    Time = authoring.Time,
                    Emits = authoring.Emits,
                });
                AddComponent(entity, new EmitOnTimeTimer());
            }
        }
    }

    [RequireMatchingQueriesForUpdate]
    public partial struct EmitOnTimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (config, timer, payload, entity) in
                SystemAPI.Query<EmitOnTime, RefRW<EmitOnTimeTimer>, RefRW<EmitterPayload>>().WithEntityAccess())
            {
                var elapsed = timer.ValueRO.Elapsed + dt;
                timer.ValueRW.Elapsed = elapsed;

                if (elapsed < config.Time) continue;

                payload.ValueRW.Value += config.Emits;
                ecb.RemoveComponent<EmitOnTime>(entity);
                ecb.RemoveComponent<EmitOnTimeTimer>(entity);
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
