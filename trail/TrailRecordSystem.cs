using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Partity
{
    /// <summary>
    /// Records the particle's history into the registry texture, after the transform system.
    /// Pure geometry: one texel per point — (pos.xyz, birthTime). Color is not recorded; the
    /// ribbon reads the particle's live BaseColor as an instanced shader property.
    ///
    /// Managed on purpose (no [BurstCompile]): it reads the managed registry directly —
    /// per-world state owned by TrailInitializeSystem — so the data path has a single source
    /// of truth (the old blittable singleton snapshot and its per-frame alignment duty are
    /// gone). The loop is per-trail microsecond scale; Burst is not load-bearing at partity's
    /// trail counts.
    ///
    /// Sampling: odometer — a point is written when the particle has moved MinVertexDistance
    /// (TrailRenderer.minVertexDistance). Time source is ElapsedTime (unscaled, matching
    /// TrailRenderer's unscaled semantics).
    ///
    /// Expiry: points expire strictly in birth order (births increase toward the head), so a
    /// backward walk from the ring head truncates at the first point older than TTL; the walk
    /// also sums the live polyline length (TotalLength — the Tile-mode hook) and computes the
    /// tail-cut fraction (TailCut) for the shader's exact TTL tip. WidthScale follows the
    /// particle's current size every frame (sizeAffectsWidth; SoL curves grow from 0).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial struct TrailRecordSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // per-world registry (multi-world), owned by the render system; producers write into it
            var history = state.World.GetExistingSystemManaged<TrailRenderSystem>()
                .Registry.History.Source.Value;
            double now = SystemAPI.Time.ElapsedTime;

            foreach (var (parametersRef, stateRef, renderRef, transform) in
                SystemAPI.Query<RefRO<TrailParams>, RefRW<TrailState>, RefRW<TrailRenderData>, RefRO<LocalToWorld>>())
            {
                var p = parametersRef.ValueRO;
                TrailState s = stateRef.ValueRW;
                if (s.Slot < 0)
                    continue; // spawned after this frame's initialize pass: no slot yet (write would be a negative History index)
                float3 pos = transform.ValueRO.Position;

                if (s.LiveCount == 0)
                {
                    s.RingHead = Next(s.RingHead, TrailRegistry.K);
                    WritePoint(history, s.Slot, s.RingHead, pos, now);
                    s.LiveCount = 1;
                    s.LastPosition = pos;
                    s.TotalLength = 0f;
                }
                else if (math.distancesq(pos, s.LastPosition) >=
                         p.MinVertexDistance * p.MinVertexDistance)
                {
                    float step = math.distance(pos, s.LastPosition);
                    s.RingHead = Next(s.RingHead, TrailRegistry.K);
                    WritePoint(history, s.Slot, s.RingHead, pos, now);
                    s.LastPosition = pos;
                    s.TotalLength += step;
                    if (s.LiveCount < TrailRegistry.K)
                        s.LiveCount++;
                }

                s.WidthScale = p.WidthOverTrail * math.length(transform.ValueRO.Value.c0.xyz);
                ExpireTail(ref s, in p, history, now);
                if (s.LiveCount == 0)
                    s.LastPosition = pos; // chain fully expired: re-arm sampling from here

                stateRef.ValueRW = s;
                renderRef.ValueRW.Value = new float4(
                    s.Slot * TrailRegistry.K + s.RingHead, s.LiveCount, s.WidthScale, s.TailCut);
            }
        }

        /// <summary>
        /// Backward scan from the newest point: truncate at the first point older than TTL
        /// (births increase toward the head, so everything older is dead too), re-sum the live
        /// polyline length, and compute TailCut — the interpolation fraction between the oldest
        /// live point and the first expired point where age == TTL exactly (0.1s window guard
        /// rejects stale data from slot reuse; all time math stays CPU-side, the shader has no clock).
        /// </summary>
        static void ExpireTail(ref TrailState s, in TrailParams p, NativeArray<float> history, double now)
        {
            int live = 0;
            float liveLength = 0f;
            float3 toward = s.LastPosition;
            float oldestBirth = 0f;
            s.TailCut = 0f;
            for (int i = 0; i < s.LiveCount; i++)
            {
                int ring = Mod(s.RingHead - i, TrailRegistry.K);
                int b = (s.Slot * TrailRegistry.K + ring) * 4;
                float3 pt = new float3(history[b], history[b + 1], history[b + 2]);
                float birth = history[b + 3];
                if (now - birth > p.Lifetime)
                {
                    float age0 = (float)now - oldestBirth;
                    float agex = (float)now - birth;
                    if (agex > age0 && agex - age0 < 0.1)
                        s.TailCut = math.saturate((p.Lifetime - age0) / (agex - age0));
                    break;
                }
                oldestBirth = birth;
                live++;
                liveLength += math.distance(toward, pt);
                toward = pt;
            }

            s.LiveCount = live;
            s.TotalLength = liveLength;
        }

        static int Next(int ring, int k) => (ring + 1) % k;

        static int Mod(int x, int k) => ((x % k) + k) % k;

        static void WritePoint(NativeArray<float> h, int slot, int ring, float3 pos, double now)
        {
            int b = (slot * TrailRegistry.K + ring) * 4;
            h[b + 0] = pos.x;
            h[b + 1] = pos.y;
            h[b + 2] = pos.z;
            h[b + 3] = (float)now;
        }
    }
}
