using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace Partity
{
    /// <summary>
    /// Authoring-time parameters, baked by TrailAuthoring onto the trail-carrying particle.
    /// TTL = StartLifetime x lifetimeMultiplier (Shuriken TrailModule.lifetime semantics),
    /// width = WidthOverTrail x head size (sizeAffectsWidth); also binds the ribbon
    /// Mesh/Material assets (editor-created, never runtime-built).
    /// Named TrailParams (not Trail) because a type name equal to its assembly name trips the
    /// Entities source generator's query-name resolution (SGQC001: the generator resolves the
    /// bare name to the assembly symbol, not this struct).
    /// </summary>
    public struct TrailParams : IComponentData
    {
        public float Lifetime;
        public float MinVertexDistance;
        public float WidthOverTrail;
        public UnityObjectRef<Mesh> Mesh;
        public UnityObjectRef<Material> Material;
    }

    /// <summary>
    /// Mutable recording state, on the same particle entity as <see cref="TrailParams"/> — there
    /// is no render proxy: the particle's own LocalToWorld feeds the instance matrix and its
    /// URPMaterialPropertyBaseColor feeds the ribbon color (bag collects it as an instanced
    /// _BaseColor automatically, alongside _TrailData, when batching the entity).
    /// </summary>
    public struct TrailState : IComponentData
    {
        public int Slot;              // registry row
        public int RingHead;          // ring index of the newest point
        public int LiveCount;         // points within TTL
        public float3 LastPosition;   // odometer anchor (last recorded head position)
        public float TotalLength;     // polyline length of the live points (Tile-mode hook)
        public float WidthScale;      // WidthOverTrail x head size, follows head size per frame
        public float TailCut;         // 尾端 TTL 切点 [0,1]:最老存活点与首个过期点之间的插值分数
    }

    /// <summary>
    /// Per-instance payload consumed by TrailRibbon.shader via the "_TrailData" material property.
    /// x = slot*K + ringHead (texel row of the newest point), y = liveCount,
    /// z = widthScale (ribbon full width in metres), w = tailCut (尾端 TTL 切点分数,CPU 侧按出生时间算好).
    /// </summary>
    [MaterialProperty("_TrailData")]
    public struct TrailRenderData : IComponentData
    {
        public float4 Value;
    }

    /// <summary>
    /// The "initialized" marker and the death shell, in one component. Added at runtime by
    /// <see cref="TrailInitializeSystem"/> when it assigns the slot — a cleanup component
    /// cannot be baked (the entity scene optimization pass strips them all), so the
    /// runtime-add doubles as the spawn detector: fresh particles are
    /// <c>WithAll&lt;TrailParams&gt;().WithNone&lt;TrailCleanup&gt;()</c>. When the particle dies,
    /// destruction strips everything except this, leaving a shell that carries the Slot to
    /// <see cref="TrailCleanupSystem"/>; removing it lets the shell die (dieWithParticles).
    /// </summary>
    public struct TrailCleanup : ICleanupComponentData
    {
        public int Slot;
    }

    /// <summary>
    /// Trails on a particle: attaches to the particle prefab root next to the other authoring.
    /// TTL = StartLifetime x LifetimeMultiplier — Shuriken TrailModule.lifetime is a multiplier
    /// over the particle lifetime, and dieWithParticles comes for free (the trail proxy is reaped
    /// when the head dies). Width = WidthOverTrail x particle size (sizeAffectsWidth, v1 constant).
    /// </summary>
    public class TrailAuthoring : MonoBehaviour
    {
        [Tooltip("Trail lifetime = particle StartLifetime x this (Shuriken TrailModule.lifetime semantics)")]
        public float LifetimeMultiplier = 0.05f;

        [Tooltip("Minimum distance between recorded trail points (TrailRenderer.minVertexDistance)")]
        public float MinVertexDistance = 0.2f;

        [Tooltip("Ribbon width as a fraction of the particle size (TrailModule widthOverTrail)")]
        public float WidthOverTrail = 0.45f;

        [Tooltip("Static ribbon topology (editor-created via Partity/Create Trail Ribbon Assets)")]
        public Mesh Mesh;

        [Tooltip("Ribbon material (editor-created; the history texture binds via MPB at submit time)")]
        public Material Material;

        class Baker : Baker<TrailAuthoring>
        {
            public override void Bake(TrailAuthoring authoring)
            {
                // startLifetime lives on the sibling LifetimeAuthoring; constant mode covers the
                // Projectile-14 profile, other modes approximate with the t=1 evaluation
                var lifetime = GetComponent<LifetimeAuthoring>();
                float startLifetime = lifetime != null ? lifetime.StartLifetime.Evaluate(1f) : 1f;

                DependsOn(authoring.Mesh);
                DependsOn(authoring.Material);

                var entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponent(entity, new TrailParams
                {
                    Lifetime = authoring.LifetimeMultiplier * startLifetime,
                    MinVertexDistance = authoring.MinVertexDistance,
                    WidthOverTrail = authoring.WidthOverTrail,
                    Mesh = authoring.Mesh,
                    Material = authoring.Material,
                });

                // Dormant recording/render state, baked so TrailLifecycleSystem only stamps
                // values here (no structural add for these two). TrailCleanup is NOT baked —
                // cleanup components are stripped by entity scene baking — it is runtime-added
                // by the lifecycle system, where the add doubles as the "initialized" marker.
                AddComponent(entity, new TrailState
                {
                    Slot = -1,
                    RingHead = -1,
                    // sentinel: the first sample records unconditionally (the fresh LTW is not
                    // computed yet, so there is no honest anchor to compare against)
                    LastPosition = new float3(float.MaxValue),
                });
                AddComponent(entity, new TrailRenderData { Value = new float4(-1f, 0f, 0f, 0f) });
            }
        }
    }
}
