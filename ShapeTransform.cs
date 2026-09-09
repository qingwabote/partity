using Unity.Entities;
using Unity.Mathematics;

namespace Partity
{
    public struct ShapeTransform : IComponentData
    {
        public float3 Position;
        public quaternion Rotation;
        public float3 Scale;
    }
}
