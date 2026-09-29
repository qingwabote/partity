using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Partity
{
    /// <summary>
    /// Init side of the cleanup-marker state machine: a fresh particle is TrailParams + dormant
    /// baked state WITHOUT TrailCleanup — this system assigns its registry slot, stamps the
    /// dormant values, and adds TrailCleanup (the "initialized" marker, later the death shell
    /// reaped by <see cref="TrailCleanupSystem"/>). One structural change per spawn, by design:
    /// cleanup components are stripped by entity scene baking, so the marker cannot be
    /// pre-baked; the dormant TrailState/TrailRenderData still are, keeping the spawn cost to
    /// this single add.
    ///
    /// A registry PRODUCER (AssignSlot, ring clearing on reuse) — the registry itself is owned
    /// by its single consumer, <see cref="TrailRenderSystem"/>, which creates it eagerly in
    /// OnCreate and never hands out null.
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
                .WithAll<TrailParams>()
                .WithNone<TrailCleanup>()
                .Build()
                .ToEntityArray(Allocator.Temp);
            if (heads.Length == 0)
            {
                heads.Dispose();
                return;
            }

            // per-world registry (multi-world), owned by the render system
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
                    LiveCount = 0,
                    // sentinel: the first sample records unconditionally (the head's LTW is not
                    // computed yet, so there is no honest anchor to compare against)
                    LastPosition = new float3(float.MaxValue),
                });
                state.EntityManager.SetComponentData(head, new TrailRenderData
                {
                    Value = new float4(slot * TrailRegistry.K - 1, 0f, 0f, 0f)
                });
                state.EntityManager.AddComponentData(head, new TrailCleanup { Slot = slot });
            }

            heads.Dispose();
        }
    }
}
