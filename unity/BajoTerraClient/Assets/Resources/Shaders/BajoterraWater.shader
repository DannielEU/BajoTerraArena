// A small pool of shallow cave water. The surface is a grid the game bends with its ripples (WaterView), so it is lit
// like a mirror: crystals, slugs and flashes glint on every ring. On top of that it is see-through in the middle and
// more reflective at grazing angles, shimmers with drifting caustics, glows faintly teal like the cavern and gets a
// band of foam where it meets its rim; outside the circle it fades to nothing.
Shader "Bajoterra/Water"
{
    Properties
    {
        _Color ("Water colour (alpha = opacity looking straight down)", Color) = (0.04, 0.3, 0.34, 0.5)
        _Foam ("Foam colour", Color) = (0.8, 1, 0.96, 1)
        _Glow ("Glow colour", Color) = (0.1, 0.55, 0.5, 1)
        _Radius ("Radius in metres", Float) = 1.5
        _Smoothness ("Smoothness", Range(0, 1)) = 0.96
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert
        #pragma target 3.0

        fixed4 _Color;
        fixed4 _Foam;
        fixed4 _Glow;
        float _Radius;
        half _Smoothness;

        struct Input
        {
            float3 local;
            float3 worldPos;
            float3 viewDir;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.local = v.vertex.xyz;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float d = length(IN.local.xz);
            float inside = saturate((_Radius - d) / 0.1);
            float t = _Time.y;
            float2 p = IN.worldPos.xz * 3.3;
            float caustic = sin(p.x * 1.1 + t * 1.3) * sin(p.y * 1.3 - t * 1.05) + sin((p.x + p.y) * 0.8 + t * 0.85);
            caustic = saturate(caustic * 0.4 + 0.15);
            float foam = saturate(1.0 - abs(d - (_Radius - 0.1)) / 0.07);
            foam *= 0.55 + 0.45 * sin(atan2(IN.local.z, IN.local.x) * 9.0 + t * 1.7);
            float grazing = pow(1.0 - saturate(normalize(IN.viewDir).y), 3.0);
            o.Albedo = lerp(_Color.rgb, _Foam.rgb, foam * 0.7);
            o.Metallic = 0;
            o.Smoothness = _Smoothness;
            o.Emission = _Glow.rgb * (0.12 + caustic * 0.3) + _Foam.rgb * foam * 0.12;
            o.Alpha = saturate(_Color.a + grazing * 0.45 + foam * 0.5) * inside;
        }
        ENDCG
    }
    Fallback Off
}
