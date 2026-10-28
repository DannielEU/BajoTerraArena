using System.Collections.Generic;
using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>The canvas every screen lives on, plus the event system that makes buttons clickable.</summary>
    public sealed class UiRoot
    {
        private readonly List<UiScreen> _screens = new List<UiScreen>();

        public UiRoot(Transform parent)
        {
            Canvas = UiKit.CreateCanvas("UI", 10);
            Canvas.transform.SetParent(parent, false);
            UiKit.EnsureEventSystem(parent);
        }

        public Canvas Canvas { get; }

        public RectTransform Transform
        {
            get { return (RectTransform)Canvas.transform; }
        }

        internal void Register(UiScreen screen)
        {
            _screens.Add(screen);
        }

        /// <summary>Advances the fades and animations of every visible screen; call once per frame.</summary>
        public void Tick(float deltaTime)
        {
            for (int i = 0; i < _screens.Count; i++)
            {
                _screens[i].Tick(deltaTime);
            }
        }
    }

    /// <summary>A full-screen layer of the interface that can be shown or hidden, fading in when shown.</summary>
    public abstract class UiScreen
    {
        private const float FadeSeconds = 0.22f;

        private readonly CanvasGroup _group;
        private float _fade = 1f;

        protected UiScreen(UiRoot ui, string name)
        {
            Root = UiKit.Rect(name, ui.Transform);
            UiKit.Stretch(Root);
            _group = Root.gameObject.AddComponent<CanvasGroup>();
            Root.gameObject.SetActive(false);
            ui.Register(this);
        }

        public RectTransform Root { get; }

        public bool Visible
        {
            get { return Root.gameObject.activeSelf; }
        }

        /// <summary>Shows the screen; it fades in unless <paramref name="instant"/> is set.</summary>
        public void Show(bool instant = false)
        {
            bool wasVisible = Visible;
            Root.gameObject.SetActive(true);
            if (!wasVisible)
            {
                _fade = instant ? 1f : 0f;
                _group.alpha = _fade;
            }
            OnShown();
        }

        public void Hide()
        {
            Root.gameObject.SetActive(false);
        }

        internal void Tick(float deltaTime)
        {
            if (!Visible)
            {
                return;
            }
            if (_fade < 1f)
            {
                _fade = Mathf.Min(1f, _fade + deltaTime / FadeSeconds);
                _group.alpha = _fade * _fade * (3f - 2f * _fade);
            }
            OnTick(deltaTime);
        }

        protected virtual void OnShown()
        {
        }

        protected virtual void OnTick(float deltaTime)
        {
        }
    }
}
