using UnityEngine;

namespace Partity
{
#if UNITY_EDITOR
    public class SizeOverLifetimeAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve Size = new ParticleSystem.MinMaxCurve(1f);
    }
#endif
}
