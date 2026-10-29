using System;
using UnityEngine;
using UnityEngine.UI;

namespace Bajoterra.Unity
{
    /// <summary>
    /// A dimmed full-screen message with up to three buttons: "connection lost" uses it, and the pause menu is built
    /// on it with a sub-view for the options. The game keeps running behind it.
    /// </summary>
    public sealed class OverlayScreen : UiScreen
    {
        private const float WindowWidth = 760f;

        private readonly Image _window;
        private readonly Text _title;
        private readonly Text _subtitle;
        private readonly Button[] _buttons = new Button[3];
        private readonly Action[] _actions = new Action[3];
        private readonly RectTransform _mainView;
        private readonly RectTransform _optionsView;
        private readonly SettingsPanel _options;

        /// <summary>Raised when an option changed while the pause menu's options are open.</summary>
        public event Action SettingsChanged;

        public OverlayScreen(UiRoot ui, string name, GameSettings settings) : base(ui, name)
        {
            Image dim = UiKit.Panel(Root, "Dim", new Color(0.0f, 0.04f, 0.06f, 0.74f));
            UiKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            _window = UiKit.Window(Root, "Window", UiKit.PanelColor, 24);
            UiKit.Place(_window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(WindowWidth, 640f), Vector2.zero);

            _mainView = UiKit.Rect("Main", _window.transform);
            UiKit.Stretch(_mainView);
            _title = UiKit.Label(_mainView, "Title", string.Empty, 84, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(WindowWidth - 60f, 110f), new Vector2(0f, -34f));
            UiKit.Fire(_title);
            _subtitle = UiKit.Label(_mainView, "Subtitle", string.Empty, 30, TextAnchor.MiddleCenter, UiKit.TextDim);
            UiKit.AddShadow(_subtitle, 1.5f);
            UiKit.Place(_subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(WindowWidth - 80f, 90f), new Vector2(0f, -150f));
            _subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            for (int i = 0; i < _buttons.Length; i++)
            {
                int index = i;
                _buttons[i] = UiKit.MakeButton(_mainView, "Button " + i, string.Empty, new Vector2(560f, 82f),
                    () => _actions[index]?.Invoke(), ButtonStyle.Secondary, 36);
                UiKit.Place((RectTransform)_buttons[i].transform, new Vector2(0.5f, 1f), new Vector2(560f, 82f),
                    new Vector2(0f, -260f - i * 98f));
            }

            _optionsView = UiKit.Rect("Options", _window.transform);
            UiKit.Stretch(_optionsView);
            Text optionsTitle = UiKit.Label(_optionsView, "Title", "AJUSTES", 62, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(optionsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(600f, 80f), new Vector2(0f, -28f));
            UiKit.Fire(optionsTitle);
            _options = new SettingsPanel(_optionsView, settings, 660f);
            UiKit.Place(_options.Root, new Vector2(0.5f, 1f), new Vector2(660f, 76f * 7f), new Vector2(0f, -120f));
            _options.Changed += () => SettingsChanged?.Invoke();
            Button back = UiKit.MakeButton(_optionsView, "Back", "Volver", new Vector2(320f, 70f), ShowMainView,
                ButtonStyle.Primary, 34);
            UiKit.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(320f, 70f), new Vector2(0f, 40f));
            _optionsView.gameObject.SetActive(false);
        }

        /// <summary>Shows a message with one or two buttons (the second is styled as the way out).</summary>
        public void Present(string title, Color titleColor, string subtitle, string primaryCaption,
            Action primaryAction, string secondaryCaption = null, Action secondaryAction = null)
        {
            SetTexts(title, titleColor, subtitle);
            SetButton(0, primaryCaption, primaryAction, ButtonStyle.Primary);
            SetButton(1, secondaryCaption, secondaryAction, ButtonStyle.Secondary);
            SetButton(2, null, null, ButtonStyle.Secondary);
            Resize(secondaryCaption != null ? 2 : 1);
            ShowMainView();
            Show();
        }

        /// <summary>Shows the pause menu: resume, options, and leaving the match.</summary>
        public void PresentPause(Action resume, Action leave)
        {
            SetTexts(UiText.Paused, Color.white, "La partida sigue en marcha mientras estás en pausa.");
            SetButton(0, UiText.Resume, resume, ButtonStyle.Primary);
            SetButton(1, "Ajustes", ShowOptions, ButtonStyle.Secondary);
            SetButton(2, UiText.LeaveMatch, leave, ButtonStyle.Danger);
            Resize(3);
            ShowMainView();
            Show();
        }

        private void SetTexts(string title, Color color, string subtitle)
        {
            _title.text = title;
            if (color == Color.white)
            {
                UiKit.Gradient(_title, UiKit.FireTop, UiKit.FireBottom);
            }
            else
            {
                UiKit.Gradient(_title, UiKit.Lighter(color, 0.45f), UiKit.Darker(color, 0.15f));
            }
            _subtitle.text = subtitle ?? string.Empty;
        }

        private void SetButton(int index, string caption, Action action, ButtonStyle style)
        {
            _buttons[index].gameObject.SetActive(caption != null);
            if (caption == null)
            {
                return;
            }
            UiKit.SetCaption(_buttons[index], caption);
            UiKit.Restyle(_buttons[index], style);
            _actions[index] = action;
        }

        private void Resize(int buttons)
        {
            float height = 270f + buttons * 98f + 20f;
            _window.rectTransform.sizeDelta = new Vector2(WindowWidth, height);
        }

        private void ShowOptions()
        {
            _mainView.gameObject.SetActive(false);
            _optionsView.gameObject.SetActive(true);
            _window.rectTransform.sizeDelta = new Vector2(WindowWidth, 800f);
            _options.Refresh();
        }

        private void ShowMainView()
        {
            _optionsView.gameObject.SetActive(false);
            _mainView.gameObject.SetActive(true);
            int buttons = 0;
            foreach (Button button in _buttons)
            {
                buttons += button.gameObject.activeSelf ? 1 : 0;
            }
            Resize(Mathf.Max(1, buttons));
        }
    }
}
