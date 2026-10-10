using Unity.Entities;

namespace Partity
{
    /// <summary>
    /// Birth evaluation of particles instantiated by EmitSystem: the Nudge-driven start
    /// evaluators run right after <see cref="ShapeSystemGroup"/>, in the same frame phase the
    /// particles were born and before the frame's destroy point, so a dying emitter is still
    /// readable throughout.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(ShapeSystemGroup))]
    public partial class StartSystemGroup : ComponentSystemGroup { }
}
