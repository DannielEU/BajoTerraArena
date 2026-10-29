using UnityEngine;
using UnityEngine.Rendering;

namespace Bajoterra.Unity
{
    /// <summary>
    /// The look of the cavern in one place: the deep teal haze and blue-green light of the show's caverns, the soft
    /// "skylight" and the quality settings that make shadows and a few coloured lights affordable. The menu backdrop
    /// and the arena share it.
    /// </summary>
    public static class Atmosphere
    {
        public static readonly Color FogColor = new Color(0.02f, 0.085f, 0.1f);

        /// <summary>Applies the cavern look to the scene and the camera.</summary>
        public static void Apply(Camera camera, Light sun)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = 0.0065f;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.33f, 0.58f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.22f, 0.38f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.11f, 0.18f, 0.16f);
            RenderSettings.reflectionIntensity = 0.25f;

            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = FogColor;
            }
            if (sun != null)
            {
                sun.type = LightType.Directional;
                sun.color = new Color(0.78f, 0.96f, 0.93f);
                sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.85f;
                sun.shadowBias = 0.05f;
                sun.shadowNormalBias = 0.4f;
                sun.transform.rotation = Quaternion.Euler(58f, -32f, 0f);
            }

            QualitySettings.shadowDistance = 75f;
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.pixelLightCount = 6;
            QualitySettings.antiAliasing = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
        }
    }
}
