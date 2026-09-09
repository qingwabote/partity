using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    public struct StartRotation
    {
        public float3 Min;
        public float3 Max;
    }

    public struct Emitter : IComponentData
    {
        public Entity ParticlePrefab;
        public StartRotation StartRotation;
        public float3 StartSize;
        public int Payload;
    }

    public class EmitterAuthoring : MonoBehaviour
    {
        public GameObject ParticlePrefab;

        [Header("Start Rotation")]
        // 平铺三个 MinMaxCurve，勿包进 [Serializable] struct：MinMaxCurvePropertyDrawer 的
        // AttachToPanelEvent 闭包缓存 SerializedProperty，嵌套 struct 在 Inspector 重建时触发
        // "SerializedObject of SerializedProperty has been Disposed" NRE（Unity 6000.0 已实测复现）
        public ParticleSystem.MinMaxCurve StartRotationX;
        public ParticleSystem.MinMaxCurve StartRotationY;
        public ParticleSystem.MinMaxCurve StartRotationZ;
        [Space]
        public Vector3 StartSize = Vector3.one;

        class Baker : Baker<EmitterAuthoring>
        {
            public override void Bake(EmitterAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Emitter
                {
                    ParticlePrefab = GetEntity(authoring.ParticlePrefab, TransformUsageFlags.Dynamic),
                    StartSize = authoring.StartSize,
                    StartRotation = new StartRotation
                    {
                        Min = math.radians(new float3(
                            authoring.StartRotationX.Evaluate(0f, 0f),
                            authoring.StartRotationY.Evaluate(0f, 0f),
                            authoring.StartRotationZ.Evaluate(0f, 0f))),
                        Max = math.radians(new float3(
                            authoring.StartRotationX.Evaluate(0f, 1f),
                            authoring.StartRotationY.Evaluate(0f, 1f),
                            authoring.StartRotationZ.Evaluate(0f, 1f))),
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
            foreach (var (emitter, buffer, world) in
                SystemAPI.Query<RefRW<Emitter>, DynamicBuffer<Emission>, LocalToWorld>()
                    .WithNone<Paused>()
                    .WithOptions(EntityQueryOptions.FilterWriteGroup))
            {
                var e = emitter.ValueRO;
                if (e.Payload <= 0) continue;

                var rotation = math.inverse(world.Value.Rotation());
                for (int i = 0; i < e.Payload; i++)
                {
                    buffer.Add(new Emission
                    {
                        Position = float3.zero,
                        Rotation = rotation
                    });
                }

                emitter.ValueRW.Payload = 0;
            }
        }
    }
}
