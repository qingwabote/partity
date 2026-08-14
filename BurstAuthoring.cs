using System;
using System.Linq;
using Unity.Entities;
using UnityEngine;

namespace Partity
{
    [Serializable]
    public struct Burst : IBufferElementData
    {
        public float Time;
        public int Count;
    }

    public struct BurstTimer : IComponentData
    {
        public float Elapsed;
        public int Index;
    }

    public class BurstAuthoring : MonoBehaviour
    {
        public Burst[] Bursts;

        class Baker : Baker<BurstAuthoring>
        {
            public override void Bake(BurstAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new BurstTimer());
                var buf = AddBuffer<Burst>(entity);
                if (authoring.Bursts != null)
                {
                    foreach (var b in authoring.Bursts.OrderBy(b => b.Time))
                    {
                        buf.Add(new Burst { Time = b.Time, Count = b.Count });
                    }
                }
            }
        }
    }

    [RequireMatchingQueriesForUpdate]
    public partial struct BurstSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            foreach (var (timer, payload, bursts) in
                SystemAPI.Query<RefRW<BurstTimer>, RefRW<EmitterPayload>, DynamicBuffer<Burst>>())
            {
                var t = timer.ValueRW;
                t.Elapsed += dt;

                var pl = payload.ValueRW;
                while (t.Index < bursts.Length && bursts[t.Index].Time <= t.Elapsed)
                {
                    pl.Value += bursts[t.Index].Count;
                    t.Index++;
                }

                payload.ValueRW = pl;
                timer.ValueRW = t;
            }
        }
    }
}
