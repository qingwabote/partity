using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Partity
{
    /// <summary>
    /// One structural change per spawn, by design: cleanup components are stripped by entity
    /// scene baking, so the TrailCleanup marker cannot be pre-baked and its runtime add
    /// doubles as the "initialized" detector — the dormant TrailState/TrailRenderData are
    /// baked precisely to keep the spawn cost down to this single add.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TrailRecordSystem))]
    public partial struct TrailInitializeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // snapshot first: the loop adds TrailCleanup (structural) and SystemAPI.Query
            // iteration forbids structural changes
            var heads = SystemAPI.QueryBuilder()
                .WithAll<TrailRenderer>()
                .WithNone<TrailCleanup>()
                .Build()
                .ToEntityArray(Allocator.Temp);
            if (heads.Length == 0)
            {
                heads.Dispose();
                return;
            }

            var registry = state.World.GetExistingSystemManaged<TrailRenderSystem>().Registry;

            foreach (var head in heads)
            {
                int slot = registry.AssignSlot();

                // slot 复用时清掉上一代残留(出生时间合法的旧点会污染尾端切点等消费者)
                var h = registry.History.Source.Value;
                int baseF = slot * TrailRegistry.K * 4;
                for (int i = 0; i < TrailRegistry.K * 4; i++) h[baseF + i] = 0f;

                state.EntityManager.SetComponentData(head, new TrailState
                {
                    Slot = slot,
                    RingHead = -1,
                    // sentinel: the first sample records unconditionally (the head's LTW is not
                    // computed yet, so there is no honest anchor to compare against)
                    LastPosition = new float3(float.MaxValue),
                });
                state.EntityManager.SetComponentData(head, new TrailData
                {
                    HeadRow = slot * TrailRegistry.K - 1
                });
                state.EntityManager.AddComponentData(head, new TrailCleanup { Slot = slot });
            }

            heads.Dispose();
        }
    }
}
