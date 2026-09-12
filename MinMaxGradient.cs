using Graphix;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Partity
{
    public enum GradientRangeMode
    {
        Color = 0,
        Gradient = 1,
        TwoColors = 2,
        TwoGradients = 3,
        RandomColor = 4,
    }

    public struct GradientSampler
    {
        public Sampler Color;
        public Sampler Alpha;

        public float4 Float(float time)
        {
            return new float4(Color.Vec3(time), Alpha.Float(time));
        }
    }

    public struct MinMaxGradientSampler
    {
        public GradientSampler Min;
        public GradientSampler Max;

        public float4 Float(float time, float lerp)
        {
            return math.lerp(Min.Float(time), Max.Float(time), lerp);
        }
    }

    public struct MinMaxGradient : IComponentData
    {
        public GradientRangeMode Mode;
        public float4 ColorMin;
        public float4 ColorMax;
        public BlobAssetReference<MinMaxGradientSampler> Sampler;

        public float4 Evaluate(float t, float lerpFactor)
        {
            switch (Mode)
            {
                case GradientRangeMode.Color: return ColorMax;
                case GradientRangeMode.TwoColors: return math.lerp(ColorMin, ColorMax, lerpFactor);
                case GradientRangeMode.TwoGradients: return Sampler.Value.Float(t, lerpFactor);
                case GradientRangeMode.RandomColor: return Sampler.Value.Max.Float(lerpFactor);
                default: return Sampler.Value.Max.Float(t);
            }
        }

#if UNITY_EDITOR
        public static implicit operator MinMaxGradient(ParticleSystem.MinMaxGradient mmg)
        {
            return new MinMaxGradient
            {
                Mode = (GradientRangeMode)(int)mmg.mode,
                ColorMin = new float4(mmg.colorMin.r, mmg.colorMin.g, mmg.colorMin.b, mmg.colorMin.a),
                ColorMax = new float4(mmg.colorMax.r, mmg.colorMax.g, mmg.colorMax.b, mmg.colorMax.a),
                Sampler = ToSamplerBlob(mmg),
            };
        }

        static BlobAssetReference<MinMaxGradientSampler> ToSamplerBlob(ParticleSystem.MinMaxGradient mmg)
        {
            var mode = (GradientRangeMode)(int)mmg.mode;
            if (mode != GradientRangeMode.Gradient && mode != GradientRangeMode.TwoGradients && mode != GradientRangeMode.RandomColor)
            {
                return default;
            }
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<MinMaxGradientSampler>();
            BakeGradient(mmg.gradientMax, builder, ref root.Max);
            if (mode == GradientRangeMode.TwoGradients)
            {
                BakeGradient(mmg.gradientMin, builder, ref root.Min);
            }
            return builder.CreateBlobAssetReference<MinMaxGradientSampler>(Allocator.Persistent);
        }

        static void BakeGradient(Gradient gradient, BlobBuilder builder, ref GradientSampler sampler)
        {
            int colorCount = gradient == null ? 0 : gradient.colorKeys.Length;
            int alphaCount = gradient == null ? 0 : gradient.alphaKeys.Length;

            if (colorCount > 0)
            {
                var times = builder.Allocate(ref sampler.Color.Times, colorCount);
                var values = builder.Allocate(ref sampler.Color.Values, colorCount * 3);
                for (int i = 0; i < colorCount; i++)
                {
                    var key = gradient.colorKeys[i];
                    times[i] = key.time;
                    var color = (Color)key.color;
                    values[i * 3] = color.r;
                    values[i * 3 + 1] = color.g;
                    values[i * 3 + 2] = color.b;
                }
            }

            if (alphaCount > 0)
            {
                var times = builder.Allocate(ref sampler.Alpha.Times, alphaCount);
                var values = builder.Allocate(ref sampler.Alpha.Values, alphaCount);
                for (int i = 0; i < alphaCount; i++)
                {
                    times[i] = gradient.alphaKeys[i].time;
                    values[i] = gradient.alphaKeys[i].alpha;
                }
            }
        }
#endif
    }
}
