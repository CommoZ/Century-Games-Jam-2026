Shader "Fluid/MetaballParticle"
{
    Properties
    {
        _Softness ("Softness", Range(0.01, 2.0)) = 1.0
        _Intensity ("Intensity", Range(0.1, 5.0)) = 1.0
        _TailLength ("Tail Length", Range(0.1, 0.8)) = 0.35
        _TailTaper ("Tail Taper", Range(0.01, 0.5)) = 0.08
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Blend One One // Additive blending
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            float _Softness;
            float _Intensity;
            float _TailLength;
            float _TailTaper;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float teardrop = input.color.b; // 0 = circle, 1 = full teardrop

                float2 uv = input.uv;

                // Y axis: 0 = tail (behind), 1 = front (leading edge)
                // X axis: -0.5 to 0.5 centered

                float value;

                if (teardrop > 0.01)
                {
                    // Teardrop / water droplet shape
                    // Shift center forward so the round head is at the front
                    float centerShift = lerp(0.0, _TailLength, teardrop);
                    float2 p = float2(uv.x - 0.5, uv.y - (0.5 + centerShift));

                    // Taper width toward the tail: full width at front, narrow at back
                    float yNorm = saturate((uv.y) / (0.5 + centerShift)); // 0 at tail tip, 1 at center
                    float taper = lerp(_TailTaper, 1.0, yNorm * yNorm);
                    p.x /= max(taper, 0.001);

                    float dist = length(p) * 2.0;
                    float falloff = saturate(1.0 - dist / _Softness);
                    falloff = falloff * falloff;
                    value = falloff * _Intensity;
                }
                else
                {
                    // Normal circle
                    float2 center = uv - 0.5;
                    float dist = length(center) * 2.0;
                    float falloff = saturate(1.0 - dist / _Softness);
                    falloff = falloff * falloff;
                    value = falloff * _Intensity;
                }

                float velocityInfo = input.color.g * value;
                return half4(value, velocityInfo, 0, value);
            }
            ENDHLSL
        }
    }
}
