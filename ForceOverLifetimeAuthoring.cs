using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    public struct ForceOverLifetime : IComponentData
    {
        public MinMaxCurve X;
        public MinMaxCurve Y;
        public MinMaxCurve Z;
    }

#if UNITY_EDITOR
    public class ForceOverLifetimeAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve X;
        public ParticleSystem.MinMaxCurve Y;
        public ParticleSystem.MinMaxCurve Z;

        class Baker : Baker<ForceOverLifetimeAuthoring>
        {
            public override void Bake(ForceOverLifetimeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new ForceOverLifetime
                {
                    X = authoring.X,
                    Y = authoring.Y,
                    Z = authoring.Z,
                });
            }
        }
    }
#endif

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LifetimeStepSystem))]
    [UpdateBefore(typeof(MovementSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ForceOverLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            foreach (var (speed, direction, force, lifetime) in
                SystemAPI.Query<RefRW<Speed>, RefRW<Direction>, ForceOverLifetime, Lifetime>())
            {
                var dir = direction.ValueRO.Value;
                var spd = speed.ValueRO.Value;

                var t = lifetime.Time / lifetime.Life;
                var f = new float3(
                    force.X.Evaluate(t, lifetime.Lerp),
                    force.Y.Evaluate(t, lifetime.Lerp),
                    force.Z.Evaluate(t, lifetime.Lerp));
                var v = dir * spd + f * dt;

                speed.ValueRW.Value = math.length(v);
                direction.ValueRW.Value = math.normalizesafe(v, dir);
            }
        }
    }
}
