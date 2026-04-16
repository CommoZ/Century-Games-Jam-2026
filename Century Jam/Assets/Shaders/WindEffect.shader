Shader "Fluid/WindEffect"
{
    Properties
    {
        [HDR] _Color ("Streak Color", Color) = (0.6, 0.7, 0.8, 0.4)
        [HDR] _CoreColor ("Core Glow Color", Color) = (0.85, 0.92, 1.0, 0.8)
        _Speed ("Flow Speed", Range(0.5, 20.0)) = 6.0
        _LineCount ("Line Count", Range(2, 16)) = 6
        _LineWidth ("Line Width", Range(0.001, 0.08)) = 0.025
        _CurveAmount ("Curve Amount", Range(0.0, 1.5)) = 0.4
        _CurveFreq ("Curve Frequency", Range(0.5, 6.0)) = 2.0
        _TaperPower ("Taper (thin at ends)", Range(0.5, 4.0)) = 1.5
        _WindDir ("Wind Direction (set by script)", Vector) = (1, 0, 0, 0)
        _WindStrength ("Wind Strength (set by script)", Float) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha
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
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            half4 _Color;
            half4 _CoreColor;
            float _Speed;
            float _LineCount;
            float _LineWidth;
            float _CurveAmount;
            float _CurveFreq;
            float _TaperPower;
            float4 _WindDir;
            float _WindStrength;

            float hash(float n)
            {
                return frac(sin(n) * 43758.5453);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // UV.x = 0 at fan source, 1 at max range
                // UV.y = 0..1 across the cone width (0.5 = center)
                float along = input.uv.x;
                float across = input.uv.y;
                float time = _Time.y;

                // --- Envelope ---
                // Fade in from source
                float sourceFade = smoothstep(0.0, 0.2, along);
                // Fade out at end
                float rangeFade = smoothstep(1.0, 0.55, along);
                // Edge fade at cone sides
                float edgeDist = abs(across - 0.5) * 2.0;
                float edgeFade = smoothstep(1.0, 0.6, edgeDist);
                float envelope = sourceFade * rangeFade * edgeFade;

                // --- Flowing curved lines ---
                float totalAlpha = 0.0;
                float totalCore = 0.0;
                int lineCount = (int)_LineCount;

                for (int i = 0; i < lineCount; i++)
                {
                    // Spread lines evenly across the cone, with random offset
                    float fi = float(i);
                    float rnd = hash(fi * 13.7);
                    float baseY = (fi + 0.5) / float(lineCount); // 0..1 evenly spaced

                    // Each line has a unique speed variation
                    float speedVar = 0.8 + 0.4 * hash(fi * 7.3);

                    // Scroll phase — moves along the wind direction
                    float phase = along * 3.0 - time * _Speed * speedVar + rnd * 6.28;

                    // Curve: sine wave displacement perpendicular to wind
                    float curvePhase = along * _CurveFreq * 3.14159 + fi * 1.7 + time * 0.5;
                    float curve = sin(curvePhase) * _CurveAmount * 0.1;

                    // Final y position of this line at this x
                    float lineY = baseY + curve;

                    // Distance from this pixel to the line
                    float dist = abs(across - lineY);

                    // Line width tapers: thicker in the middle, thinner at source and end
                    float taperMask = pow(sin(saturate(along) * 3.14159), _TaperPower);
                    float width = _LineWidth * taperMask * (0.7 + 0.3 * hash(fi * 3.1));

                    // Soft line with glow falloff
                    float lineMask = smoothstep(width, width * 0.15, dist);

                    // Core brightness (very center of the line)
                    float coreMask = smoothstep(width * 0.4, 0.0, dist);

                    // Flowing alpha — lines appear and disappear as they flow
                    float flowMask = sin(phase) * 0.5 + 0.5;
                    flowMask = smoothstep(0.1, 0.6, flowMask);

                    totalAlpha += lineMask * flowMask;
                    totalCore += coreMask * flowMask;
                }

                totalAlpha = saturate(totalAlpha);
                totalCore = saturate(totalCore);

                // --- Compose ---
                float strengthFactor = saturate(_WindStrength * 200.0);

                // Mix outer glow color with brighter core
                half4 color = lerp(_Color, _CoreColor, totalCore);
                color.a = totalAlpha * envelope * strengthFactor;

                if (color.a < 0.001)
                    discard;

                return color;
            }
            ENDHLSL
        }
    }
}
