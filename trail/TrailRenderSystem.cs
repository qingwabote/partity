using Bag;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Partity
{
    /// <summary>
    /// No proxy: the trail rides the particle entity; bag collects LocalToWorld, _TrailData
    /// and _BaseColor off the particle's own archetype, so the ribbon inherits the live
    /// particle color with zero trail-side plumbing.
    ///
    /// _TrailTex is bound on the block directly on purpose: batch is ref readonly, so calling
    /// bag's PropertyTextureBind would write a defensive copy and the texture would never
    /// reach the GPU (the instanced-invisibility root cause). The engine fills
    /// _TrailTex_TexelSize from the texture binding itself.
    ///
    /// A SystemBase because it owns the managed <see cref="TrailRegistry"/> as an instance
    /// field; the registry is created eagerly so producer systems never see null.
    /// </summary>
    [UpdateInGroup(typeof(BatchGroup))]
    [CreateAfter(typeof(BatchGroup))]
    public partial class TrailRenderSystem : SystemBase
    {
        static readonly MaterialPropertyBlock s_Block = new();
        static readonly int s_TrailTex = Shader.PropertyToID("_TrailTex");

        BatchQueue m_Queue;
        TrailRegistry m_Registry;

        /// <summary>This world's trail registry; never null after OnCreate.</summary>
        public TrailRegistry Registry => m_Registry;

        protected override void OnCreate()
        {
            m_Registry = new TrailRegistry();

            // CreateAfter: the group's OnCreate (context allocation) is guaranteed to have run,
            // so the queue cannot capture a null context
            m_Queue = World.GetExistingSystemManaged<BatchGroup>().CreateQueue();
        }

        protected override void OnDestroy()
        {
            m_Queue.Dispose();
            m_Registry.Dispose();
        }

        protected override void OnUpdate()
        {
            EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
            EntityManager.CompleteDependencyBeforeRO<TrailData>();

            var chunks = SystemAPI.QueryBuilder()
                .WithAll<TrailRenderer, TrailData, LocalToWorld>()
                .Build()
                .ToArchetypeChunkArray(Allocator.Temp);
            if (chunks.Length == 0)
            {
                chunks.Dispose();
                return;
            }

            var paramsHandle = SystemAPI.GetComponentTypeHandle<TrailRenderer>(true);
            var renderHandle = SystemAPI.GetComponentTypeHandle<TrailData>(true);
            foreach (var chunk in chunks)
            {
                using var batcher = m_Queue.Auto(chunk);

                var allParams = chunk.GetNativeArray(ref paramsHandle);
                var renders = chunk.GetNativeArray(ref renderHandle);
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (renders[i].LiveCount < 2f)
                        continue; // fewer than 2 points: no polyline to unfold
                    batcher.Add(allParams[i].Material, allParams[i].Mesh, i);
                }
            }
            chunks.Dispose();

            var tex = m_Registry.History.Texture;
            tex.Apply(false);

            var batches = m_Queue.Dump();
            for (int index = 0; index < batches.Length; index++)
            {
                ref readonly var batch = ref batches.ElementAt(index);

                var material = (Material)batch.Material;
                var mesh = (Mesh)batch.Mesh;
                if (material == null || mesh == null)
                    continue;

                s_Block.Clear();
                s_Block.SetTexture(s_TrailTex, tex);
                batch.PropertyToBlock(s_Block);

                var parameters = new RenderParams(material)
                {
                    matProps = s_Block,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false
                };
                Graphics.RenderMeshInstanced(parameters, mesh, 0,
                    batch.LocalToWorlds.AsArray().Reinterpret<Matrix4x4>(), batch.Count);
            }
            batches.Dispose();
        }
    }
}
