using Unity.Entities;

namespace Partity
{
    /// <summary>
    /// Converts raised Payloads into Emissions and instantiates particles: the shape systems
    /// fill the Emission buffer, and EmitSystem records into a manual ECB it plays back within
    /// its own update, so particles are born here. Runs after the frame's first playback point.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(BeginInitializationEntityCommandBufferSystem))]
    public partial class ShapeSystemGroup : ComponentSystemGroup { }
}
