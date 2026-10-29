using System;
using UnityEngine;
using UnityEngine.UI;

namespace Bajoterra.Unity
{
    /// <summary>
    /// The options rows (mouse sensitivity, field of view, volumes and a few switches) as a block of widgets that
    /// can sit inside any window: the main menu and the pause menu both use it. It edits a <see cref="GameSettings"/>
    /// directly and raises <see cref="Changed"/> so the game can apply the values (audio volume, camera) at once.
    /// </summary>
    public sealed class SettingsPanel
    {
        private const float RowHeight = 76f;
        private const float DefaultSensitivity = 0.0025f;

        private readonly GameSettings _settings;
        private readonly Text _sensitivityValue;
        private readonly Text _fovValue;
        private readonly Text _volumeValue;
        private readonly Text _ambienceValue;
        private readonly Button _invert;
        private readonly Button _prediction;
        private readonly Button _stats;
        private bool _building = true;

        public event Action Changed;

        public SettingsPanel(RectTransform parent, GameSettings settings, float width)
        {
            _settings = settings;
            Root = UiKit.Rect("Settings", parent);
            UiKit.Place(Root, new Vector2(0.5f, 1f), new Vector2(width, RowHeight * 7f), Vector2.zero);

            int row = 0;
            Slider sensitivity = SliderRow(row++, width, "Sensibilidad del ratón", 0.25f, 3f,
                settings.LookSensitivity / DefaultSensitivity, out _sensitivityValue, value =>
                {
                    _settings.LookSensitivity = value * DefaultSensitivity;
                    _sensitivityValue.text = Mathf.RoundToInt(value * 100f) + "%";
                });
            Slider fov = SliderRow(row++, width, "Campo de visión", 60f, 100f, settings.FieldOfView, out _fovValue,
                value =>
                {
                    _settings.FieldOfView = Mathf.Round(value);
                    _fovValue.text = Mathf.RoundToInt(value) + "°";
                });
            Slider volume = SliderRow(row++, width, "Volumen general", 0f, 1f, settings.MasterVolume, out _volumeValue,
                value =>
                {
                    _settings.MasterVolume = value;
                    _volumeValue.text = Mathf.RoundToInt(value * 100f) + "%";
                });
            Slider ambience = SliderRow(row++, width, "Ambiente de la cueva", 0f, 1f, settings.AmbienceVolume,
                out _ambienceValue, value =>
                {
                    _settings.AmbienceVolume = value;
                    _ambienceValue.text = Mathf.RoundToInt(value * 100f) + "%";
                });
            _invert = SwitchRow(row++, width, "Invertir eje Y", () => _settings.InvertY, v => _settings.InvertY = v);
            _prediction = SwitchRow(row++, width, "Respuesta instantánea al moverte", () => _settings.Prediction,
                v => _settings.Prediction = v);
            _stats = SwitchRow(row, width, "Mostrar FPS en pantalla", () => _settings.ShowStats,
                v => _settings.ShowStats = v);

            // Prime the value labels now that every field exists.
            sensitivity.onValueChanged.Invoke(sensitivity.value);
            fov.onValueChanged.Invoke(fov.value);
            volume.onValueChanged.Invoke(volume.value);
            ambience.onValueChanged.Invoke(ambience.value);
            _building = false;
        }

        public RectTransform Root { get; }

        /// <summary>Re-reads the settings object (after something else changed it).</summary>
        public void Refresh()
        {
            RefreshSwitch(_invert, _settings.InvertY);
            RefreshSwitch(_prediction, _settings.Prediction);
            RefreshSwitch(_stats, _settings.ShowStats);
        }

        private Slider SliderRow(int row, float width, string label, float min, float max, float value,
            out Text valueLabel, UnityEngine.Events.UnityAction<float> apply)
        {
            float y = -row * RowHeight;
            Text name = UiKit.Label(Root, label + " label", label, 27, TextAnchor.MiddleLeft, UiKit.TextColor);
            UiKit.AddShadow(name, 1.5f);
            UiKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(width * 0.42f, RowHeight), new Vector2(0f, y));
            Slider slider = UiKit.MakeSlider(Root, label, new Vector2(width * 0.42f, 30f), min, max, value, null);
            UiKit.Place((RectTransform)slider.transform, new Vector2(0f, 1f), new Vector2(width * 0.42f, 30f),
                new Vector2(width * 0.44f, y - RowHeight * 0.5f + 15f));
            valueLabel = UiKit.Label(Root, label + " value", string.Empty, 26, TextAnchor.MiddleRight, UiKit.AccentColor);
            UiKit.Place(valueLabel.rectTransform, new Vector2(1f, 1f), new Vector2(width * 0.12f, RowHeight), new Vector2(0f, y));
            UiKit.Strong(valueLabel);
            slider.onValueChanged.AddListener(apply);
            slider.onValueChanged.AddListener(_ => OnChanged());
            return slider;
        }

        private Button SwitchRow(int row, float width, string label, Func<bool> read, Action<bool> write)
        {
            float y = -row * RowHeight;
            Text name = UiKit.Label(Root, label + " label", label, 27, TextAnchor.MiddleLeft, UiKit.TextColor);
            UiKit.AddShadow(name, 1.5f);
            UiKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(width * 0.6f, RowHeight), new Vector2(0f, y));
            Button button = null;
            button = UiKit.MakeButton(Root, label, string.Empty, new Vector2(150f, 52f), () =>
            {
                write(!read());
                RefreshSwitch(button, read());
                OnChanged();
            }, ButtonStyle.Secondary, 24);
            UiKit.Place((RectTransform)button.transform, new Vector2(1f, 1f), new Vector2(150f, 52f),
                new Vector2(0f, y - (RowHeight - 52f) * 0.5f));
            RefreshSwitch(button, read());
            return button;
        }

        private static void RefreshSwitch(Button button, bool on)
        {
            UiKit.SetCaption(button, on ? "Sí" : "No");
            UiKit.Restyle(button, on ? ButtonStyle.Success : ButtonStyle.Secondary);
        }

        private void OnChanged()
        {
            if (!_building)
            {
                Changed?.Invoke();
            }
        }
    }
}
