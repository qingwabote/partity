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

    [WriteGroup(typeof(Emitter))]
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
            foreach (var (emitterRef, world, cone, buffer) in
                SystemAPI.Query<RefRW<Emitter>, LocalToWorld, ShapeCone, DynamicBuffer<Emission>>())
            {
                var emitter = emitterRef.ValueRO;
                if (emitter.Payload <= 0) continue;

                var emitterPosition = world.Value.Translation();
                var emitterRotation = math.mul(world.Value.Rotation(), cone.Rotation);
                var emitterScale = world.Value.Scale().x;

                for (int j = 0; j < emitter.Payload; j++)
                {
                    ConeEmit(cone.Radius, cone.RadiusThickness,
                        GenerateArcAngle(cone.ArcMode, cone.Arc, j, emitter.Payload, ref rng),
                        cone.Angle, ref rng, out float3 pos, out float3 dir);
                    if (cone.RandomPositionAmount > 0f)
                    {
                        pos += rng.NextFloat3Direction() * cone.RandomPositionAmount;
                    }
                    buffer.Add(new Emission
                    {
                        Position = emitterPosition + math.rotate(emitterRotation, pos) * emitterScale,
                        Rotation = math.mul(emitterRotation, quaternion.LookRotationSafe(dir, math.up()))
                    });
                }

                emitterRef.ValueRW.Payload = 0;
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
    }
}
