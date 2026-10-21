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
            struct neuron_instance
            {
                float3 position;
                float age; // simulated ms since the last spike
                float selected;
            };

            StructuredBuffer<neuron_instance> neuron_buffer;
            float neuron_scale; // diameter in world units
            float glow_time; // simulated ms for a spike's glow to fade to ~37% (see NeuronRenderer.GlowTime)

            // selected somas grow by this much, and the extra margin becomes their ring. somas are only a few pixels wide
            // at a distance, so a ring drawn inside the normal disc would vanish
            static const float selected_scale = 1.6;
            static const float ring_start = 0.55; // squared radius (in -1..1 corner units) where the ring begins

            // two triangles covering the square from (-1, -1) to (1, 1)
            static const float2 corners[6] =
            {
                float2(-1, -1), float2(1, -1), float2(1, 1),
                float2(-1, -1), float2(1, 1), float2(-1, 1)
            };

            struct varyings
            {
                float4 position_hcs : SV_POSITION;
                float2 corner : TEXCOORD0; // -1..1 across the quad, used to cut the circle out
                float glow : TEXCOORD1; // 1 the step a neuron spikes, fading toward 0 after
                float selected : TEXCOORD2;
            };

            varyings vert(uint vertex_id : SV_VertexID, uint instance_id : SV_InstanceID)
            {
                neuron_instance data = neuron_buffer[instance_id];
                float2 corner = corners[vertex_id];

                // view space is the camera's own frame: its x and y axes are always screen right and screen up. so
                // offsetting the corners along x/y there makes the quad face the camera from any angle. the offset is
                // in world units, so perspective still shrinks it with distance exactly like a real sphere
                float3 center_vs = TransformWorldToView(data.position);
                float scale = neuron_scale * lerp(1, selected_scale, data.selected);
                float3 position_vs = center_vs + float3(corner * (scale * 0.5), 0);

                varyings OUT;
                OUT.position_hcs = TransformWViewToHClip(position_vs);
                OUT.corner = corner;
                OUT.glow = exp(-data.age / glow_time);
                OUT.selected = data.selected;
                return OUT;
            }

            half4 frag(varyings IN) : SV_Target
            {
                // everything outside the inscribed circle is thrown away, which turns the square into a disc
                float radius_sq = dot(IN.corner, IN.corner);
                clip(1 - radius_sq);

                if (IN.selected > 0 && radius_sq > ring_start)
                    return half4(1, 1, 1, 1);

                half3 resting_color = half3(0.2, 0.4, 0.9);
                half3 spiked_color = half3(1.0, 0.9, 0.1);
                half3 color = lerp(resting_color, spiked_color, IN.glow);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}