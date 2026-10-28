// A see-through bubble that glows at its rim: the shield around a combatant, explosion spheres. The edge facing
// away from the viewer is bright and the middle almost clear.
Shader "Bajoterra/Fresnel"
{
    Properties
    {
        _Color ("Colour", Color) = (0.35, 0.75, 1, 1)
        _Base ("Base opacity", Range(0, 1)) = 0.05
        _Rim ("Rim strength", Range(0, 4)) = 1.6
        _Power ("Rim sharpness", Range(0.5, 8)) = 2.4
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Back
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Base;
            float _Rim;
            float _Power;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = _WorldSpaceCameraPos - world;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 v = normalize(i.viewDir);
                float rim = pow(1.0 - saturate(dot(n, v)), _Power);
                fixed4 c = _Color;
                c.a = saturate((_Base + rim * _Rim) * _Color.a);
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
