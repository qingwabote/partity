using Unity.Entities;
using UnityEngine;

namespace Partity
{
    /// <summary>
    /// Emitter-side override: when this emitter spawns particles, this curve replaces the particle
    /// prefab's <see cref="StartLifetime"/>, adding the component if the prefab has none (see
    /// <see cref="EmitSystem"/>), so per-emitter lifetimes can differ from the particle prefab's own
    /// baked value.
    /// </summary>
    public struct LifetimeOverride : IComponentData
    {
        public MinMaxCurve Curve;
    }

#if UNITY_EDITOR
    public class LifetimeOverrideAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve Curve = new ParticleSystem.MinMaxCurve(1f);

        class Baker : Baker<LifetimeOverrideAuthoring>
        {
            public override void Bake(LifetimeOverrideAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new LifetimeOverride
                {
                    Curve = authoring.Curve
                });
            }
        }
    }
#endif
}
