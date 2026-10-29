using System;
using Bajoterra.Client.Session;
using UnityEngine;
using UnityEngine.UI;

namespace Bajoterra.Unity
{
    /// <summary>
    /// What the player sees when the match is over: a big VICTORY, DEFEAT or DRAW, the final score in rounds and the
    /// full table of the match (kills, deaths, damage and healing for all six), with the way back to the room or the
    /// menu.
    /// </summary>
    public sealed class ResultScreen : UiScreen
    {
        private readonly Text _title;
        private readonly Text _score;
        private readonly Text _detail;
        private readonly HudScoreboard _table;
        private readonly Button _primary;
        private readonly Button _secondary;
        private Action _primaryAction;
        private Action _secondaryAction;
        private float _age;

        public ResultScreen(UiRoot ui) : base(ui, "Result")
        {
            Image dim = UiKit.Panel(Root, "Dim", new Color(0.0f, 0.04f, 0.06f, 0.86f));
            UiKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            _title = UiKit.Label(Root, "Title", string.Empty, 160, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(1500f, 200f), new Vector2(0f, -24f));
            UiKit.Fire(_title);
            _score = UiKit.Label(Root, "Score", string.Empty, 50, TextAnchor.MiddleCenter, UiKit.TextColor);
            UiKit.Place(_score.rectTransform, new Vector2(0.5f, 1f), new Vector2(1000f, 64f), new Vector2(0f, -222f));
            UiKit.Heading(_score);
            _detail = UiKit.Label(Root, "Detail", string.Empty, 26, TextAnchor.MiddleCenter, UiKit.TextDim);
            UiKit.AddShadow(_detail, 1.5f);
            UiKit.Place(_detail.rectTransform, new Vector2(0.5f, 1f), new Vector2(1000f, 36f), new Vector2(0f, -284f));

            RectTransform tableHolder = UiKit.Rect("TableHolder", Root);
            UiKit.Place(tableHolder, new Vector2(0.5f, 0.5f), new Vector2(1060f, 600f), new Vector2(0f, -78f));
            _table = new HudScoreboard(tableHolder);
            _table.Root.anchorMin = new Vector2(0.5f, 0.5f);
            _table.Root.anchorMax = new Vector2(0.5f, 0.5f);
            _table.Root.pivot = new Vector2(0.5f, 0.5f);
            _table.Root.anchoredPosition = Vector2.zero;

            _primary = UiKit.MakeButton(Root, "Primary", UiText.BackToRoom, new Vector2(440f, 86f), () => _primaryAction?.Invoke(),
                ButtonStyle.Primary, 36);
            UiKit.Place((RectTransform)_primary.transform, new Vector2(0.5f, 0f), new Vector2(440f, 86f), new Vector2(-240f, 44f));
            _secondary = UiKit.MakeButton(Root, "Secondary", "Menú principal", new Vector2(440f, 86f), () => _secondaryAction?.Invoke(),
                ButtonStyle.Secondary, 36);
            UiKit.Place((RectTransform)_secondary.transform, new Vector2(0.5f, 0f), new Vector2(440f, 86f), new Vector2(240f, 44f));
        }

        /// <summary>Shows the result of a finished match.</summary>
        /// <param name="winner">Team that won, or -1 for a draw.</param>
        public void Present(MatchSession match, int winner, int ownWins, int otherWins, Action backToRoom,
            Action backToMenu)
        {
            string title;
            Color top;
            Color bottom;
            if (winner < 0)
            {
                title = UiText.Draw;
                top = new Color(0.9f, 0.97f, 1f);
                bottom = new Color(0.45f, 0.68f, 0.9f);
            }
            else if (winner == match.YourTeam)
            {
                title = UiText.Victory;
                top = UiKit.FireTop;
                bottom = UiKit.FireBottom;
            }
            else
            {
                title = UiText.Defeat;
                top = new Color(1f, 0.6f, 0.5f);
                bottom = new Color(0.8f, 0.08f, 0.1f);
            }
            _title.text = title;
            UiKit.Gradient(_title, top, bottom);
            _score.text = UiText.You + "  " + ownWins + "  -  " + otherWins + "  Rival";
            Bajoterra.Client.Hud.StatLine mine = match.Stats.Line(match.YourId);
            _detail.text = "Tus bajas: " + mine.Kills + "     Caídas: " + mine.Deaths + "     Daño: "
                           + Mathf.RoundToInt(mine.DamageDealt) + "     Curación: " + Mathf.RoundToInt(mine.Healing);
            _table.Refresh(match, ownWins, otherWins);
            _primaryAction = backToRoom;
            _secondaryAction = backToMenu;
            _age = 0f;
            Show();
        }

        protected override void OnTick(float deltaTime)
        {
            // The title drops in with a small overshoot.
            _age += deltaTime;
            float t = Mathf.Clamp01(_age / 0.45f);
            float overshoot = 1f + Mathf.Sin(t * Mathf.PI) * 0.25f * (1f - t);
            float scale = Mathf.Lerp(1.6f, 1f, 1f - (1f - t) * (1f - t)) * overshoot;
            _title.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
