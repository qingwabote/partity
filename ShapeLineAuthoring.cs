using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    [WriteGroup(typeof(Emission))]
    public struct ShapeLine : IComponentData
    {
        public quaternion Rotation;
    }

    public class ShapeLineAuthoring : MonoBehaviour
    {
        public Vector3 Rotation;

        class Baker : Baker<ShapeLineAuthoring>
        {
            public override void Bake(ShapeLineAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new ShapeLine
                {
                    Rotation = Quaternion.Euler(authoring.Rotation)
                });
            }
        }
    }

    [UpdateInGroup(typeof(ShapeSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ShapeLineSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (emitter, buffer, world, line) in
                SystemAPI.Query<RefRW<Emitter>, DynamicBuffer<Emission>, LocalToWorld, ShapeLine>().WithNone<Paused>())
            {
                var e = emitter.ValueRO;
                if (e.Payload <= 0) continue;

                var rotation = math.mul(world.Value.Rotation(), line.Rotation);
                for (int i = 0; i < e.Payload; i++)
                {
                    buffer.Add(new Emission
                    {
                        Position = world.Value.Translation(),
                        Rotation = rotation
                    });
                }

                emitter.ValueRW.Payload = 0;
            }
        }
    }
}
