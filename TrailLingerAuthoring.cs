using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Partity
{
    public struct TrailLinger : IComponentData
    {
        /// <summary>
        /// Fork semantics: the linger entity is an independent snapshot (it gets its own stream
        /// segment at the next rebuild); the source keeps recording untouched, so a live source
        /// renders its trail AND the unwinding fork side by side. Destroying the source is the
        /// caller's ordinary destroy.
        /// </summary>
        public static Entity Detach(EntityManager em, Entity source)
        {
            var linger = em.CreateEntity(
                typeof(TrailRenderer), typeof(TrailState), typeof(TrailData),
                typeof(TrailLingering), typeof(LocalToWorld));
            em.SetComponentData(linger, em.GetComponentData<TrailRenderer>(source));
            em.SetComponentData(linger, em.GetComponentData<TrailState>(source));
            em.SetComponentData(linger, em.GetComponentData<TrailData>(source));
            em.SetComponentData(linger, em.GetComponentData<LocalToWorld>(source));
            if (em.HasComponent<URPMaterialPropertyBaseColor>(source))
                em.AddComponentData(linger, em.GetComponentData<URPMaterialPropertyBaseColor>(source));
            return linger;
        }
    }

    /// <summary>
    /// Marks a detached fork. It carries no Lifetime — the linger-end system is its only
    /// death source, which is also why auto-detach's WithDisabled&lt;Lifetime&gt; window can
    /// never re-capture it.
    /// </summary>
    public struct TrailLingering : IComponentData { }

#if UNITY_EDITOR
    [RequireComponent(typeof(LifetimeAuthoring))]
    public class TrailLingerAuthoring : MonoBehaviour
    {
        class Baker : Baker<TrailLingerAuthoring>
        {
            public override void Bake(TrailLingerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponent<TrailLinger>(entity);
            }
        }
    }
#endif

    /// <summary>
    /// The dieWithParticles=false semantics for lifetime deaths. Death is a two-phase protocol
    /// (LifetimeStep disables the component, LifetimeEndSystem destroys later in the frame), so
    /// <c>WithDisabled&lt;Lifetime&gt;</c> inside that window is the only queryable "dying"
    /// state — this system must live between the two. Placing it after TrailStreamBuilder
    /// forks the freshest rebuilt stream state.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrailUpdateSystem))]
    [UpdateBefore(typeof(LifetimeEndSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TrailLingerSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var dying = SystemAPI.QueryBuilder()
                .WithAll<TrailLinger>()
                .WithDisabled<Lifetime>()
                .Build()
                .ToEntityArray(Allocator.Temp);

            var em = state.EntityManager;
            foreach (var entity in dying)
            {
                TrailLinger.Detach(em, entity);
            }
            dying.Dispose();
        }
    }

    /// <summary>
    /// Linger fork terminus: destroy once the snapshot has fully unwound (fewer than two
    /// stream points = no renderable polyline, matching the render system's cull). The fork
    /// carries no cleanup components — destruction is a plain destroy.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrailUpdateSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TrailLingeringSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            foreach (var (data, entity) in SystemAPI.Query<TrailData>().WithAll<TrailLingering>().WithEntityAccess())
            {
                if (data.Count < 2f)
                {
                    ecb.DestroyEntity(entity);
                }
            }
            ecb.Playback(state.EntityManager);
        }
    }
}
