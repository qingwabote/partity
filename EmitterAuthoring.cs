using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    public struct Rotation
    {
        public float3 Min;
        public float3 Max;
    }

    public struct Emitter : IComponentData
    {
        public Entity ParticlePrefab;
        public Rotation Rotation;
        public float Size;
        public int Payload;
    }

    public class EmitterAuthoring : MonoBehaviour
    {
        public GameObject ParticlePrefab;

        [Header("Rotation")]
        // 平铺三个 MinMaxCurve，勿包进 [Serializable] struct：MinMaxCurvePropertyDrawer 的
        // AttachToPanelEvent 闭包缓存 SerializedProperty，嵌套 struct 在 Inspector 重建时触发
        // "SerializedObject of SerializedProperty has been Disposed" NRE（Unity 6000.0 已实测复现）
        public ParticleSystem.MinMaxCurve RotationX;
        public ParticleSystem.MinMaxCurve RotationY;
        public ParticleSystem.MinMaxCurve RotationZ;
        [Space]
        public float Size = 1f;

        class Baker : Baker<EmitterAuthoring>
        {
            public override void Bake(EmitterAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Emitter
                {
                    ParticlePrefab = GetEntity(authoring.ParticlePrefab, TransformUsageFlags.Dynamic),
                    Size = authoring.Size,
                    Rotation = new Rotation
                    {
                        Min = math.radians(new float3(
                            authoring.RotationX.Evaluate(0f, 0f),
                            authoring.RotationY.Evaluate(0f, 0f),
                            authoring.RotationZ.Evaluate(0f, 0f))),
                        Max = math.radians(new float3(
                            authoring.RotationX.Evaluate(0f, 1f),
                            authoring.RotationY.Evaluate(0f, 1f),
                            authoring.RotationZ.Evaluate(0f, 1f))),
                    },
                });
            }
        }
    }

    [UpdateInGroup(typeof(ShapeSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ShapePointSystem : ISystem
    {
        private Unity.Mathematics.Random rng;

        public void OnCreate(ref SystemState state)
        {
            rng = new Unity.Mathematics.Random(987654321);
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var em = state.EntityManager;

            foreach (var (emitterRef, world) in
                SystemAPI.Query<RefRW<Emitter>, LocalToWorld>()
                    .WithOptions(EntityQueryOptions.FilterWriteGroup))
            {
                var emitter = emitterRef.ValueRO;
                if (emitter.Payload <= 0) continue;

                var particlePrefab = emitter.ParticlePrefab;
                var offset = em.GetComponentData<LocalTransform>(particlePrefab);
                var position = world.Value.Translation() + offset.Position;
                var scale = offset.Scale * emitter.Size;

                for (int i = 0; i < emitter.Payload; i++)
                {
                    var rotation = math.mul(offset.Rotation, quaternion.EulerZXY(rng.NextFloat3(emitter.Rotation.Min, emitter.Rotation.Max)));
                    var p = ecb.Instantiate(particlePrefab);
                    ecb.SetComponent(p, LocalTransform.FromPositionRotationScale(position, rotation, scale));
                }

                emitterRef.ValueRW.Payload = 0;
            }
        }
    }
}
