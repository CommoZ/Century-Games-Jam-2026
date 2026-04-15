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

        Blend One One
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
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 data : TEXCOORD1;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 data : TEXCOORD1;
            };

            float _Softness;
            float _Intensity;
            float _TailLength;
            float _TailTaper;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.pos = TransformObjectToHClip(input.vertex.xyz);
                output.uv = input.uv;
                output.data = input.data;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float teardrop = input.data.y;
                float speed = input.data.x;

                float2 uv = input.uv;
                float value;

                if (teardrop > 0.01)
                {
                    float centerShift = lerp(0.0, _TailLength, teardrop);
                    float2 p = float2(uv.x - 0.5, uv.y - (0.5 + centerShift));

                    float yNorm = saturate(uv.y / (0.5 + centerShift));
                    float taper = lerp(_TailTaper, 1.0, yNorm * yNorm);
                    p.x /= max(taper, 0.001);

                    float dist = length(p) * 2.0;
                    float falloff = saturate(1.0 - dist / _Softness);
                    falloff = falloff * falloff;
                    value = falloff * _Intensity;
                }
                else
                {
                    float2 center = uv - 0.5;
                    float dist = length(center) * 2.0;
                    float falloff = saturate(1.0 - dist / _Softness);
                    falloff = falloff * falloff;
                    value = falloff * _Intensity;
                }

                return half4(value, value * speed, 0, value);
            }
            ENDHLSL
        }
    }
}
