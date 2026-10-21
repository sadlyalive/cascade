// minimal unlit URP shader for NeuronRenderer.cs
// draws each neuron as a camera-facing quad ("billboard") cut into a circle, instead of a sphere mesh. there is no mesh:
// the vertex shader builds the quad's 6 corners from SV_VertexID, and positions it from _NeuronBuffer[SV_InstanceID]
Shader "Custom/Soma"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            // the quad always faces the camera, so there's no back side to cull. turning culling off also means the
            // corner order below doesn't have to match whatever winding the platform calls front-facing
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // field order and types must match NeuronRenderer.NeuronInstance exactly
            struct NeuronInstance
            {
                float3 position;
                float age; // simulated ms since the last spike
                float selected;
            };

            StructuredBuffer<NeuronInstance> _NeuronBuffer;
            float _NeuronScale; // diameter in world units
            float _GlowTime; // simulated ms for a spike's glow to fade to ~37% (see NeuronRenderer.GlowTime)

            // selected somas grow by this much, and the extra margin becomes their ring. somas are only a few pixels wide
            // at a distance, so a ring drawn inside the normal disc would vanish
            static const float SelectedScale = 1.6;
            static const float RingStart = 0.55; // squared radius (in -1..1 corner units) where the ring begins

            // two triangles covering the square from (-1, -1) to (1, 1)
            static const float2 Corners[6] =
            {
                float2(-1, -1), float2(1, -1), float2(1, 1),
                float2(-1, -1), float2(1, 1), float2(-1, 1)
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 corner : TEXCOORD0; // -1..1 across the quad, used to cut the circle out
                float glow : TEXCOORD1; // 1 the step a neuron spikes, fading toward 0 after
                float selected : TEXCOORD2;
            };

            Varyings vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                NeuronInstance data = _NeuronBuffer[instanceID];
                float2 corner = Corners[vertexID];

                // view space is the camera's own frame: its x and y axes are always screen right and screen up. so
                // offsetting the corners along x/y there makes the quad face the camera from any angle. the offset is
                // in world units, so perspective still shrinks it with distance exactly like a real sphere
                float3 centerVS = TransformWorldToView(data.position);
                float scale = _NeuronScale * lerp(1, SelectedScale, data.selected);
                float3 positionVS = centerVS + float3(corner * (scale * 0.5), 0);

                Varyings OUT;
                OUT.positionHCS = TransformWViewToHClip(positionVS);
                OUT.corner = corner;
                OUT.glow = exp(-data.age / _GlowTime);
                OUT.selected = data.selected;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // everything outside the inscribed circle is thrown away, which turns the square into a disc
                float radiusSq = dot(IN.corner, IN.corner);
                clip(1 - radiusSq);

                if (IN.selected > 0 && radiusSq > RingStart)
                    return half4(1, 1, 1, 1);

                half3 restingColor = half3(0.2, 0.4, 0.9);
                half3 spikedColor = half3(1.0, 0.9, 0.1);
                half3 color = lerp(restingColor, spikedColor, IN.glow);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}