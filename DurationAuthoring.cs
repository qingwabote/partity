using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Partity
{
    public struct Duration : IComponentData
    {
        public float Total;
        public bool Looping;
        public float Elapsed;
    }

    public class DurationAuthoring : MonoBehaviour
    {
        [Min(0.01f)] public float Total = 5f;
        public bool Looping;

        class Baker : Baker<DurationAuthoring>
        {
            public override void Bake(DurationAuthoring authoring)
            {
                if (GetComponent<EmitterAuthoring>() == null)
                    Debug.LogError($"{authoring.name}: DurationAuthoring requires a sibling EmitterAuthoring.");
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Duration
                {
                    Total = Mathf.Max(authoring.Total, 0.01f),
                    Looping = authoring.Looping,
                });
            }
        }
    }

    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct DurationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            foreach (var (duration, emitter) in
                SystemAPI.Query<RefRW<Duration>, EnabledRefRW<Emitter>>())
            {
                var d = duration.ValueRO;

                if (d.Elapsed < d.Total)
                    d.Elapsed = math.min(d.Elapsed + dt, d.Total);
                else if (d.Looping)
                    d.Elapsed = 0f;
                else
                    emitter.ValueRW = false;

                duration.ValueRW = d;
            }
        }
    }
}
