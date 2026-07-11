Shader "StoryBricks/EquirectangularInside"
{
    Properties
    {
        _MainTex ("Equirectangular", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Cull Front
        ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // 从球心指向顶点：用于 equirectangular 采样
                o.dir = normalize(v.vertex.xyz);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float longitude = atan2(d.x, d.z);
                float latitude = asin(clamp(d.y, -1.0, 1.0));
                float u = longitude / (2.0 * UNITY_PI) + 0.5;
                float v = latitude / UNITY_PI + 0.5;
                return tex2D(_MainTex, float2(u, v));
            }
            ENDCG
        }
    }
    FallBack "Unlit/Texture"
}
