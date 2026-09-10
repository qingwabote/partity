using Unity.Mathematics;

namespace Partity
{
    internal static class ShapeUtils
    {
        public static void RandomPointBetweenCircleAtFixedAngle(out float3 pos, float minRadius, float maxRadius, float theta, ref Unity.Mathematics.Random rng)
        {
            FixedAngleUnitVector2(out pos, theta);
            pos.z = 0f;
            pos *= minRadius + (maxRadius - minRadius) * rng.NextFloat();
        }

        public static void FixedAngleUnitVector2(out float3 v, float theta)
        {
            v = new float3(math.cos(theta), math.sin(theta), 0f);
        }

        public static float GenerateArcAngle(ParticleArcMode arcMode, float arc, int particleIndex, int burstCount, ref Unity.Mathematics.Random rng)
        {
            if (arcMode == ParticleArcMode.BurstSpread)
            {
                return Repeat(particleIndex * arc / burstCount, arc);
            }
            return rng.NextFloat(0f, arc);
        }

        public static float Repeat(float t, float length)
        {
            return t - math.floor(t / length) * length;
        }
    }
}
