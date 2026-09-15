using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Partity
{
    [WriteGroup(typeof(Emission))]
    public struct ShapeLine : IComponentData
    {
    }

    public class ShapeLineAuthoring : MonoBehaviour
    {
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale = Vector3.one;

        class Baker : Baker<ShapeLineAuthoring>
        {
            public override void Bake(ShapeLineAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<ShapeLine>(entity);
                AddComponent(entity, new ShapeTransform
                {
                    Position = authoring.Position,
                    Rotation = Quaternion.Euler(authoring.Rotation),
                    Scale = authoring.Scale,
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
            foreach (var (emitter, buffer, transform) in
                SystemAPI.Query<RefRW<Emitter>, DynamicBuffer<Emission>, ShapeTransform>().WithAll<ShapeLine>())
            {
                var e = emitter.ValueRO;
                if (e.Payload <= 0) continue;

                for (int i = 0; i < e.Payload; i++)
                {
                    buffer.Add(new Emission
                    {
                        Position = transform.Position,
                        Rotation = transform.Rotation
                    });
                }

                emitter.ValueRW.Payload = 0;
            }
        }
    }
}
