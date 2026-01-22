Shader "Custom/Sprite_SlicedCircle"
{
 Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // Cut params (world space)
        _CutCenter ("Cut Center (world)", Vector) = (0,0,0,0)
        _CutNormal ("Cut Normal (world)", Vector) = (1,0,0,0)   // normalized
        _HitPoint  ("Hit Point (world)", Vector) = (0,0,0,0)

        // Animation
        _Progress ("Progress", Range(0,1)) = 0
        _Separation ("Separation", Range(0,0.5)) = 0.08

        // Edge visuals
        _EdgeWidth ("Edge Width (world)", Range(0,0.5)) = 0.05
        _EdgeGlow  ("Edge Glow", Range(0,5)) = 1.5
        _EdgeColor ("Edge Color", Color) = (1,1,1,1)

        // Which side this renderer shows: +1 or -1
        _Side ("Side", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex   : POSITION;
                float2 uv       : TEXCOORD0;
                float4 color    : COLOR;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float4 color    : COLOR;
                float3 worldPos : TEXCOORD1;
                float3 baseWorldPos : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;

            float4 _CutCenter;
            float4 _CutNormal;
            float4 _HitPoint;

            float _Progress;
            float _Separation;

            float _EdgeWidth;
            float _EdgeGlow;
            float4 _EdgeColor;

            float _Side;

            v2f vert (appdata v)
            {
                v2f o;
                float4 world = mul(unity_ObjectToWorld, v.vertex);
                float3 baseWorld = world.xyz;

                // Push halves apart along cut normal, based on which side this renderer is.
                float3 n = normalize(_CutNormal.xyz);
                world.xyz = baseWorld + n * (_Separation * _Side);

                o.baseWorldPos = baseWorld;
                o.worldPos = world.xyz;
                o.vertex = mul(UNITY_MATRIX_VP, world);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * i.color;

                // Sprite alpha clip early
                if (tex.a <= 0.001) discard;

                float3 basePos = i.baseWorldPos;
                float3 n = normalize(_CutNormal.xyz);
                float3 center = _CutCenter.xyz;
                float3 hp = _HitPoint.xyz;

                // Signed distance to the cut line/plane in world space.
                // Positive means one side, negative the other.
                float distToPlane = dot(basePos - center, n);

                // Keep only one side per renderer
                // _Side = +1 keeps dist >= 0, _Side = -1 keeps dist <= 0
                float sideMask = step(0.0, distToPlane * _Side); // 1 if on kept side
                if (sideMask < 0.5) discard;

                // Progress mask: the cut “grows” from the hit point along the tangent direction.
                // We compute distance from hitpoint in the cut-perpendicular axis (tangent).
                // Tangent is perpendicular to normal in XY.
                float2 nn = normalize(n.xy);
                float2 tangent = float2(-nn.y, nn.x);

                float tDist = abs(dot((basePos.xy - hp.xy), tangent));
                // At progress 0, almost nothing visible; at progress 1, fully visible.
                float revealRadius = lerp(0.01, 3.0, _Progress); // 3 world units is plenty for a player sprite
                float revealMask = step(tDist, revealRadius);
                if (revealMask < 0.5) discard;

                // Edge glow: based on absolute distance to plane
                float edge = 1.0 - saturate(abs(distToPlane) / max(_EdgeWidth, 1e-4));
                float glow = pow(edge, 2.0) * _EdgeGlow;

                // Additive-ish edge on top of sprite
                fixed4 outCol = tex;
                outCol.rgb += _EdgeColor.rgb * glow * tex.a;

                return outCol;
            }
            ENDCG
        }
    }}
