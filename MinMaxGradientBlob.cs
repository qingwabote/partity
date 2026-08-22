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
    }

    public struct MinMaxGradientBlob
    {
        public GradientRangeMode Mode;
        public float4 ColorMin;
        public float4 ColorMax;
        public GradientSampler Max;
        public GradientSampler Min;

        public float4 Evaluate(float t, float lerpFactor)
        {
            switch (Mode)
            {
                case GradientRangeMode.Color: return ColorMax;
                case GradientRangeMode.TwoColors: return math.lerp(ColorMin, ColorMax, lerpFactor);
                case GradientRangeMode.TwoGradients: return math.lerp(Sample(ref Min, t), Sample(ref Max, t), lerpFactor);
                case GradientRangeMode.RandomColor: return Sample(ref Max, lerpFactor);
                default: return Sample(ref Max, t);
            }
        }

        static float4 Sample(ref GradientSampler s, float t) => new float4(s.Color.Vec3(t), s.Alpha.Float(t));
    }

#if UNITY_EDITOR
    public static class MinMaxGradientExtensions
    {
        public static BlobAssetReference<MinMaxGradientBlob> ToBlob(this ParticleSystem.MinMaxGradient mmg)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<MinMaxGradientBlob>();
            root.Mode = (GradientRangeMode)(int)mmg.mode;
            root.ColorMin = new float4(mmg.colorMin.r, mmg.colorMin.g, mmg.colorMin.b, mmg.colorMin.a);
            root.ColorMax = new float4(mmg.colorMax.r, mmg.colorMax.g, mmg.colorMax.b, mmg.colorMax.a);
            BakeGradient(mmg.gradientMax, builder, ref root.Max);
            if (mmg.mode == ParticleSystemGradientMode.TwoGradients)
            {
                BakeGradient(mmg.gradientMin, builder, ref root.Min);
            }
            return builder.CreateBlobAssetReference<MinMaxGradientBlob>(Allocator.Persistent);
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
    }
#endif
}
