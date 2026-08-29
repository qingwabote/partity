using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    [WriteGroup(typeof(Emitter))]
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
            foreach (var (emitterRef, world, line, buffer) in
                SystemAPI.Query<RefRW<Emitter>, LocalToWorld, ShapeLine, DynamicBuffer<Emission>>())
            {
                var emitter = emitterRef.ValueRO;
                if (emitter.Payload <= 0) continue;

                var emitterRotation = math.mul(world.Value.Rotation(), line.Rotation);
                var record = new Emission
                {
                    Position = world.Value.Translation(),
                    Rotation = emitterRotation
                };
                for (int i = 0; i < emitter.Payload; i++)
                {
                    buffer.Add(record);
                }

                emitterRef.ValueRW.Payload = 0;
            }
        }
    }
}
