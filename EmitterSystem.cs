using System;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Partity
{
    [Serializable]
    public struct Burst : IBufferElementData
    {
        public float Time;
        public int Count;
    }

    public struct Emitter : IComponentData
    {
        public Entity ParticlePrefab;
        public float Elapsed;
        public int Burst;
    }

    public enum ParticleArcMode
    {
        Random = 0,
        BurstSpread = 3,
    }

    public struct EmitterShapeCone : IComponentData
    {
        public float Angle;
        public float Radius;
        public float RadiusThickness;
        public float Arc;
        public ParticleArcMode ArcMode;
        public quaternion Rotation;
        public float RandomPositionAmount;
    }

    class EmitterBaker : Baker<EmitterAuthoring>
    {
        public override void Bake(EmitterAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Emitter
            {
                ParticlePrefab = GetEntity(authoring.ParticlePrefab, TransformUsageFlags.Dynamic),
            });
            var buf = AddBuffer<Burst>(entity);
            if (authoring.Bursts != null)
            {
                foreach (var b in authoring.Bursts.OrderBy(b => b.Time))
                {
                    buf.Add(new Burst { Time = b.Time, Count = b.Count });
                }
            }
        }
    }

    class EmitterShapeConeBaker : Baker<EmitterShapeConeAuthoring>
    {
        public override void Bake(EmitterShapeConeAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new EmitterShapeCone
            {
                ArcMode = authoring.ArcMode,
                Angle = math.radians(authoring.Angle),
                Arc = math.radians(authoring.Arc),
                Radius = authoring.Radius,
                RadiusThickness = authoring.RadiusThickness,
                RandomPositionAmount = authoring.RandomPositionAmount,
                Rotation = Quaternion.Euler(authoring.Rotation),
            });
        }
    }

    [RequireMatchingQueriesForUpdate]
    public partial struct EmitterSystem : ISystem
    {
        private Random rng;

        public void OnCreate(ref SystemState state)
        {
            rng = new Random(123456789);
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var dt = SystemAPI.Time.DeltaTime;
            var em = state.EntityManager;

            foreach (var (emitter, transform, bursts, shape) in SystemAPI.Query<RefRW<Emitter>, LocalTransform, DynamicBuffer<Burst>, RefRW<EmitterShapeCone>>())
            {
                var e = emitter.ValueRW;
                var s = shape.ValueRW;
                e.Elapsed += dt;

                var prefabLT = em.GetComponentData<LocalTransform>(e.ParticlePrefab);
                var prefabLife = em.GetComponentData<Lifetime>(e.ParticlePrefab);
                var rotation = math.mul(transform.Rotation, s.Rotation);

                while (e.Burst < bursts.Length && bursts[e.Burst].Time <= e.Elapsed)
                {
                    int count = bursts[e.Burst].Count;
                    for (int j = 0; j < count; j++)
                    {
                        var p = ecb.Instantiate(e.ParticlePrefab);
                        var lt = prefabLT;
                        ConeEmit(s.Radius, s.RadiusThickness, GenerateArcAngle(s.ArcMode, s.Arc, j, count, ref rng), s.Angle, ref rng, out float3 localPos, out float3 localDir);
                        if (s.RandomPositionAmount > 0f)
                        {
                            localPos += new float3(
                                rng.NextFloat(-s.RandomPositionAmount, s.RandomPositionAmount),
                                rng.NextFloat(-s.RandomPositionAmount, s.RandomPositionAmount),
                                rng.NextFloat(-s.RandomPositionAmount, s.RandomPositionAmount));
                        }
                        lt.Position = transform.Position + math.rotate(rotation, localPos);
                        float3 worldDir = math.rotate(rotation, localDir);
                        lt.Rotation = math.mul(FromToRotation(new float3(0f, 1f, 0f), worldDir), prefabLT.Rotation);
                        ecb.SetComponent(p, lt);
                        ecb.SetComponent(p, new Lifetime { Life = prefabLife.Life, Factor = rng.NextFloat() });
                    }
                    e.Burst++;
                }
                emitter.ValueRW = e;
                shape.ValueRW = s;
            }
        }

        static void ConeEmit(float radius, float radiusThickness, float theta, float angle, ref Random rng, out float3 pos, out float3 dir)
        {
            RandomPointBetweenCircleAtFixedAngle(out pos, radius * (1f - radiusThickness), radius, theta, ref rng);
            dir = pos * math.sin(angle);
            dir.z = math.cos(angle) * radius;
            dir = math.normalizesafe(dir);
            pos.z = 0f;
        }

        static void RandomPointBetweenCircleAtFixedAngle(out float3 pos, float minRadius, float maxRadius, float theta, ref Random rng)
        {
            FixedAngleUnitVector2(out pos, theta);
            pos.z = 0f;
            pos *= minRadius + (maxRadius - minRadius) * rng.NextFloat();
        }

        static void FixedAngleUnitVector2(out float3 v, float theta)
        {
            v = new float3(math.cos(theta), math.sin(theta), 0f);
        }

        static float GenerateArcAngle(ParticleArcMode arcMode, float arc, int particleIndex, int burstCount, ref Random rng)
        {
            if (arcMode == ParticleArcMode.BurstSpread)
            {
                return Repeat(particleIndex * arc / burstCount, arc);
            }
            return rng.NextFloat(0f, arc);
        }

        static float Repeat(float t, float length)
        {
            return t - math.floor(t / length) * length;
        }

        static quaternion FromToRotation(float3 from, float3 to)
        {
            float3 f = math.normalizesafe(from);
            float3 t = math.normalizesafe(to);
            if (math.all(t == 0f)) return quaternion.identity;
            float dot = math.clamp(math.dot(f, t), -1f, 1f);
            if (dot > 0.9999f) return quaternion.identity;
            if (dot < -0.9999f) return quaternion.AxisAngle(new float3(1f, 0f, 0f), math.PI);
            return quaternion.AxisAngle(math.normalize(math.cross(f, t)), math.acos(dot));
        }
    }
}
