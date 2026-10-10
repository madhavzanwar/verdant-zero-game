// Panoramic (equirectangular) skybox that blends two skies: smoggy and clean.
// _Blend = 0 shows only the smoggy sky; 1, only the clean one. Controlled by SkyPurification.
// _CleanHorizonClamp hides the clean HDRI's landscape near the horizon.
// Based on Unity's built-in Skybox/Panoramic (latitude-longitude mode, 360°).
Shader "VerdantZero/Skybox/Panoramic Blend"
{
    Properties
    {
        [NoScaleOffset] _SmogTex ("Smoggy sky (HDR)", 2D) = "grey" {}
        _SmogTint ("Smoggy tint", Color) = (0.5, 0.5, 0.5, 0.5)
        [NoScaleOffset] _CleanTex ("Clean sky (HDR)", 2D) = "grey" {}
        _CleanTint ("Clean tint", Color) = (0.5, 0.5, 0.5, 0.5)
        _CleanHorizonClamp ("Clean horizon clamp (degrees)", Range(0, 45)) = 0
        _Blend ("Blend (0 smog, 1 clean)", Range(0, 1)) = 0
        [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1
        _Rotation ("Rotation", Range(0, 360)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            sampler2D _SmogTex;
            half4 _SmogTex_HDR;
            half4 _SmogTint;
            sampler2D _CleanTex;
            half4 _CleanTex_HDR;
            half4 _CleanTint;
            half _CleanHorizonClamp;
            half _Blend;
            half _Exposure;
            float _Rotation;

            float3 RotateAroundYInDegrees(float3 vertex, float degrees)
            {
                float alpha = degrees * UNITY_PI / 180.0;
                float sina, cosa;
                sincos(alpha, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                return float3(mul(m, vertex.xz), vertex.y).xzy;
            }

            float2 ToRadialCoords(float3 coords)
            {
                float3 n = normalize(coords);
                float latitude = acos(n.y);
                float longitude = atan2(n.z, n.x);
                float2 sphereCoords = float2(longitude, latitude) * float2(0.5 / UNITY_PI, 1.0 / UNITY_PI);
                return float2(0.5, 1.0) - sphereCoords;
            }

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 rotated = RotateAroundYInDegrees(v.vertex.xyz, _Rotation);
                o.vertex = UnityObjectToClipPos(rotated);
                o.texcoord = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = ToRadialCoords(i.texcoord);

                half3 smog = DecodeHDR(tex2D(_SmogTex, uv), _SmogTex_HDR) * _SmogTint.rgb;
                // Below _CleanHorizonClamp degrees the band of sky at that height is repeated,
                // to hide the photographed landscape (trees, ground) of an HDRI that is not just sky
                float2 cleanUv = float2(uv.x, max(uv.y, 0.5 + _CleanHorizonClamp / 180.0));
                half3 clean = DecodeHDR(tex2D(_CleanTex, cleanUv), _CleanTex_HDR) * _CleanTint.rgb;

                half3 c = lerp(smog, clean, _Blend) * unity_ColorSpaceDouble.rgb * _Exposure;
                return half4(c, 1);
            }
            ENDCG
        }
    }

    Fallback Off
}
