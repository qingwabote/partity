// Trailing/TrailRibbon — 终版:Phong 蓝本 HLSL + MPB 实例化(_TrailData 数组 + _TrailTex)。
// 纹理尺寸由引擎随 _TrailTex 绑定自动填充 _TrailTex_TexelSize(同 graphix _JointMap_TexelSize 路线),
// 修复全集:槽内模回卷(防邻槽泄漏)/头端外插至实时头位/尾端按出生时间精确切 TTL(0.1s 窗防前世残留)。
Shader "Partity/TrailRibbon"
{
    Properties
    {
        _TrailRingK("Trail Ring K", Float) = 24
        _MainTex("Trail Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "ForwardUnlit"

            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nolightprobe

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_TrailTex);
            SAMPLER(sampler_TrailTex);
            float4 _TrailTex_TexelSize; // (1/w,1/h,w,h) 引擎随 _TrailTex 绑定自动填充,不走 Properties/MPB

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex); // 外观贴图(u=沿拖尾,v=横向),材质资产持有

            CBUFFER_START(UnityPerMaterial)
                float _TrailRingK;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(PerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float4, _TrailData)  // (slot*K+head, live, width, tailCut)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)  // 粒子实体的 URPMaterialBaseColor,bag 自动采集
            UNITY_INSTANCING_BUFFER_END(PerInstance)

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID // instance id to frag: _BaseColor read there
                float2 uv : TEXCOORD0;   // (沿拖尾 u, 横向 side)——_MainTex 外观贴图用
            };

            float4 FetchPoint(int linearTexel)
            {
                float w = _TrailTex_TexelSize.z;
                float h = _TrailTex_TexelSize.w;
                float2 uv = float2(fmod(linearTexel + 0.5, w) / w,
                                   fmod(floor(linearTexel / w) + 0.5, h) / h);
                return SAMPLE_TEXTURE2D_LOD(_TrailTex, sampler_TrailTex, uv, 0);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float4 trailData = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _TrailData);
                int baseRow = (int)trailData.x;
                int liveCount = (int)trailData.y;
                float widthScale = trailData.z;

                // uv.x=距头距离(头行=0,见 RibbonMeshFactory);插值参数 u=1-uv.x → u=1 在头行
                float u = 1.0 - input.texcoord.x;
                float side = input.texcoord.y * 2.0 - 1.0;

                float f = u * (liveCount - 1);
                int i0 = (int)floor(f);
                int i1 = min(i0 + 1, liveCount - 1);
                if (i1 == i0) i0 = max(i0 - 1, 0);
                float t = saturate(f - i0);

                // 槽内模回卷:行 = slot*K + ((head-i) mod K)
                int K = (int)_TrailRingK;
                int headRing = baseRow % K;
                int slotBase = baseRow - headRing;
                int r0 = slotBase + ((headRing - (liveCount - 1 - i0)) % K + K) % K;
                int r1 = slotBase + ((headRing - (liveCount - 1 - i1)) % K + K) % K;

                float4 pa = FetchPoint(r0);
                float4 pb = FetchPoint(r1);
                float3 p = lerp(pa.xyz, pb.xyz, t);

                // 头端外插:末段延伸到实例矩阵平移(=实时头位置)
                float3 headPos = UNITY_MATRIX_M._m03_m13_m23;
                p = lerp(p, headPos, saturate(f - (liveCount - 2)));

                // 尾端精确切点:cut 由 record 系统按出生时间算好经 _TrailData.w 传入——shader 无时钟
                {
                    int rx = slotBase + ((headRing - liveCount) % K + K) % K;
                    float4 px = FetchPoint(rx);
                    float3 tip = lerp(pa.xyz, px.xyz, trailData.w);
                    p = lerp(tip, p, saturate(f));
                }

                float3 tangent = pb.xyz - pa.xyz;
                // URP 标准视线(GetWorldSpaceViewDir):正交相机下 camPos-p 不与投影方向对齐,
                // 丝带会侧对视轴整条零宽——正交分支用视轴前向
                float3 viewDir = GetWorldSpaceViewDir(p);
                float3 sideDir = cross(tangent, viewDir);
                float sideLen = length(sideDir);
                sideDir = (sideLen > 1e-5 && length(tangent) > 1e-5)
                    ? (sideDir / sideLen) * side : float3(0, 0, 0);

                float3 wp = p + sideDir * (widthScale * 0.5);
                output.positionCS = TransformWorldToHClip(wp);

                output.uv = input.texcoord;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 贴图 X=沿拖尾(头=左边,峰在~30%),Y=横截面(双峰对称);无 alpha,渐隐靠亮度×加色
                UNITY_SETUP_INSTANCE_ID(input);
                half4 baseColor = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _BaseColor);
                baseColor.rgb *= baseColor.a;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * baseColor;
            }
            ENDHLSL
        }
    }
}
