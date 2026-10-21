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
            struct NeuronInstance
            {
                float3 position;
                float age; // simulated ms since the last spike
                float selected; // 1 or 0, read by the selection filter
            };

            // must match SynapseRenderer.SynapseInstance
            struct SynapseInstance
            {
                int pre;
                int post;
                float weight;
            };

            StructuredBuffer<NeuronInstance> _NeuronBuffer;
            StructuredBuffer<SynapseInstance> _SynapseBuffer;

            float _SomaRadius;
            float _WidthPerWeight;
            float _MinWidth;
            float _MaxWidth;
            float _HeadLength;
            float _HeadWidthScale;
            float _SideOffset;
            float _FadeStart; // distance from the camera where synapses start fading out
            float _FadeEnd; // distance from the camera where they're gone
            float _GlowTime; // same as Soma.shader's, so a synapse fades with its source neuron
            float _SelectionOnly; // 1 hides synapses with neither end selected (SynapseRenderer.Filter)

            half4 _RestColor;
            half4 _ActiveColor;

            // (t, s): t runs 0 -> 1 along the piece, s runs -1 -> 1 across it
            static const float2 Shaft[6] =
            {
                float2(0, -1), float2(1, -1), float2(1, 1),
                float2(0, -1), float2(1, 1), float2(0, 1)
            };

            static const float2 Head[3] =
            {
                float2(0, -1), float2(0, 1), float2(1, 0)
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
            };

            Varyings vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                SynapseInstance synapse = _SynapseBuffer[instanceID];
                NeuronInstance pre = _NeuronBuffer[synapse.pre];
                NeuronInstance post = _NeuronBuffer[synapse.post];

                float3 delta = post.position - pre.position;
                float gap = length(delta); // not named `distance`: that would shadow the distance() function used below
                float3 dir = delta / max(gap, 1e-6);

                // start and end at the soma surfaces rather than their centers, or the head is buried in the target
                float3 start = pre.position + dir * _SomaRadius;
                float3 end = post.position - dir * _SomaRadius;
                float visible = gap - 2 * _SomaRadius;

                float halfWidth = clamp(abs(synapse.weight) * _WidthPerWeight, _MinWidth, _MaxWidth) * 0.5;
                float headHalfWidth = halfWidth * _HeadWidthScale;
                float headLength = min(_HeadLength, max(visible, 0) * 0.5);
                float3 shaftEnd = end - dir * headLength;

                // the width axis is perpendicular to both the synapse and the line of sight, so the arrow always shows
                // its flat side. it collapses only when you look straight down the synapse, where it'd be edge-on anyway
                float3 toCamera = normalize(_WorldSpaceCameraPos - (start + end) * 0.5);
                float3 side = cross(dir, toCamera);
                side *= rsqrt(max(dot(side, side), 1e-12));

                float3 positionWS;

                if (vertexID < 6)
                {
                    float2 c = Shaft[vertexID];
                    positionWS = lerp(start, shaftEnd, c.x) + side * (c.y * halfWidth);
                }
                else
                {
                    float2 c = Head[vertexID - 6];
                    positionWS = lerp(shaftEnd, end, c.x) + side * (c.y * headHalfWidth);
                }

                // every arrow sits slightly to its own right. a reciprocal pair points opposite ways, so `side` flips and
                // the two land on opposite sides automatically: no need to find reciprocal pairs at all
                positionWS += side * _SideOffset;

                // fade per VERTEX, not per synapse, so a long synapse running away from the camera fades gradually
                // along its length (like fog) instead of popping in or out as one piece
                float fade = 1 - smoothstep(_FadeStart, _FadeEnd, distance(positionWS, _WorldSpaceCameraPos));

                // collapse to one point (zero-area triangles, which the rasterizer drops before any pixel work) when
                // there's nothing to see: overlapping somas, or the whole synapse already past the fade distance. "whole"
                // means its nearest point to the camera, not just its ends: a long synapse can pass right by the camera
                // while both ends are far away
                float3 segment = end - start;
                float along = saturate(dot(_WorldSpaceCameraPos - start, segment) / max(dot(segment, segment), 1e-12));
                bool tooFar = distance(start + segment * along, _WorldSpaceCameraPos) > _FadeEnd;

                bool filtered = _SelectionOnly > 0 && pre.selected == 0 && post.selected == 0;

                if (visible <= 0 || tooFar || filtered)
                    positionWS = start;

                Varyings OUT;
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                // lights up when its source neuron fires and fades with it
                OUT.color = lerp(_RestColor, _ActiveColor, exp(-pre.age / _GlowTime));
                OUT.color.a *= fade;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return IN.color;
            }
            ENDHLSL
        }
    }
}