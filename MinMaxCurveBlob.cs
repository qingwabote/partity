using Graphix;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Partity
{
    public enum CurveMode
    {
        Constant = 0,
        Curve = 1,
        TwoCurves = 2,
        TwoConstants = 3,
    }

    public struct MinMaxCurveBlob
    {
        public CurveMode Mode;
        public float ConstantMin;
        public float ConstantMax;
        public Sampler Max;
        public Sampler Min;

        public float Evaluate(float t, float lerpFactor)
        {
            switch (Mode)
            {
                case CurveMode.Constant: return ConstantMax;
                case CurveMode.TwoCurves: return math.lerp(Min.Float(t), Max.Float(t), lerpFactor);
                case CurveMode.TwoConstants: return math.lerp(ConstantMin, ConstantMax, lerpFactor);
                default: return Max.Float(t);
            }
        }
    }

#if UNITY_EDITOR
    public static class MinMaxCurveExtensions
    {
        public static BlobAssetReference<MinMaxCurveBlob> ToBlob(this ParticleSystem.MinMaxCurve mmc)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<MinMaxCurveBlob>();
            root.Mode = (CurveMode)(int)mmc.mode;
            root.ConstantMin = mmc.constantMin;
            root.ConstantMax = mmc.constantMax;
            BakeCurve(mmc.curveMax, builder, ref root.Max, mmc.curveMultiplier);
            if (mmc.mode == ParticleSystemCurveMode.TwoCurves)
            {
                BakeCurve(mmc.curveMin, builder, ref root.Min, mmc.curveMultiplier);
            }
            return builder.CreateBlobAssetReference<MinMaxCurveBlob>(Allocator.Persistent);
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
    }
#endif
}
