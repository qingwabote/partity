using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Partity
{
    public enum ParticleArcMode
    {
        Random = 0,
        BurstSpread = 3,
    }

    [WriteGroup(typeof(Emission))]
    public struct ShapeCone : IComponentData
    {
        public float Angle;
        public float Radius;
        public float RadiusThickness;
        public float Arc;
        public ParticleArcMode ArcMode;
        public float RandomPositionAmount;
    }

    public class ShapeConeAuthoring : MonoBehaviour
    {
        public float Angle = 80f;
        public float Radius = 0.01f;
        public float RadiusThickness = 1f;
        public float Arc = 360f;
        public ParticleArcMode ArcMode = ParticleArcMode.Random;
        public Vector3 Position;
        public Vector3 Rotation = new Vector3(-90f, 0f, 0f);
        public Vector3 Scale = Vector3.one;
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
                });
                AddComponent(entity, new ShapeTransform
                {
                    Position = authoring.Position,
                    Rotation = Quaternion.Euler(authoring.Rotation),
                    Scale = authoring.Scale,
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
            foreach (var (emitter, buffer, cone, transform) in
                SystemAPI.Query<RefRW<Emitter>, DynamicBuffer<Emission>, ShapeCone, ShapeTransform>())
            {
                var e = emitter.ValueRO;
                if (e.Payload <= 0) continue;

                var matrix = float4x4.TRS(transform.Position, transform.Rotation, transform.Scale);

                for (int j = 0; j < e.Payload; j++)
                {
                    ConeEmit(cone.Radius, cone.RadiusThickness,
                        ShapeUtils.GenerateArcAngle(cone.ArcMode, cone.Arc, j, e.Payload, ref rng),
                        cone.Angle, ref rng, out float3 pos, out float3 dir);
                    if (cone.RandomPositionAmount > 0f)
                    {
                        pos += rng.NextFloat3Direction() * cone.RandomPositionAmount;
                    }
                    buffer.Add(new Emission
                    {
                        Position = math.transform(matrix, pos),
                        Rotation = math.mul(transform.Rotation, quaternion.LookRotationSafe(dir, math.up()))
                    });
                }

                emitter.ValueRW.Payload = 0;
            }
        }

        static void ConeEmit(float radius, float radiusThickness, float theta, float angle, ref Unity.Mathematics.Random rng, out float3 pos, out float3 dir)
        {
            ShapeUtils.RandomPointBetweenCircleAtFixedAngle(out pos, radius * (1f - radiusThickness), radius, theta, ref rng);
            dir = pos * math.sin(angle);
            dir.z = math.cos(angle) * radius;
            dir = math.normalizesafe(dir);
            pos.z = 0f;
        }
    }
}
