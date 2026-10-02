using System.Collections.Generic;
using Bastard;

namespace Partity
{
    /// <summary>
    /// Pre-allocated at full capacity: TextureView growth goes through Texture.Reinitialize
    /// (new GPU storage, hitches a frame), so the pool must never resize in play.
    ///
    /// One per trail-carrying world, held by that world's TrailRenderSystem: the single
    /// consumer (the shader path) owns the lifecycle, initialize/record/cleanup are producers.
    /// </summary>
    public class TrailRegistry
    {
        // the material asset's _TrailRingK and the ribbon mesh's segment count are baked from
        // this value — changing K means regenerating both (Partity/Create Trail Ribbon Assets)
        public const int K = 24;
        public const int Capacity = 1024;

        public readonly TextureView History;

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
        /// Without this, every editor play session leaks one Persistent allocation plus the
        /// history texture (NativeLeakDetection reports it at each domain reload).
        /// </summary>
        public void Dispose()
        {
            History.Dispose();
            UnityEngine.Object.Destroy(History.Texture);
        }
    }
}
