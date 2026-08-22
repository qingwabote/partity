using Unity.Entities;
using UnityEngine;

namespace Partity
{
    public struct Emitter : IComponentData
    {
        public Entity ParticlePrefab;
        public float Size;
        public int Payload;
    }

    public class EmitterAuthoring : MonoBehaviour
    {
        public GameObject ParticlePrefab;
        public float Size = 1f;

        class Baker : Baker<EmitterAuthoring>
        {
            public override void Bake(EmitterAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Emitter
                {
                    ParticlePrefab = GetEntity(authoring.ParticlePrefab, TransformUsageFlags.Dynamic),
                    Size = authoring.Size,
                });
            }
        }
    }
}
