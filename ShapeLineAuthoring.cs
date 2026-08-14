using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
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
                    Rotation = Quaternion.Euler(authoring.Rotation),
                });
            }
        }
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ShapeLineSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var em = state.EntityManager;

            foreach (var (emitter, payload, transform, line) in
                SystemAPI.Query<Emitter, RefRW<EmitterPayload>, LocalTransform, ShapeLine>())
            {
                int count = payload.ValueRO.Value;
                if (count <= 0) continue;

                var prefabLT = em.GetComponentData<LocalTransform>(emitter.ParticlePrefab);
                var rotation = math.mul(transform.Rotation, line.Rotation);

                for (int j = 0; j < count; j++)
                {
                    var p = ecb.Instantiate(emitter.ParticlePrefab);
                    var lt = prefabLT;
                    lt.Position = transform.Position + math.rotate(rotation, prefabLT.Position);
                    lt.Rotation = math.mul(rotation, prefabLT.Rotation);
                    ecb.SetComponent(p, lt);
                    ecb.SetComponent(p, new Direction
                    {
                        Value = math.rotate(rotation, new float3(0f, 0f, 1f))
                    });
                }

                payload.ValueRW.Value = 0;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
