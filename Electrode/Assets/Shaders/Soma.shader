// minimal unlit URP shader for NeuronRenderer.cs
// position and spike state come from _NeuronBuffer[SV_InstanceID] instead of a nonexistent transform
Shader "Custom/Soma"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // field order and types must match NeuronRenderer.NeuronInstance exactly
            struct NeuronInstance
            {
                float3 position;
                float spike;
            };

            StructuredBuffer<NeuronInstance> _NeuronBuffer;
            float _NeuronScale;

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float spike : TEXCOORD0;
            };

            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                NeuronInstance data = _NeuronBuffer[instanceID];

                // data is a custom struct and not a transform for performance
                float3 positionWS = IN.positionOS * _NeuronScale + data.position;

                Varyings OUT;
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.spike = data.spike;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 restingColor = half3(0.2, 0.4, 0.9);
                half3 spikedColor = half3(1.0, 0.9, 0.1);
                half3 color = lerp(restingColor, spikedColor, IN.spike);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
