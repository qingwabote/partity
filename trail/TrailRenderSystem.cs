using Bag;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Partity
{
    /// <summary>
    /// Submits trail-carrying particles through bag batching — the particle entity itself, no
    /// proxy: Batcher.Add registers each particle under its (material, mesh) from TrailParams;
    /// Batcher.Dispose pulls LocalToWorld and every [MaterialProperty] component on the
    /// particle's archetype automatically — _TrailData (ours) and _BaseColor
    /// (URPMaterialPropertyBaseColor, kept current by partity's color systems: the ribbon
    /// inherits the live particle color with zero trail-side plumbing). Per batch,
    /// PropertyToBlock fills the MPB, then _TrailTex is set directly on the block — batch 是
    /// ref readonly,调 bag 的 PropertyTextureBind 会写防御性拷贝,纹理绑不上(实例化隐形半天的
    /// 根因)。纹理尺寸由引擎随 _TrailTex 绑定自动填充 _TrailTex_TexelSize。最后
    /// Graphics.RenderMeshInstanced 单次提交。渲染时机在 BatchGroup(record 已写完当帧数据)。
    ///
    /// A SystemBase because it owns the managed <see cref="TrailRegistry"/> as an instance
    /// field: the render path is the registry's single consumer (the shader samples the
    /// history texture), so it owns the resource's lifecycle — the simulation systems
    /// (initialize/record/cleanup) are producers that write into it, reaching it via
    /// World.GetExistingSystemManaged&lt;TrailRenderSystem&gt;().Registry. The registry is
    /// created eagerly in OnCreate (never null); every world with BatchGroup carries one
    /// 256KB texture for its lifetime.
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
            EntityManager.CompleteDependencyBeforeRO<TrailState>();
            EntityManager.CompleteDependencyBeforeRO<TrailRenderData>();

            var chunks = SystemAPI.QueryBuilder()
                .WithAll<TrailParams, TrailState, TrailRenderData, LocalToWorld>()
                .Build()
                .ToArchetypeChunkArray(Allocator.Temp);
            if (chunks.Length == 0)
            {
                chunks.Dispose();
                return;
            }

            var paramsHandle = SystemAPI.GetComponentTypeHandle<TrailParams>(true);
            var renderHandle = SystemAPI.GetComponentTypeHandle<TrailRenderData>(true);
            foreach (var chunk in chunks)
            {
                using var batcher = m_Queue.Auto(chunk);

                var allParams = chunk.GetNativeArray(ref paramsHandle);
                var renders = chunk.GetNativeArray(ref renderHandle);
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (renders[i].Value.y < 2f)
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
