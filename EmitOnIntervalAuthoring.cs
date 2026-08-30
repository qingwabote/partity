using Unity.Entities;
using UnityEngine;

namespace Partity
{
    public struct EmitOnInterval : IComponentData
    {
        public float Interval;
        public int Emits;
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
            }
        }
    }

    [RequireMatchingQueriesForUpdate]
    public partial struct EmitOnIntervalSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            foreach (var (config, emitter) in
                SystemAPI.Query<RefRW<EmitOnInterval>, RefRW<Emitter>>())
            {
                var c = config.ValueRO;
                var elapsed = c.Elapsed + dt;

                emitter.ValueRW.Payload += (int)(elapsed / c.Interval) * c.Emits;
                c.Elapsed = elapsed % c.Interval;

                config.ValueRW = c;
            }
        }
    }
}
