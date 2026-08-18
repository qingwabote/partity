using Unity.Entities;
using UnityEngine;

namespace Partity
{
    public struct EmitOnDestroy : IComponentData { }

    public class EmitOnDestroyAuthoring : MonoBehaviour
    {
        class Baker : Baker<EmitOnDestroyAuthoring>
        {
            public override void Bake(EmitOnDestroyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<EmitOnDestroy>(entity);
            }
        }
    }
}
