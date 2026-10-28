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
            Image dim = UiKit.Panel(Root, "Dim", new Color(0.01f, 0.015f, 0.035f, 0.9f));
            UiKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            _title = UiKit.Label(Root, "Title", string.Empty, 150, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(1500f, 190f), new Vector2(0f, -34f));
            _title.fontStyle = FontStyle.Bold;
            UiKit.AddOutline(_title, 4f);
            _score = UiKit.Label(Root, "Score", string.Empty, 44, TextAnchor.MiddleCenter, UiKit.TextColor);
            UiKit.Place(_score.rectTransform, new Vector2(0.5f, 1f), new Vector2(1000f, 60f), new Vector2(0f, -226f));
            _score.fontStyle = FontStyle.Bold;
            UiKit.AddShadow(_score, 2f);
            _detail = UiKit.Label(Root, "Detail", string.Empty, 24, TextAnchor.MiddleCenter, UiKit.TextDim);
            UiKit.Place(_detail.rectTransform, new Vector2(0.5f, 1f), new Vector2(1000f, 36f), new Vector2(0f, -284f));

            RectTransform tableHolder = UiKit.Rect("TableHolder", Root);
            UiKit.Place(tableHolder, new Vector2(0.5f, 0.5f), new Vector2(1060f, 600f), new Vector2(0f, -78f));
            _table = new HudScoreboard(tableHolder);
            _table.Root.anchorMin = new Vector2(0.5f, 0.5f);
            _table.Root.anchorMax = new Vector2(0.5f, 0.5f);
            _table.Root.pivot = new Vector2(0.5f, 0.5f);
            _table.Root.anchoredPosition = Vector2.zero;

            _primary = UiKit.MakeButton(Root, "Primary", "Volver a la sala", new Vector2(420f, 78f), () => _primaryAction?.Invoke(),
                ButtonStyle.Primary, 30);
            UiKit.Place((RectTransform)_primary.transform, new Vector2(0.5f, 0f), new Vector2(420f, 78f), new Vector2(-230f, 50f));
            _secondary = UiKit.MakeButton(Root, "Secondary", "Menú principal", new Vector2(420f, 78f), () => _secondaryAction?.Invoke(),
                ButtonStyle.Secondary, 30);
            UiKit.Place((RectTransform)_secondary.transform, new Vector2(0.5f, 0f), new Vector2(420f, 78f), new Vector2(230f, 50f));
        }

        /// <summary>Shows the result of a finished match.</summary>
        /// <param name="winner">Team that won, or -1 for a draw.</param>
        public void Present(MatchSession match, int winner, int ownWins, int otherWins, Action backToRoom,
            Action backToMenu)
        {
            string title;
            Color color;
            if (winner < 0)
            {
                title = UiText.Draw;
                color = Color.white;
            }
            else if (winner == match.YourTeam)
            {
                title = UiText.Victory;
                color = Palette.Health;
            }
            else
            {
                title = UiText.Defeat;
                color = Palette.TeamOther;
            }
            _title.text = title;
            _title.color = color;
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
