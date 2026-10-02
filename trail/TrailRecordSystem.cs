using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Partity
{
    /// <summary>
    /// Managed on purpose (no [BurstCompile]): it reads the managed registry directly, keeping
    /// a single source of truth — reintroducing any "snapshot into a blittable singleton" hop
    /// re-creates the double-truth-source desync (texture rebuilt while the array stays
    /// stale). The loop is per-trail microsecond scale; Burst is not load-bearing at partity
    /// trail counts.
    ///
    /// Color is deliberately not recorded: the ribbon reads the particle's live BaseColor as
    /// an instanced property. Time is deliberately unscaled (TrailRenderer parity).
    ///
    /// Expiry invariant: births strictly increase toward the head, so the backward walk may
    /// truncate at the first point older than TTL. WidthScale is recomputed every frame —
    /// sizeAffectsWidth semantics, and SoL curves grow from 0, so a latched width is
    /// sub-pixel at spawn.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial struct TrailRecordSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var history = state.World.GetExistingSystemManaged<TrailRenderSystem>()
                .Registry.History.Source.Value;
            double now = SystemAPI.Time.ElapsedTime;

            foreach (var (parametersRef, stateRef, renderRef, transform) in
                SystemAPI.Query<RefRO<TrailRenderer>, RefRW<TrailState>, RefRW<TrailData>, RefRO<LocalToWorld>>())
            {
                var p = parametersRef.ValueRO;
                TrailState s = stateRef.ValueRW;
                if (s.Slot < 0)
                    continue; // spawned after this frame's initialize pass: no slot yet (write would be a negative History index)
                float3 pos = transform.ValueRO.Position;
                int liveCount = (int)renderRef.ValueRO.LiveCount;

                if (liveCount == 0)
                {
                    s.RingHead = Next(s.RingHead, TrailRegistry.K);
                    WritePoint(history, s.Slot, s.RingHead, pos, now);
                    liveCount = 1;
                    s.LastPosition = pos;
                    s.TotalLength = 0f;
                }
                else if (math.distancesq(pos, s.LastPosition) >= p.MinVertexDistance * p.MinVertexDistance)
                {
                    float step = math.distance(pos, s.LastPosition);
                    s.RingHead = Next(s.RingHead, TrailRegistry.K);
                    WritePoint(history, s.Slot, s.RingHead, pos, now);
                    s.LastPosition = pos;
                    s.TotalLength += step;
                    if (liveCount < TrailRegistry.K)
                        liveCount++;
                }

                ExpireTail(history, in p, in s, ref liveCount, now, out float tailCut, out float liveLength);
                s.TotalLength = liveLength;
                if (liveCount == 0)
                    s.LastPosition = pos; // chain fully expired: re-arm sampling from here

                stateRef.ValueRW = s;
                renderRef.ValueRW = new TrailData
                {
                    HeadRow = s.Slot * TrailRegistry.K + s.RingHead,
                    LiveCount = liveCount,
                    WidthScale = p.WidthOverTrail * math.length(transform.ValueRO.Value.c0.xyz),
                    TailCut = tailCut,
                };
            }
        }

        /// <summary>
        /// The 0.1s window guard rejects points that predate the current generation (slot
        /// reuse leaves legally-dated corpses); all time math stays here because the shader is
        /// a clockless pure function.
        /// </summary>
        static void ExpireTail(NativeArray<float> history, in TrailRenderer p, in TrailState s,
            ref int liveCount, double now, out float tailCut, out float liveLength)
        {
            int live = 0;
            float liveLen = 0f;
            float3 toward = s.LastPosition;
            float oldestBirth = 0f;
            tailCut = 0f;
            for (int i = 0; i < liveCount; i++)
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
                        tailCut = math.saturate((p.Lifetime - age0) / (agex - age0));
                    break;
                }
                oldestBirth = birth;
                live++;
                liveLen += math.distance(toward, pt);
                toward = pt;
            }

            liveCount = live;
            liveLength = liveLen;
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
