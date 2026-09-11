using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Partity
{
    [WriteGroup(typeof(Emission))]
    public struct ShapeBox : IComponentData
    {
        public float3 BoxThickness;
        public float RandomPositionAmount;
    }

    public class ShapeBoxAuthoring : MonoBehaviour
    {
        public Vector3 BoxThickness = Vector3.zero;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale = Vector3.one;
        public float RandomPositionAmount;

        class Baker : Baker<ShapeBoxAuthoring>
        {
            public override void Bake(ShapeBoxAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new ShapeBox
                {
                    BoxThickness = authoring.BoxThickness,
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
    public partial struct ShapeBoxSystem : ISystem
    {
        private Unity.Mathematics.Random rng;

        public void OnCreate(ref SystemState state)
        {
            rng = new Unity.Mathematics.Random(192837465);
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (emitter, buffer, box, transform) in
                SystemAPI.Query<RefRW<Emitter>, DynamicBuffer<Emission>, ShapeBox, ShapeTransform>().WithNone<Paused>())
            {
                var e = emitter.ValueRO;
                if (e.Payload <= 0) continue;

                var matrix = float4x4.TRS(transform.Position, transform.Rotation, transform.Scale);

                for (int j = 0; j < e.Payload; j++)
                {
                    BoxEmit(box.BoxThickness, ref rng, out float3 pos);
                    if (box.RandomPositionAmount > 0f)
                    {
                        var a = box.RandomPositionAmount;
                        pos += new float3(rng.NextFloat(-a, a), rng.NextFloat(-a, a), rng.NextFloat(-a, a));
                    }
                    buffer.Add(new Emission
                    {
                        Position = math.transform(matrix, pos),
                        Rotation = transform.Rotation
                    });
                }

                emitter.ValueRW.Payload = 0;
            }
        }

        // Cocos boxEmit：thickness 全 0 = 单位立方体均匀体积采样；否则取壳面（两轴随机 + 一轴
        // 固定 ±0.5，Fisher–Yates 洗牌），thickness > 0 的轴向内扩张后逐轴夹回立方体
        static void BoxEmit(float3 thickness, ref Unity.Mathematics.Random rng, out float3 pos)
        {
            if (math.all(thickness == 0f))
            {
                pos = rng.NextFloat3(-0.5f, 0.5f);
                return;
            }

            float v0 = rng.NextFloat(-0.5f, 0.5f);
            float v1 = rng.NextFloat(-0.5f, 0.5f);
            float v2 = (rng.NextInt(0, 2) * 2 - 1) * 0.5f;

            var k = rng.NextInt(0, 3);
            if (k == 1) { (v0, v1) = (v1, v0); }
            else if (k == 2) { (v0, v2) = (v2, v0); }
            k = rng.NextInt(1, 3);
            if (k == 2) { (v1, v2) = (v2, v1); }

            pos = new float3(v0, v1, v2);
            if (thickness.x > 0f) pos.x = math.clamp(pos.x + 0.5f * rng.NextFloat(-thickness.x, thickness.x), -0.5f, 0.5f);
            if (thickness.y > 0f) pos.y = math.clamp(pos.y + 0.5f * rng.NextFloat(-thickness.y, thickness.y), -0.5f, 0.5f);
            if (thickness.z > 0f) pos.z = math.clamp(pos.z + 0.5f * rng.NextFloat(-thickness.z, thickness.z), -0.5f, 0.5f);
        }
    }
}
