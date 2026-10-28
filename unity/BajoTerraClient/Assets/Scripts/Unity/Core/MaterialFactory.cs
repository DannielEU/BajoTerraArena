using System.Collections.Generic;
using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Creates the materials the game needs without depending on a render pipeline or on assets made by hand.
    /// Lit surfaces copy the pipeline's own default material (the one a new cube gets: Standard in the built-in
    /// pipeline, Lit in URP) and set its properties by whichever name it has; unlit things use
    /// <c>Sprites/Default</c>; glows and shields use the two small shaders in <c>Resources/Shaders</c>.
    /// </summary>
    public static class MaterialFactory
    {
        private static readonly Dictionary<(int Kind, int Texture, uint Color, int Extra), Material> Cache =
            new Dictionary<(int Kind, int Texture, uint Color, int Extra), Material>();
        private static Material _litTemplate;
        private static Shader _unlitShader;
        private static Shader _glowShader;
        private static Shader _fresnelShader;
        private static bool _glowSearched;
        private static bool _fresnelSearched;
        private static Material _softGlow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            _litTemplate = null;
            _unlitShader = null;
            _glowShader = null;
            _fresnelShader = null;
            _glowSearched = false;
            _fresnelSearched = false;
            _softGlow = null;
        }

        // ------------------------------------------------------------------------------------------ lit

        /// <summary>A shaded opaque material of the given colour (shared between callers).</summary>
        public static Material Lit(Color color)
        {
            return Surface(null, color, 0.15f, 0f);
        }

        /// <summary>
        /// A shaded opaque material with an optional texture (multiplied by <paramref name="tint"/>), shared between
        /// callers that ask for the same combination.
        /// </summary>
        public static Material Surface(Texture2D texture, Color tint, float smoothness, float metallic)
        {
            var key = (0, texture != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(texture) : 0, Pack(tint),
                Mathf.RoundToInt(smoothness * 100f) * 1000 + Mathf.RoundToInt(metallic * 100f));
            if (Cache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }
            Material material = LitInstance(tint);
            if (texture != null)
            {
                material.mainTexture = texture;
            }
            SetSmoothness(material, smoothness);
            SetMetallic(material, metallic);
            Cache[key] = material;
            return material;
        }

        /// <summary>A shaded material that also shines on its own (crystals, lamps, edge strips).</summary>
        public static Material Emissive(Color color, float intensity, float smoothness = 0.6f)
        {
            var key = (1, 0, Pack(color),
                Mathf.RoundToInt(intensity * 100f) * 1000 + Mathf.RoundToInt(smoothness * 100f));
            if (Cache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }
            Material material = LitInstance(Color.Lerp(color, Color.black, 0.45f));
            SetSmoothness(material, smoothness);
            SetEmission(material, color * intensity);
            Cache[key] = material;
            return material;
        }

        /// <summary>A private lit material, for things whose colour changes at run time.</summary>
        public static Material LitInstance(Color color)
        {
            return new Material(LitTemplate()) { color = color };
        }

        // ------------------------------------------------------------------------------------------ unlit and glow

        /// <summary>A flat colour that ignores lighting and supports transparency (shared between callers).</summary>
        public static Material Unlit(Color color)
        {
            var key = (2, 0, Pack(color), 0);
            if (Cache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }
            var material = new Material(UnlitShader()) { color = color };
            Cache[key] = material;
            return material;
        }

        /// <summary>A private copy of an unlit material, for things whose colour changes every frame.</summary>
        public static Material UnlitInstance(Color color)
        {
            return new Material(UnlitShader()) { color = color };
        }

        /// <summary>
        /// The material for particles and trails: additive, tinted per vertex, with a soft round sprite. The
        /// texture is drawn in code, so no image asset is needed.
        /// </summary>
        public static Material Glow()
        {
            if (_softGlow == null)
            {
                _softGlow = GlowInstance(Textures.SoftDot, 1f);
            }
            return _softGlow;
        }

        /// <summary>An additive material with its own sprite and strength (rings, flares, halos).</summary>
        public static Material GlowInstance(Texture2D sprite, float intensity)
        {
            Shader shader = GlowShader();
            Material material;
            if (shader != null)
            {
                material = new Material(shader);
                material.SetFloat("_Intensity", intensity);
            }
            else
            {
                material = new Material(UnlitShader());
            }
            material.mainTexture = sprite;
            return material;
        }

        /// <summary>A bubble that is clear in the middle and bright at the rim.</summary>
        public static Material Fresnel(Color color, float rim = 1.6f, float power = 2.4f, float baseOpacity = 0.05f)
        {
            Shader shader = FresnelShader();
            Material material;
            if (shader != null)
            {
                material = new Material(shader);
                material.SetFloat("_Rim", rim);
                material.SetFloat("_Power", power);
                material.SetFloat("_Base", baseOpacity);
            }
            else
            {
                material = new Material(UnlitShader());
                color.a *= 0.3f;
            }
            material.color = color;
            return material;
        }

        /// <summary>The four bytes of a colour as one number, for dictionary keys that never allocate.</summary>
        private static uint Pack(Color color)
        {
            Color32 c = color;
            return (uint)(c.r | c.g << 8 | c.b << 16 | c.a << 24);
        }

        // ------------------------------------------------------------------------------------------ properties

        public static void SetSmoothness(Material material, float value)
        {
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", value);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", value);
            }
        }

        public static void SetMetallic(Material material, float value)
        {
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", value);
            }
        }

        public static void SetEmission(Material material, Color color)
        {
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color);
            }
        }

        private static Material LitTemplate()
        {
            if (_litTemplate == null)
            {
                GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _litTemplate = probe.GetComponent<Renderer>().sharedMaterial;
                Object.Destroy(probe);
            }
            return _litTemplate;
        }

        private static Shader UnlitShader()
        {
            if (_unlitShader == null)
            {
                _unlitShader = Shader.Find("Sprites/Default");
            }
            return _unlitShader;
        }

        private static Shader GlowShader()
        {
            if (!_glowSearched)
            {
                _glowSearched = true;
                _glowShader = Resources.Load<Shader>("Shaders/BajoterraGlow");
                if (_glowShader == null)
                {
                    _glowShader = Shader.Find("Bajoterra/Glow");
                }
            }
            return _glowShader;
        }

        private static Shader FresnelShader()
        {
            if (!_fresnelSearched)
            {
                _fresnelSearched = true;
                _fresnelShader = Resources.Load<Shader>("Shaders/BajoterraFresnel");
                if (_fresnelShader == null)
                {
                    _fresnelShader = Shader.Find("Bajoterra/Fresnel");
                }
            }
            return _fresnelShader;
        }
    }
}
