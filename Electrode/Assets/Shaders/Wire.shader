// minimal unlit URP shader for BoundsPreview.cs
// meant for MeshTopology.Lines meshes: the rasterizer draws each index pair as a 1px line, and this only forwards the
// vertex color so a single mesh can carry several colors (e.g. the axis gizmo)
Shader "Custom/Wire"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct attributes
            {
                float3 position_os : POSITION;
                half4 color : COLOR;
            };

            struct varyings
            {
                float4 position_hcs : SV_POSITION;
                half4 color : COLOR;
            };

            varyings vert(attributes IN)
            {
                varyings OUT;
                OUT.position_hcs = TransformObjectToHClip(IN.position_os);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(varyings IN) : SV_Target
            {
                return IN.color;
            }
            ENDHLSL
        }
    }
}