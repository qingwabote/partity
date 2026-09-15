using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Partity
{
    [WriteGroup(typeof(Emission))]
    public struct ShapeCircle : IComponentData
    {
        public float Radius;
        public float RadiusThickness;
        public float Arc;
        public ParticleArcMode ArcMode;
        public float RandomPositionAmount;
    }

    public class ShapeCircleAuthoring : MonoBehaviour
    {
        public float Radius = 1f;
        public float RadiusThickness = 1f;
        public float Arc = 360f;
        public ParticleArcMode ArcMode = ParticleArcMode.Random;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale = Vector3.one;
        public float RandomPositionAmount;

        class Baker : Baker<ShapeCircleAuthoring>
        {
            public override void Bake(ShapeCircleAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new ShapeCircle
                {
                    ArcMode = authoring.ArcMode,
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
    public partial struct ShapeCircleSystem : ISystem
    {
        private Unity.Mathematics.Random rng;

        public void OnCreate(ref SystemState state)
        {
            rng = new Unity.Mathematics.Random(987654321);
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (emitter, buffer, circle, transform) in
                SystemAPI.Query<RefRW<Emitter>, DynamicBuffer<Emission>, ShapeCircle, ShapeTransform>())
            {
                var e = emitter.ValueRO;
                if (e.Payload <= 0) continue;

                var matrix = float4x4.TRS(transform.Position, transform.Rotation, transform.Scale);

                for (int j = 0; j < e.Payload; j++)
                {
                    CircleEmit(circle.Radius, circle.RadiusThickness,
                        ShapeUtils.GenerateArcAngle(circle.ArcMode, circle.Arc, j, e.Payload, ref rng),
                        ref rng, out float3 pos, out float3 dir);
                    if (circle.RandomPositionAmount > 0f)
                    {
                        var a = circle.RandomPositionAmount;
                        pos += new float3(rng.NextFloat(-a, a), rng.NextFloat(-a, a), rng.NextFloat(-a, a));
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

        static void CircleEmit(float radius, float radiusThickness, float theta, ref Unity.Mathematics.Random rng, out float3 pos, out float3 dir)
        {
            ShapeUtils.RandomPointBetweenCircleAtFixedAngle(out pos, radius * (1f - radiusThickness), radius, theta, ref rng);
            dir = math.normalizesafe(pos);
        }
    }
}
