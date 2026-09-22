using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Partity
{
    public struct Sampler
    {
        public BlobArray<float> Times;
        public BlobArray<float> Values;

        public unsafe float Float(float time)
        {
            return Bastard.Sampler.Float((float*)Times.GetUnsafePtr(), (float*)Values.GetUnsafePtr(), Times.Length, time);
        }

        public unsafe float3 Vec3(float time)
        {
            return Bastard.Sampler.Vec3((float*)Times.GetUnsafePtr(), (float3*)Values.GetUnsafePtr(), Times.Length, time);
        }

        public unsafe quaternion Quat(float time)
        {
            return Bastard.Sampler.Quat((float*)Times.GetUnsafePtr(), (quaternion*)Values.GetUnsafePtr(), Times.Length, time);
        }
    }

    public struct MinMaxSampler
    {
        public Sampler Min;
        public Sampler Max;

        public float Float(float time, float lerp)
        {
            return math.lerp(Min.Float(time), Max.Float(time), lerp);
        }
    }

    public enum CurveMode
    {
        Constant = 0,
        Curve = 1,
        TwoCurves = 2,
        TwoConstants = 3,
    }

    public struct MinMaxCurve : IComponentData
    {
        public CurveMode Mode;
        public float ConstantMin;
        public float ConstantMax;
        public BlobAssetReference<MinMaxSampler> Sampler;

        public float Evaluate(float t, float lerpFactor)
        {
            switch (Mode)
            {
                case CurveMode.Constant: return ConstantMax;
                case CurveMode.TwoCurves: return Sampler.Value.Float(t, lerpFactor);
                case CurveMode.TwoConstants: return math.lerp(ConstantMin, ConstantMax, lerpFactor);
                default: return Sampler.Value.Max.Float(t);
            }
        }

#if UNITY_EDITOR
        public static implicit operator MinMaxCurve(ParticleSystem.MinMaxCurve mmc)
        {
            return new MinMaxCurve
            {
                Mode = (CurveMode)(int)mmc.mode,
                ConstantMin = mmc.constantMin,
                ConstantMax = mmc.constantMax,
                Sampler = ToSamplerBlob(mmc),
            };
        }

        static BlobAssetReference<MinMaxSampler> ToSamplerBlob(ParticleSystem.MinMaxCurve mmc)
        {
            var mode = (CurveMode)(int)mmc.mode;
            if (mode != CurveMode.Curve && mode != CurveMode.TwoCurves)
            {
                return default;
            }
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<MinMaxSampler>();
            BakeCurve(mmc.curveMax, builder, ref root.Max, mmc.curveMultiplier);
            if (mode == CurveMode.TwoCurves)
            {
                BakeCurve(mmc.curveMin, builder, ref root.Min, mmc.curveMultiplier);
            }
            return builder.CreateBlobAssetReference<MinMaxSampler>(Allocator.Persistent);
        }

        static void BakeCurve(AnimationCurve curve, BlobBuilder builder, ref Sampler sampler, float multiplier)
        {
            int n = curve == null ? 0 : curve.length;
            if (n == 0)
            {
                return;
            }
            var times = builder.Allocate(ref sampler.Times, n);
            var values = builder.Allocate(ref sampler.Values, n);
            for (int i = 0; i < n; i++)
            {
                times[i] = curve[i].time;
                values[i] = curve[i].value * multiplier;
            }
        }
#endif
    }
}
