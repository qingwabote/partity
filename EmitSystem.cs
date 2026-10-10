using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Partity
{
    public struct Emission : IBufferElementData
    {
        public float3 Position;
        public quaternion Rotation;
    }

    [UpdateInGroup(typeof(ShapeSystemGroup), OrderLast = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct EmitSystem : ISystem
    {
        private Random m_Random;

        public void OnCreate(ref SystemState state)
        {
            m_Random = new Random(456789123);
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var em = state.EntityManager;

            foreach (var (buffer, emitter, world, entity) in
                SystemAPI.Query<DynamicBuffer<Emission>, Emitter, LocalToWorld>().WithEntityAccess())
            {
                if (buffer.Length == 0) continue;

                var hasPtm = em.HasComponent<PostTransformMatrix>(emitter.ParticlePrefab);
                var prefabPtm = hasPtm ? em.GetComponentData<PostTransformMatrix>(emitter.ParticlePrefab).Value : float4x4.identity;

                var offset = em.GetComponentData<LocalTransform>(emitter.ParticlePrefab);

                var emitterScale = math.length(world.Value.c0.xyz);
                var emitterRotation = world.Value.Rotation();

                for (int i = 0; i < buffer.Length; i++)
                {
                    var emission = buffer[i];
                    var p = ecb.Instantiate(emitter.ParticlePrefab);

                    var scale = emitterScale * offset.Scale;
                    var size = math.lerp(emitter.StartSize.Min, emitter.StartSize.Max, m_Random.NextFloat());
                    if (size.x == size.y && size.y == size.z)
                    {
                        scale *= size.x;
                    }
                    else
                    {
                        var ptm = math.mul(prefabPtm, float4x4.Scale(size));
                        if (hasPtm)
                            ecb.SetComponent(p, new PostTransformMatrix { Value = ptm });
                        else
                            ecb.AddComponent(p, new PostTransformMatrix { Value = ptm });
                    }

                    var rotation = math.mul(emitterRotation, emission.Rotation);
                    ecb.SetComponent(p, new LocalTransform
                    {
                        Position = math.transform(world.Value, emission.Position) + math.rotate(rotation, offset.Position),
                        Rotation = math.mul(math.mul(rotation, offset.Rotation), quaternion.EulerZXY(m_Random.NextFloat3(emitter.StartRotation.Min, emitter.StartRotation.Max))),
                        Scale = scale
                    });

                    ecb.AddComponent(p, new Newborn { Emitter = entity, EmissionIndex = i });
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
