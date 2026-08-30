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

    public struct Emitter : IComponentData, IEnableableComponent
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
                AddBuffer<Emission>(entity);
            }
        }
    }

    [UpdateInGroup(typeof(ShapeSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ShapePointSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (emitterRef, world, buffer) in
                SystemAPI.Query<RefRW<Emitter>, LocalToWorld, DynamicBuffer<Emission>>()
                    .WithOptions(EntityQueryOptions.FilterWriteGroup))
            {
                var emitter = emitterRef.ValueRO;
                if (emitter.Payload <= 0) continue;

                for (int i = 0; i < emitter.Payload; i++)
                {
                    buffer.Add(new Emission
                    {
                        Position = world.Value.Translation(),
                        Rotation = quaternion.identity
                    });
                }

                emitterRef.ValueRW.Payload = 0;
            }
        }
    }
}
