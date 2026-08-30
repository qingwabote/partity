using Unity.Entities;
using UnityEngine;

namespace Partity
{
    public struct EmitOnTime : IComponentData
    {
        public float Time;
        public int Emits;
        public bool Emitted;
    }

    public class EmitOnTimeAuthoring : MonoBehaviour
    {
        [Min(0f)] public float Time = 0f;
        public int Emits = 1;

        class Baker : Baker<EmitOnTimeAuthoring>
        {
            public override void Bake(EmitOnTimeAuthoring authoring)
            {
                if (GetComponent<DurationAuthoring>() == null)
                    Debug.LogError($"{authoring.name}: EmitOnTimeAuthoring requires a sibling DurationAuthoring.");
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new EmitOnTime
                {
                    Time = Mathf.Max(authoring.Time, 0f),
                    Emits = authoring.Emits,
                });
            }
        }
    }

    [RequireMatchingQueriesForUpdate]
    public partial struct EmitOnTimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (config, emitter, duration) in
                SystemAPI.Query<RefRW<EmitOnTime>, RefRW<Emitter>, Duration>())
            {
                var c = config.ValueRO;
                if (!c.Emitted && duration.Elapsed >= c.Time)
                {
                    emitter.ValueRW.Payload += c.Emits;
                    c.Emitted = true;
                }
                if (duration.Elapsed == duration.Total)
                    c.Emitted = false;

                config.ValueRW = c;
            }
        }
    }
}
