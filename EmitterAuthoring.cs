using Unity.Entities;
using UnityEngine;

namespace Partity
{
    public struct Emitter : IComponentData
    {
        public Entity ParticlePrefab;
    }

    public struct EmitterPayload : IComponentData
    {
        public int Value;
    }

    public class EmitterAuthoring : MonoBehaviour
    {
        public GameObject ParticlePrefab;

        class Baker : Baker<EmitterAuthoring>
        {
            public override void Bake(EmitterAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Emitter
                {
                    ParticlePrefab = GetEntity(authoring.ParticlePrefab, TransformUsageFlags.Dynamic),
                });
                AddComponent(entity, new EmitterPayload());
            }
        }
    }
}
