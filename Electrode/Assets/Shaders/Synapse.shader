// unlit URP shader for SynapseRenderer.cs
// draws each synapse as a flat arrow (shaft quad + head triangle) that always shows its wide side to the camera. like
// Soma.shader there is no mesh: the 9 vertices of instance i are built from SV_VertexID, synapse i's (pre, post, weight)
// from _SynapseBuffer, and both somas' positions from the same _NeuronBuffer NeuronRenderer fills
Shader "Custom/Synapse"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            // translucent so dense wiring reads as density instead of a solid mat. still depth-tested, so somas in
            // front hide synapses behind them
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // must match NeuronRenderer.NeuronInstance
            struct neuron_instance
            {
                float3 position;
                float age; // simulated ms since the last spike
                float selected; // 1 or 0, read by the selection filter
            };

            // must match SynapseRenderer.SynapseInstance
            struct synapse_instance
            {
                int pre;
                int post;
                float weight;
                float reciprocal; // 1 if post also synapses back onto pre, else 0
            };

            StructuredBuffer<neuron_instance> neuron_buffer;
            StructuredBuffer<synapse_instance> synapse_buffer;

            float soma_radius;
            float width_per_weight;
            float min_width;
            float max_width;
            float head_length;
            float head_width_scale;
            float side_offset; // gap between the two arrows of a reciprocal pair
            float fade_start; // distance from the camera where synapses start fading out
            float fade_end; // distance from the camera where they're gone
            float glow_time; // same as Soma.shader's, so a synapse fades with its source neuron
            float selection_only; // 1 hides synapses with neither end selected (SynapseRenderer.Filter)

            half4 rest_color;
            half4 active_color;

            // (t, s): t runs 0 -> 1 along the piece, s runs -1 -> 1 across it
            static const float2 shaft[6] =
            {
                float2(0, -1), float2(1, -1), float2(1, 1),
                float2(0, -1), float2(1, 1), float2(0, 1)
            };

            static const float2 head[3] =
            {
                float2(0, -1), float2(0, 1), float2(1, 0)
            };

            struct varyings
            {
                float4 position_hcs : SV_POSITION;
                half4 color : COLOR;
            };

            varyings vert(uint vertex_id : SV_VertexID, uint instance_id : SV_InstanceID)
            {
                synapse_instance synapse = synapse_buffer[instance_id];
                neuron_instance pre = neuron_buffer[synapse.pre];
                neuron_instance post = neuron_buffer[synapse.post];

                float3 delta = post.position - pre.position;
                float gap = length(delta); // not named `distance`: that would shadow the distance() function used below
                float3 dir = delta / max(gap, 1e-6);

                // start and end at the soma surfaces rather than their centers, or the head is buried in the target
                float3 start = pre.position + dir * soma_radius;
                float3 end = post.position - dir * soma_radius;
                float visible = gap - 2 * soma_radius;

                float half_width = clamp(abs(synapse.weight) * width_per_weight, min_width, max_width) * 0.5;
                float head_half_width = half_width * head_width_scale;
                float head_len = min(head_length, max(visible, 0) * 0.5);
                float3 shaft_end = end - dir * head_len;

                // the width axis is perpendicular to both the synapse and the line of sight, so the arrow always shows
                // its flat side. it collapses only when you look straight down the synapse, where it'd be edge-on anyway
                float3 to_camera = normalize(_WorldSpaceCameraPos - (start + end) * 0.5);
                float3 side = cross(dir, to_camera);
                side *= rsqrt(max(dot(side, side), 1e-12));

                float3 position_ws;

                if (vertex_id < 6)
                {
                    float2 c = shaft[vertex_id];
                    position_ws = lerp(start, shaft_end, c.x) + side * (c.y * half_width);
                }
                else
                {
                    float2 c = head[vertex_id - 6];
                    position_ws = lerp(shaft_end, end, c.x) + side * (c.y * head_half_width);
                }

                // only reciprocal pairs split apart, each arrow to its own right. the two point opposite ways, so `side`
                // flips and they land on opposite sides. half_width + half the gap puts their inner edges exactly
                // side_offset apart at any weight. lone synapses stay centered on the line between their somas
                position_ws += side * (synapse.reciprocal * (half_width + side_offset * 0.5));

                // fade per VERTEX, not per synapse, so a long synapse running away from the camera fades gradually
                // along its length (like fog) instead of popping in or out as one piece
                float fade = 1 - smoothstep(fade_start, fade_end, distance(position_ws, _WorldSpaceCameraPos));

                // collapse to one point (zero-area triangles, which the rasterizer drops before any pixel work) when
                // there's nothing to see: overlapping somas, or the whole synapse already past the fade distance. "whole"
                // means its nearest point to the camera, not just its ends: a long synapse can pass right by the camera
                // while both ends are far away
                float3 segment = end - start;
                float along = saturate(dot(_WorldSpaceCameraPos - start, segment) / max(dot(segment, segment), 1e-12));
                bool too_far = distance(start + segment * along, _WorldSpaceCameraPos) > fade_end;

                bool filtered = selection_only > 0 && pre.selected == 0 && post.selected == 0;

                if (visible <= 0 || too_far || filtered)
                    position_ws = start;

                varyings OUT;

                OUT.position_hcs = TransformWorldToHClip(position_ws);

                // lights up when its source neuron fires and fades with it
                OUT.color = lerp(rest_color, active_color, exp(-pre.age / glow_time));
                OUT.color.a *= fade;

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