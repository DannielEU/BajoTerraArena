using UnityEngine;
using UnityEngine.Rendering;

namespace Bajoterra.Unity
{
    /// <summary>
    /// The look of the cavern in one place: fog, ambient light, the soft "skylight" and the quality settings that
    /// make shadows and a few coloured lights affordable. The menu backdrop and the arena share it.
    /// </summary>
    public static class Atmosphere
    {
        public static readonly Color FogColor = new Color(0.035f, 0.045f, 0.075f);

        /// <summary>Applies the cavern look to the scene and the camera.</summary>
        public static void Apply(Camera camera, Light sun)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = 0.0065f;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.40f, 0.47f, 0.64f);
            RenderSettings.ambientEquatorColor = new Color(0.29f, 0.32f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.14f, 0.2f);
            RenderSettings.reflectionIntensity = 0.25f;

            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = FogColor;
            }
            if (sun != null)
            {
                sun.type = LightType.Directional;
                sun.color = new Color(0.72f, 0.82f, 1f);
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
