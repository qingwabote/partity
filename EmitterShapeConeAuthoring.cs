using UnityEngine;

namespace Partity
{
    public class EmitterShapeConeAuthoring : MonoBehaviour
    {
        public float Angle = 80f;
        public float Radius = 0.01f;
        public float RadiusThickness = 1f;
        public float Arc = 360f;
        public ParticleArcMode ArcMode = ParticleArcMode.Random;
        public Vector3 Rotation = new Vector3(-90f, 0f, 0f);
        public float RandomPositionAmount = 0.5f;
    }
}
