using System.Collections.Generic;
using Bajoterra.Client.Hud;
using Bajoterra.Client.Session;
using Bajoterra.Protocol;
using UnityEngine;
using UnityEngine.UI;

namespace Bajoterra.Unity
{
    /// <summary>
    /// The match table: both teams, one row per player with the hero, kills, deaths, damage dealt and healing done.
    /// It sits inside any parent (the Tab overlay during a match, the result screen at the end) and is refreshed from
    /// <see cref="MatchStats"/>.
    /// </summary>
    public sealed class HudScoreboard
    {
        private const int RowsPerTeam = 3;
        private const float RowHeight = 54f;
        private const float Width = 1060f;

        // Column left edges and widths.
        private static readonly float[] ColumnX = { 36f, 360f, 560f, 650f, 740f, 880f };
        private static readonly float[] ColumnWidth = { 310f, 190f, 80f, 80f, 130f, 130f };

        private sealed class Row
        {
            public Image Background;
            public Image Bar;
            public Text[] Cells = new Text[6];
        }

        private static readonly Color RowColor = new Color(0.04f, 0.13f, 0.22f, 0.92f);

        private readonly RectTransform _root;
        private readonly Row[][] _rows = new Row[2][];
        private readonly Text[] _teamTitle = new Text[2];
        private readonly Text[] _teamScore = new Text[2];

        public HudScoreboard(RectTransform parent)
        {
            _root = UiKit.Rect("Scoreboard", parent);
            float teamHeight = 52f + 40f + RowsPerTeam * RowHeight;
            UiKit.Place(_root, new Vector2(0.5f, 0.5f), new Vector2(Width, teamHeight * 2f + 30f), Vector2.zero);
            for (int team = 0; team < 2; team++)
            {
                float top = -team * (teamHeight + 30f);
                RectTransform block = UiKit.Rect("Team " + team, _root);
                UiKit.Place(block, new Vector2(0.5f, 1f), new Vector2(Width, teamHeight), new Vector2(0f, top));
                Color accent = team == 0 ? Palette.TeamOwn : Palette.TeamOther;

                Image bar = UiKit.Window(block, "Header", Palette.WithAlpha(accent * 0.6f, 0.96f), 12);
                UiKit.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(Width, 56f), Vector2.zero);
                _teamTitle[team] = UiKit.Label(bar.transform, "Title", string.Empty, 32, TextAnchor.MiddleLeft, Color.white);
                UiKit.Heading(_teamTitle[team]);
                UiKit.Stretch(_teamTitle[team].rectTransform);
                _teamTitle[team].rectTransform.offsetMin = new Vector2(36f, 0f);
                UiKit.AddShadow(_teamTitle[team], 1.5f);
                _teamScore[team] = UiKit.Label(bar.transform, "Score", string.Empty, 38, TextAnchor.MiddleRight, Color.white);
                UiKit.Heading(_teamScore[team]);
                UiKit.Stretch(_teamScore[team].rectTransform);
                _teamScore[team].rectTransform.offsetMax = new Vector2(-36f, 0f);

                string[] heads = { "Jugador", "Héroe", "Bajas", "Caídas", "Daño", "Curación" };
                for (int c = 0; c < heads.Length; c++)
                {
                    Text head = UiKit.Label(block, "Head " + c, heads[c], 21, c < 2 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter,
                        UiKit.TextDim);
                    UiKit.AddShadow(head, 1.5f);
                    UiKit.Place(head.rectTransform, new Vector2(0f, 1f), new Vector2(ColumnWidth[c], 36f),
                        new Vector2(ColumnX[c], -56f));
                }

                _rows[team] = new Row[RowsPerTeam];
                for (int r = 0; r < RowsPerTeam; r++)
                {
                    var row = new Row();
                    row.Background = UiKit.Card(block, "Row " + r, RowColor, 10);
                    UiKit.Place(row.Background.rectTransform, new Vector2(0.5f, 1f), new Vector2(Width, RowHeight - 6f),
                        new Vector2(0f, -(92f + r * RowHeight)));
                    row.Bar = UiKit.Card(row.Background.transform, "Bar", accent, 4);
                    UiKit.Place(row.Bar.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f, RowHeight - 20f), new Vector2(12f, 0f));
                    row.Bar.rectTransform.pivot = new Vector2(0f, 0.5f);
                    for (int c = 0; c < 6; c++)
                    {
                        row.Cells[c] = UiKit.Label(row.Background.transform, "Cell " + c, string.Empty, c < 2 ? 26 : 28,
                            c < 2 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, UiKit.TextColor);
                        UiKit.AddShadow(row.Cells[c], 1.5f);
                        UiKit.Place(row.Cells[c].rectTransform, new Vector2(0f, 0.5f),
                            new Vector2(ColumnWidth[c], RowHeight - 6f), new Vector2(ColumnX[c], 0f));
                        row.Cells[c].rectTransform.pivot = new Vector2(0f, 0.5f);
                    }
                    _rows[team][r] = row;
                }
            }
        }

        public RectTransform Root
        {
            get { return _root; }
        }

        /// <summary>Fills the table. <paramref name="ownWins"/> and <paramref name="otherWins"/> are rounds won.</summary>
        public void Refresh(MatchSession match, int ownWins, int otherWins)
        {
            for (int team = 0; team < 2; team++)
            {
                bool own = team == 0;
                int teamId = own ? match.YourTeam : 1 - match.YourTeam;
                _teamTitle[team].text = own ? "TU EQUIPO" : "RIVALES";
                _teamScore[team].text = (own ? ownWins : otherWins).ToString();
                int rowIndex = 0;
                for (int i = 0; i < match.Slots.Count && rowIndex < RowsPerTeam; i++)
                {
                    Slot slot = match.Slots[i];
                    if (slot.Team != teamId)
                    {
                        continue;
                    }
                    Fill(_rows[team][rowIndex++], match, i, slot, own);
                }
                for (; rowIndex < RowsPerTeam; rowIndex++)
                {
                    _rows[team][rowIndex].Background.gameObject.SetActive(false);
                }
            }
        }

        private static void Fill(Row row, MatchSession match, int id, Slot slot, bool ownTeam)
        {
            row.Background.gameObject.SetActive(true);
            StatLine line = match.Stats.Line(id);
            bool you = id == match.YourId;
            string hero = match.Content.FindCharacter(slot.CharacterId)?.Name ?? slot.CharacterId;
            row.Background.color = you ? new Color(0.12f, 0.33f, 0.5f, 0.95f) : RowColor;
            row.Cells[0].text = slot.Name + (you ? "  (tú)" : slot.Bot ? "  (CPU)" : string.Empty);
            row.Cells[0].color = you ? UiKit.GoldColor : UiKit.TextColor;
            row.Cells[1].text = hero;
            row.Cells[1].color = ownTeam ? new Color(0.72f, 0.86f, 1f) : new Color(1f, 0.8f, 0.76f);
            row.Cells[2].text = line.Kills.ToString();
            row.Cells[3].text = line.Deaths.ToString();
            row.Cells[4].text = Mathf.RoundToInt(line.DamageDealt).ToString();
            row.Cells[5].text = Mathf.RoundToInt(line.Healing).ToString();
            row.Cells[2].color = line.Kills > 0 ? UiKit.GoldColor : UiKit.TextColor;
        }
    }
}
