using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Partity
{
    public struct EmitOnTime : IComponentData
    {
        public float Time;
        public int Emits;
        public int Cycles;
        public float Interval;
        public float Reset;
        public float Elapsed;
        public int Fired;
    }

    public class EmitOnTimeAuthoring : MonoBehaviour
    {
        [Min(0f)] public float Time = 0f;
        public int Emits = 1;
        [Min(0)] public int Cycles = 1;
        [Min(0.0001f)] public float Interval = 0.01f;
        [Min(0f)] public float Reset = 0f;

        class Baker : Baker<EmitOnTimeAuthoring>
        {
            public override void Bake(EmitOnTimeAuthoring authoring)
            {
                if (GetComponent<EmitterAuthoring>() == null)
                    Debug.LogError($"{authoring.name}: EmitOnTimeAuthoring requires a sibling EmitterAuthoring.");
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new EmitOnTime
                {
                    Time = Mathf.Max(authoring.Time, 0f),
                    Emits = authoring.Emits,
                    Cycles = authoring.Cycles <= 0 ? int.MaxValue : authoring.Cycles,
                    Interval = Mathf.Max(authoring.Interval, 0.0001f),
                    Reset = Mathf.Max(authoring.Reset, 0f),
                });
            }
        }
    }

    [RequireMatchingQueriesForUpdate]
    public partial struct EmitOnTimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            foreach (var (config, emitter) in
                SystemAPI.Query<RefRW<EmitOnTime>, RefRW<Emitter>>())
            {
                var c = config.ValueRO;

                var fires = c.Elapsed < c.Time
                    ? 0
                    : math.min((int)((c.Elapsed - c.Time) / c.Interval) + 1, c.Cycles);
                emitter.ValueRW.Payload += (fires - c.Fired) * c.Emits;
                c.Fired = fires;

                if (c.Reset <= 0f || c.Elapsed < c.Reset)
                    c.Elapsed += dt;
                else
                {
                    c.Elapsed = 0f;
                    c.Fired = 0;
                }

                config.ValueRW = c;
            }
        }
    }
}
