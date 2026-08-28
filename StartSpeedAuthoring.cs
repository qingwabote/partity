using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Partity
{
    public struct StartSpeed : IComponentData
    {
        public BlobAssetReference<MinMaxCurveBlob> Curve;
    }

    public struct SpaceScale : IComponentData
    {
        public float Value;
    }

#if UNITY_EDITOR
    public class StartSpeedAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve Speed = new ParticleSystem.MinMaxCurve(1f);

        class Baker : Baker<StartSpeedAuthoring>
        {
            public override void Bake(StartSpeedAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                if (authoring.Speed.mode == ParticleSystemCurveMode.Constant)
                {
                    AddComponent(entity, new Speed { Value = authoring.Speed.constant });
                }
                else
                {
                    AddComponent(entity, new Speed { Value = 0f });
                    AddComponent(entity, new StartSpeed { Curve = authoring.Speed.ToBlob() });
                }
                AddComponent(entity, new Direction { Value = new float3(0f, 0f, 1f) });
                AddComponent(entity, new SpaceScale { Value = 1f });
            }
        }
    }
#endif

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LifetimeLerpSystem))]
    [UpdateBefore(typeof(MovementSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct StartSpeedSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (start, speed, lifetime) in
                SystemAPI.Query<StartSpeed, RefRW<Speed>, Lifetime>()
                    .WithAll<Nudge>())
            {
                speed.ValueRW.Value = start.Curve.Value.Evaluate(0f, lifetime.Lerp);
            }
        }
    }
}
