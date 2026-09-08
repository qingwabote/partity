using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    public struct Paused : IComponentData, IEnableableComponent { }

    public class PausableAuthoring : MonoBehaviour
    {
        class Baker : Baker<PausableAuthoring>
        {
            public override void Bake(PausableAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<Paused>(entity);
                SetComponentEnabled<Paused>(entity, false);
            }
        }
    }
}
