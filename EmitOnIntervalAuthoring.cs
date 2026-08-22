using Unity.Entities;
using UnityEngine;

namespace Partity
{
    public struct EmitOnInterval : IComponentData
    {
        public float Interval;
        public int Emits;
    }

    public struct EmitOnIntervalTimer : IComponentData
    {
        public float Elapsed;
    }

    public class EmitOnIntervalAuthoring : MonoBehaviour
    {
        [Min(0.0001f)] public float Interval = 1f;
        public int Emits = 1;

        class Baker : Baker<EmitOnIntervalAuthoring>
        {
            public override void Bake(EmitOnIntervalAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new EmitOnInterval
                {
                    Interval = authoring.Interval,
                    Emits = authoring.Emits,
                });
                AddComponent(entity, new EmitOnIntervalTimer());
            }
        }
    }

    [RequireMatchingQueriesForUpdate]
    public partial struct EmitOnIntervalSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            foreach (var (config, timer, emitter) in
                SystemAPI.Query<EmitOnInterval, RefRW<EmitOnIntervalTimer>, RefRW<Emitter>>())
            {
                var elapsed = timer.ValueRO.Elapsed + dt;

                emitter.ValueRW.Payload += (int)(elapsed / config.Interval) * config.Emits;
                timer.ValueRW.Elapsed = elapsed % config.Interval;
            }
        }
    }
}
