using Unity.Entities;

namespace Partity
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial class ShapeSystemGroup : ComponentSystemGroup { }
}
