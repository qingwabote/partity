using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    /// <summary>
    /// Named TrailParams (not Trail) because a type name equal to its assembly name trips the
    /// Entities source generator's query-name resolution (SGQC001: the generator resolves the
    /// bare name to the assembly symbol, not this struct).
    /// </summary>
    public struct TrailRenderer : IComponentData
    {
        public static Entity Detach(EntityManager em, Entity source)
        {
            var linger = em.CreateEntity(
                typeof(TrailRenderer), typeof(TrailState), typeof(TrailData),
                typeof(TrailCleanup), typeof(TrailLinger), typeof(LocalToWorld));
            em.SetComponentData(linger, em.GetComponentData<TrailRenderer>(source));
            em.SetComponentData(linger, em.GetComponentData<TrailState>(source));
            em.SetComponentData(linger, em.GetComponentData<TrailData>(source));
            em.SetComponentData(linger, em.GetComponentData<TrailCleanup>(source));
            em.SetComponentData(linger, em.GetComponentData<LocalToWorld>(source));
            if (em.HasComponent<URPMaterialPropertyBaseColor>(source))
                em.AddComponentData(linger, em.GetComponentData<URPMaterialPropertyBaseColor>(source));

            em.RemoveComponent(source, new ComponentTypeSet(
                typeof(TrailRenderer), typeof(TrailState), typeof(TrailData), typeof(TrailCleanup)));
            return linger;
        }

        public float Lifetime;
        public float MinVertexDistance;
        public float WidthOverTrail;
        public UnityObjectRef<Mesh> Mesh;
        public UnityObjectRef<Material> Material;

        /// true: 粒子死亡当帧丝带消失(销毁连头带尾回收);false: 死亡置残窗口内 Detach 成存续壳
        public bool DieWithParticles;
    }

    /// <summary>
    /// No render proxy by design: the trail rides the particle entity whose own LocalToWorld
    /// is the instance matrix and whose BaseColor is the ribbon color.
    /// </summary>
    public struct TrailState : IComponentData
    {
        public int Slot;
        public int RingHead;
        public float3 LastPosition;
        public float TotalLength;     // Tile-mode hook: no reader until then
    }

    /// <summary>
    /// Single truth for the live count / width / tail cut plus the shader's packed head-row
    /// index. bag collects this by raw blit into one Vector4 slot (size-registered, no field
    /// coupling), and C# value types are Sequential by default — declaration order IS the
    /// xyzw channel order. TrailState deliberately does not mirror these fields — parallel
    /// writable copies updated in lockstep are a desync bug class.
    /// </summary>
    [MaterialProperty("_TrailData")]
    public struct TrailData : IComponentData
    {
        public float HeadRow;      // x = slot*K + ringHead, texel row of the newest point
        public float LiveCount;    // y = points within TTL
        public float WidthScale;   // z = ribbon full width in metres
        public float TailCut;      // w = 尾端 TTL 切点分数
    }

    /// <summary>
    /// Cleanup components are stripped by entity scene baking, so this marker cannot be
    /// pre-baked — the runtime add doubles as the "initialized" detector
    /// (<c>WithNone&lt;TrailCleanup&gt;()</c> = fresh). Destruction keeps only cleanup
    /// components, which is what turns it into the death shell carrying the Slot.
    /// </summary>
    public struct TrailCleanup : ICleanupComponentData
    {
        public int Slot;
    }

    /// <summary>
    /// Marks a detached trail shell. The shell carries no Lifetime — the linger-end system is
    /// its only death source, which is also why auto-detach's WithDisabled&lt;Lifetime&gt;
    /// window can never re-capture it.
    /// </summary>
    public struct TrailLinger : IComponentData { }

    /// <summary>
    /// TTL is derived at bake time because Shuriken's TrailModule.lifetime is a multiplier
    /// over the particle lifetime, not seconds.
    /// </summary>
    public class TrailRendererAuthoring : MonoBehaviour
    {
        [Tooltip("Trail lifetime = particle StartLifetime x this (Shuriken TrailModule.lifetime semantics)")]
        public float LifetimeMultiplier = 0.05f;

        [Tooltip("Minimum distance between recorded trail points (TrailRenderer.minVertexDistance)")]
        public float MinVertexDistance = 0.2f;

        [Tooltip("Ribbon width as a fraction of the particle size (TrailModule widthOverTrail)")]
        public float WidthOverTrail = 0.45f;

        [Tooltip("True: the ribbon dies with its particle. False: the trail detaches at death and unwinds on its own (dieWithParticles)")]
        public bool DieWithParticles = true;

        [Tooltip("Static ribbon topology (editor-created via Partity/Create Trail Ribbon Assets)")]
        public Mesh Mesh;

        [Tooltip("Ribbon material (editor-created; the history texture binds via MPB at submit time)")]
        public Material Material;

        class Baker : Baker<TrailRendererAuthoring>
        {
            public override void Bake(TrailRendererAuthoring authoring)
            {
                // startLifetime lives on the sibling LifetimeAuthoring; constant mode covers the
                // Projectile-14 profile, other modes approximate with the t=1 evaluation
                var lifetime = GetComponent<LifetimeAuthoring>();
                float startLifetime = lifetime != null ? lifetime.StartLifetime.Evaluate(1f) : 1f;

                DependsOn(authoring.Mesh);
                DependsOn(authoring.Material);

                var entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponent(entity, new TrailRenderer
                {
                    Lifetime = authoring.LifetimeMultiplier * startLifetime,
                    MinVertexDistance = authoring.MinVertexDistance,
                    WidthOverTrail = authoring.WidthOverTrail,
                    Mesh = authoring.Mesh,
                    Material = authoring.Material,
                    DieWithParticles = authoring.DieWithParticles,
                });

                // Dormant on purpose so initialize only stamps values (no structural add for
                // these two); TrailCleanup stays runtime-only because baking strips cleanup
                // components.
                AddComponent(entity, new TrailState
                {
                    Slot = -1,
                    RingHead = -1,
                    // sentinel: the first sample records unconditionally (the fresh LTW is not
                    // computed yet, so there is no honest anchor to compare against)
                    LastPosition = new float3(float.MaxValue),
                });
                AddComponent(entity, new TrailData { HeadRow = -1f });
            }
        }
    }
}
