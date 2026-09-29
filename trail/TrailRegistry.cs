using System.Collections.Generic;
using Bastard;

namespace Partity
{
    /// <summary>
    /// Managed registry: the history texture (bastard TextureView, RGBAFloat) and the free-slot pool.
    /// Texture layout is the TextureView's flat linear order; a point occupies 2 texels so the
    /// texel for (slot, ring) starts at linear index (slot*K + ring) * 2 — the shader converts a
    /// linear index to (x, y) with the texture width from _TrailTex_TexelSize.
    ///
    /// Pre-allocated at full capacity: TextureView growth goes through Texture.Reinitialize
    /// (new GPU storage, hitches a frame), so the pool must never resize in play.
    ///
    /// Slot liveness lives in ECS (TrailCleanup shells reaped by TrailSweepSystem); the pool
    /// itself is a bare free-stack.
    ///
    /// One per trail-carrying world: an instance field of that world's TrailRenderSystem —
    /// the single consumer of the registry (the shader path) owns the resource's lifecycle,
    /// while the simulation systems (initialize/record/cleanup) are producers writing into
    /// it. Created eagerly in its OnCreate and disposed in its OnDestroy. No process-wide
    /// state, so multiple worlds each get their own pool and texture.
    /// </summary>
    public class TrailRegistry
    {
        public const int K = 24;        // ring depth = max points per trail (fidelity knob)
        public const int Capacity = 1024;

        public readonly TextureView History;   // Capacity * K * 2 texels = 256x256 RGBAFloat

        readonly Stack<int> m_FreeSlots;

        public TrailRegistry()
        {
            History = new TextureView(Capacity * K * 2 * 4);
            var black = new UnityEngine.Color32[History.Texture.width * History.Texture.height];
            History.Texture.SetPixels32(black);
            History.Texture.Apply(false);
            m_FreeSlots = new Stack<int>(Capacity);
            for (int i = Capacity - 1; i >= 0; i--)
                m_FreeSlots.Push(i);
        }

        public int AssignSlot() => m_FreeSlots.Pop();

        public void ReleaseSlot(int slot) => m_FreeSlots.Push(slot);

        /// <summary>
        /// Tears down the pool: frees the native reference backing the history view and destroys
        /// the texture object. Called by the owning world's TrailInitializeSystem.OnDestroy —
        /// without it, every editor play session leaks one Persistent allocation plus a 256KB
        /// RGBAFloat texture (reported as "Leak Detected : Persistent allocates 1 individual
        /// allocations" at each domain reload).
        /// </summary>
        public void Dispose()
        {
            History.Dispose(); // frees the NativeReference (Persistent)
            UnityEngine.Object.Destroy(History.Texture);
        }
    }
}
