using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    public enum ParticleArcMode
    {
        Random = 0,
        BurstSpread = 3,
    }

    public struct ShapeCone : IComponentData
    {
        public float Angle;
        public float Radius;
        public float RadiusThickness;
        public float Arc;
        public ParticleArcMode ArcMode;
        public quaternion Rotation;
        public float RandomPositionAmount;
    }

    public class ShapeConeAuthoring : MonoBehaviour
    {
        public float Angle = 80f;
        public float Radius = 0.01f;
        public float RadiusThickness = 1f;
        public float Arc = 360f;
        public ParticleArcMode ArcMode = ParticleArcMode.Random;
        public Vector3 Rotation = new Vector3(-90f, 0f, 0f);
        public float RandomPositionAmount = 0.5f;

        class Baker : Baker<ShapeConeAuthoring>
        {
            public override void Bake(ShapeConeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new ShapeCone
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
    }
    [UpdateInGroup(typeof(ShapeSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ShapeConeSystem : ISystem
    {
        private Unity.Mathematics.Random rng;

        public void OnCreate(ref SystemState state)
        {
            rng = new Unity.Mathematics.Random(123456789);
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var em = state.EntityManager;

            foreach (var (emitter, payload, world, cone) in
                SystemAPI.Query<Emitter, RefRW<EmitterPayload>, LocalToWorld, ShapeCone>())
            {
                int count = payload.ValueRO.Value;
                if (count <= 0) continue;

                var emitterRotation = math.mul(world.Rotation, cone.Rotation);

                var offset = em.GetComponentData<LocalTransform>(emitter.ParticlePrefab);
                for (int j = 0; j < count; j++)
                {
                    var p = ecb.Instantiate(emitter.ParticlePrefab);
                    ConeEmit(cone.Radius, cone.RadiusThickness,
                        GenerateArcAngle(cone.ArcMode, cone.Arc, j, count, ref rng),
                        cone.Angle, ref rng, out float3 pos, out float3 dir);
                    if (cone.RandomPositionAmount > 0f)
                    {
                        pos += new float3(
                            rng.NextFloat(-cone.RandomPositionAmount, cone.RandomPositionAmount),
                            rng.NextFloat(-cone.RandomPositionAmount, cone.RandomPositionAmount),
                            rng.NextFloat(-cone.RandomPositionAmount, cone.RandomPositionAmount));
                    }
                    float3 worldDir = math.rotate(emitterRotation, dir);
                    ecb.SetComponent(p, new LocalTransform
                    {
                        Position = world.Position + math.rotate(emitterRotation, pos),
                        Rotation = math.mul(FromToRotation(new float3(0f, 1f, 0f), worldDir), offset.Rotation),
                        Scale = offset.Scale
                    });
                    ecb.SetComponent(p, new Direction { Value = worldDir });
                }

                payload.ValueRW.Value = 0;
            }
        }

        static void ConeEmit(float radius, float radiusThickness, float theta, float angle, ref Unity.Mathematics.Random rng, out float3 pos, out float3 dir)
        {
            RandomPointBetweenCircleAtFixedAngle(out pos, radius * (1f - radiusThickness), radius, theta, ref rng);
            dir = pos * math.sin(angle);
            dir.z = math.cos(angle) * radius;
            dir = math.normalizesafe(dir);
            pos.z = 0f;
        }

        static void RandomPointBetweenCircleAtFixedAngle(out float3 pos, float minRadius, float maxRadius, float theta, ref Unity.Mathematics.Random rng)
        {
            FixedAngleUnitVector2(out pos, theta);
            pos.z = 0f;
            pos *= minRadius + (maxRadius - minRadius) * rng.NextFloat();
        }

        static void FixedAngleUnitVector2(out float3 v, float theta)
        {
            v = new float3(math.cos(theta), math.sin(theta), 0f);
        }

        static float GenerateArcAngle(ParticleArcMode arcMode, float arc, int particleIndex, int burstCount, ref Unity.Mathematics.Random rng)
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
