using Unity.Entities;
using UnityEngine;

namespace Partity
{
    /// <summary>
    /// Emitter-side override: when this emitter spawns particles, this curve replaces the particle
    /// prefab's <see cref="StartLifetime"/> (see <see cref="EmitSystem"/>), so per-emitter lifetimes
    /// can differ from the particle prefab's own start lifetime module.
    /// </summary>
    public struct StartLifetimeOverride : IComponentData
    {
        public MinMaxCurve Curve;
    }

#if UNITY_EDITOR
    public class StartLifetimeOverrideAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve Curve = new ParticleSystem.MinMaxCurve(1f);

        class Baker : Baker<StartLifetimeOverrideAuthoring>
        {
            public override void Bake(StartLifetimeOverrideAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new StartLifetimeOverride
                {
                    Curve = authoring.Curve
                });
            }
        }
    }
#endif
}
