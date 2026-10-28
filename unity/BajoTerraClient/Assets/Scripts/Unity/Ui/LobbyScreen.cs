using System;
using System.Collections.Generic;
using System.Text;
using Bajoterra.Client.Content;
using Bajoterra.Protocol;
using UnityEngine;
using UnityEngine.UI;
using ProtoCharacter = Bajoterra.Protocol.CharacterInfo;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Two steps before a match: choose a room and a team, then in the room pick a hero (with its numbers on the
    /// card), build the ten-slug belt (a row of coloured tiles fills up as you add slugs) and press ready. The hero
    /// and slug lists come from the server's content, so a slug added on the server appears here with no client
    /// change.
    /// </summary>
    public sealed class LobbyScreen : UiScreen
    {
        public const int LoadoutSize = 10;

        /// <summary>
        /// Copies of one slug the server accepts by default (LoadoutValidator.DEFAULT_MAX_DUPLICATES). The server
        /// stays the judge; this only stops the player from building a loadout it would refuse.
        /// </summary>
        public const int MaxPerSlug = 2;

        private sealed class HeroCard
        {
            public Image Background;
            public Image Edge;
        }

        private readonly RectTransform _joinPanel;
        private readonly RectTransform _roomPanel;
        private readonly InputField _room;
        private readonly Button[] _teamButtons = new Button[3];
        private readonly Text _error;
        private readonly Text _members;
        private readonly Text _loadoutTotal;
        private readonly Button _readyButton;
        private readonly RectTransform _characters;
        private readonly RectTransform _slugs;
        private readonly Image[] _belt = new Image[LoadoutSize];
        private readonly List<HeroCard> _heroCards = new List<HeroCard>();
        private readonly List<Text> _countLabels = new List<Text>();
        private readonly List<int> _counts = new List<int>();
        private ContentCatalog _content;
        private int _team = -1;
        private int _characterIndex;
        private bool _ready;

        public event Action<string, int> JoinClicked;

        public event Action<string, string[]> ReadyClicked;

        public event Action ReadyCancelled;

        public event Action LeaveClicked;

        public LobbyScreen(UiRoot ui) : base(ui, "Lobby")
        {
            Image background = UiKit.Panel(Root, "Background", new Color(0.02f, 0.03f, 0.055f, 0.94f));
            UiKit.Stretch(background.rectTransform);
            background.raycastTarget = true;

            // ----- step 1: room and team
            Image join = UiKit.Window(Root, "Join", UiKit.PanelColor, 24);
            UiKit.Place(join.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(780f, 600f), Vector2.zero);
            _joinPanel = join.rectTransform;
            Text joinTitle = UiKit.Label(_joinPanel, "Title", "SALA DE JUEGO", 44, TextAnchor.MiddleCenter, UiKit.TextColor);
            UiKit.Place(joinTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(700f, 70f), new Vector2(0f, -30f));
            joinTitle.fontStyle = FontStyle.Bold;
            UiKit.AddShadow(joinTitle, 2f);
            Text roomLabel = UiKit.Label(_joinPanel, "RoomLabel", UiText.Room, 23, TextAnchor.MiddleLeft, UiKit.TextDim);
            UiKit.Place(roomLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(660f, 30f), new Vector2(0f, -124f));
            _room = UiKit.MakeField(_joinPanel, "Room", "arena", new Vector2(660f, 60f));
            UiKit.Place((RectTransform)_room.transform, new Vector2(0.5f, 1f), new Vector2(660f, 60f), new Vector2(0f, -158f));
            Text teamLabel = UiKit.Label(_joinPanel, "TeamLabel", UiText.Team, 23, TextAnchor.MiddleLeft, UiKit.TextDim);
            UiKit.Place(teamLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(660f, 30f), new Vector2(0f, -240f));
            string[] teamCaptions = { UiText.TeamAuto, "Equipo 1", "Equipo 2" };
            for (int i = 0; i < 3; i++)
            {
                int value = i - 1;
                _teamButtons[i] = UiKit.MakeButton(_joinPanel, "Team " + i, teamCaptions[i], new Vector2(212f, 62f),
                    () => SetTeam(value), ButtonStyle.Secondary, 24);
                UiKit.Place((RectTransform)_teamButtons[i].transform, new Vector2(0.5f, 1f), new Vector2(212f, 62f),
                    new Vector2(-224f + i * 224f, -274f));
            }
            Button joinButton = UiKit.MakeButton(_joinPanel, "JoinButton", "Unirse a la sala", new Vector2(660f, 80f),
                () => JoinClicked?.Invoke(_room.text.Trim(), _team), ButtonStyle.Primary, 32);
            UiKit.Place((RectTransform)joinButton.transform, new Vector2(0.5f, 1f), new Vector2(660f, 80f), new Vector2(0f, -378f));
            Button back = UiKit.MakeButton(_joinPanel, "BackButton", UiText.BackToMenu, new Vector2(320f, 60f),
                () => LeaveClicked?.Invoke(), ButtonStyle.Ghost, 25);
            UiKit.Place((RectTransform)back.transform, new Vector2(0.5f, 1f), new Vector2(320f, 60f), new Vector2(0f, -480f));
            SetTeam(-1);

            // ----- step 2: inside the room
            _roomPanel = UiKit.Rect("RoomPanel", Root);
            UiKit.Stretch(_roomPanel, 50f);

            Text heroTitle = Heading(_roomPanel, "ELIGE TU HÉROE", new Vector2(0f, 1f), new Vector2(40f, -10f));
            _characters = UiKit.Rect("Characters", _roomPanel);
            UiKit.Place(_characters, new Vector2(0f, 1f), new Vector2(520f, 860f), new Vector2(40f, -64f));

            _loadoutTotal = Heading(_roomPanel, UiText.Loadout, new Vector2(0f, 1f), new Vector2(620f, -10f));
            Button random = UiKit.MakeButton(_roomPanel, "Random", UiText.RandomLoadout, new Vector2(330f, 50f), ClearLoadout,
                ButtonStyle.Ghost, 20);
            UiKit.Place((RectTransform)random.transform, new Vector2(0f, 1f), new Vector2(330f, 50f), new Vector2(1000f, -4f));
            for (int i = 0; i < LoadoutSize; i++)
            {
                Image tile = UiKit.Card(_roomPanel, "Belt " + i, new Color(0.1f, 0.13f, 0.2f, 0.9f), 8);
                UiKit.Place(tile.rectTransform, new Vector2(0f, 1f), new Vector2(64f, 52f), new Vector2(620f + i * 70f, -66f));
                UiKit.Edge(tile.transform, new Color(1f, 1f, 1f, 0.15f), 8, 2);
                _belt[i] = tile;
            }
            _slugs = UiKit.Rect("Slugs", _roomPanel);
            UiKit.Place(_slugs, new Vector2(0f, 1f), new Vector2(760f, 760f), new Vector2(620f, -134f));

            Image membersPanel = UiKit.Window(_roomPanel, "MembersPanel", UiKit.PanelColor, 20);
            UiKit.Place(membersPanel.rectTransform, new Vector2(1f, 1f), new Vector2(420f, 520f), new Vector2(0f, -10f));
            Text membersTitle = UiKit.Label(membersPanel.transform, "Title", "JUGADORES EN LA SALA", 22, TextAnchor.MiddleLeft,
                UiKit.TextDim);
            UiKit.Place(membersTitle.rectTransform, new Vector2(0f, 1f), new Vector2(360f, 34f), new Vector2(28f, -20f));
            membersTitle.fontStyle = FontStyle.Bold;
            _members = UiKit.Label(membersPanel.transform, "Members", string.Empty, 25, TextAnchor.UpperLeft, UiKit.TextColor);
            UiKit.Place(_members.rectTransform, new Vector2(0f, 1f), new Vector2(370f, 440f), new Vector2(28f, -62f));
            _members.horizontalOverflow = HorizontalWrapMode.Wrap;
            _members.supportRichText = true;

            _readyButton = UiKit.MakeButton(_roomPanel, "Ready", UiText.Ready, new Vector2(420f, 96f), OnReadyClicked,
                ButtonStyle.Success, 38);
            UiKit.Place((RectTransform)_readyButton.transform, new Vector2(1f, 0f), new Vector2(420f, 96f), new Vector2(0f, 90f));
            Button leave = UiKit.MakeButton(_roomPanel, "Leave", UiText.Leave, new Vector2(420f, 64f), () => LeaveClicked?.Invoke(),
                ButtonStyle.Danger, 26);
            UiKit.Place((RectTransform)leave.transform, new Vector2(1f, 0f), new Vector2(420f, 64f), Vector2.zero);

            _error = UiKit.Label(Root, "Error", string.Empty, 26, TextAnchor.MiddleCenter, new Color(1f, 0.55f, 0.45f));
            UiKit.Place(_error.rectTransform, new Vector2(0.5f, 0f), new Vector2(1500f, 50f), new Vector2(0f, 14f));
            UiKit.AddShadow(_error, 1.5f);
            heroTitle.fontStyle = FontStyle.Bold;
        }

        /// <summary>Shows the room/team step.</summary>
        public void ShowJoin(string roomId)
        {
            _room.text = roomId;
            _joinPanel.gameObject.SetActive(true);
            _roomPanel.gameObject.SetActive(false);
            _error.text = string.Empty;
            Show();
        }

        /// <summary>Shows the in-room step, building the hero and slug pickers from the server's content.</summary>
        public void ShowRoom(ContentCatalog content, string characterId)
        {
            if (!ReferenceEquals(_content, content))
            {
                _content = content;
                BuildCharacters();
                BuildSlugs();
            }
            SelectCharacter(Mathf.Max(0, content.CharacterIndexOf(characterId)));
            _ready = false;
            RefreshReady();
            _joinPanel.gameObject.SetActive(false);
            _roomPanel.gameObject.SetActive(true);
            Show();
        }

        /// <summary>True while the in-room step (character, loadout, ready) is the one on screen.</summary>
        public bool RoomStepVisible
        {
            get { return Visible && _roomPanel.gameObject.activeSelf; }
        }

        public string SelectedCharacterId
        {
            get { return _content == null ? string.Empty : _content.Manifest.CharacterIds[_characterIndex]; }
        }

        public void ShowError(string message)
        {
            _error.text = message;
        }

        /// <summary>Updates the member list and the ready button from the server's room state.</summary>
        public void UpdateRoom(RoomState room, ContentCatalog content)
        {
            var text = new StringBuilder();
            text.Append("<color=#8fa0bd>Sala</color> <b>").Append(room.RoomId).Append("</b>\n\n");
            foreach (Slot member in room.Slots)
            {
                string character = content.FindCharacter(member.CharacterId)?.Name ?? member.CharacterId;
                text.Append("<color=")
                    .Append(member.Team == 0 ? "#6fb4ff" : "#ff7a6b").Append(">")
                    .Append(member.Name).Append("</color>  <color=#8fa0bd>")
                    .Append(character).Append("</color>")
                    .Append(member.Ready ? "  <color=#4ce08a>listo</color>" : string.Empty)
                    .Append(member.You ? "  <color=#ffc847>(tú)</color>" : string.Empty).Append('\n');
            }
            text.Append("\n<color=#8fa0bd>Los huecos se completan con bots hasta 3 contra 3.</color>");
            _members.text = text.ToString();
        }

        // ------------------------------------------------------------------------------------------ building

        private static Text Heading(RectTransform parent, string caption, Vector2 anchor, Vector2 offset)
        {
            Text heading = UiKit.Label(parent, "Heading", caption, 30, TextAnchor.MiddleLeft, UiKit.TextColor);
            UiKit.Place(heading.rectTransform, anchor, new Vector2(640f, 48f), offset);
            heading.fontStyle = FontStyle.Bold;
            UiKit.AddShadow(heading, 2f);
            return heading;
        }

        private void BuildCharacters()
        {
            for (int i = _characters.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(_characters.GetChild(i).gameObject);
            }
            _heroCards.Clear();
            for (int i = 0; i < _content.Details.Characters.Count; i++)
            {
                ProtoCharacter character = _content.Character(i);
                int index = i;
                CharacterStyle style = CharacterStyle.For(character.Id);
                var card = new HeroCard();
                Button button = UiKit.MakeButton(_characters, "Hero " + character.Id, string.Empty, new Vector2(520f, 196f),
                    () => SelectCharacter(index), ButtonStyle.Secondary);
                UiKit.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(520f, 196f),
                    new Vector2(0f, -i * 210f));
                button.GetComponentInChildren<Text>().text = string.Empty;
                card.Background = button.GetComponent<Image>();
                var edgeObject = button.transform.Find("Edge");
                card.Edge = edgeObject != null ? edgeObject.GetComponent<Image>() : null;

                Image swatch = UiKit.Card(button.transform, "Swatch", style.Outfit, 14);
                UiKit.Place(swatch.rectTransform, new Vector2(0f, 0.5f), new Vector2(120f, 150f), new Vector2(20f, 0f));
                swatch.rectTransform.pivot = new Vector2(0f, 0.5f);
                Image head = UiKit.Panel(swatch.transform, "Head", style.Skin);
                head.sprite = UiSprites.Circle();
                UiKit.Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(56f, 56f), new Vector2(0f, -18f));
                Image hair = UiKit.Panel(head.transform, "Hair", style.Hair);
                hair.sprite = UiSprites.Circle();
                UiKit.Place(hair.rectTransform, new Vector2(0.5f, 1f), new Vector2(60f, 26f), new Vector2(0f, 6f));
                Image belt = UiKit.Panel(swatch.transform, "Trim", style.Trim);
                UiKit.Place(belt.rectTransform, new Vector2(0.5f, 0f), new Vector2(120f, 14f), new Vector2(0f, 38f));
                Image pants = UiKit.Panel(swatch.transform, "Pants", style.Pants);
                UiKit.Place(pants.rectTransform, new Vector2(0.5f, 0f), new Vector2(120f, 36f), new Vector2(0f, 0f));

                Text name = UiKit.Label(button.transform, "Name", character.Name, 31, TextAnchor.MiddleLeft, Color.white);
                UiKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(350f, 40f), new Vector2(160f, -14f));
                name.fontStyle = FontStyle.Bold;
                UiKit.AddShadow(name, 1.5f);

                StatRow(button.transform, 0, "Vida", character.MaxHp, 160f, Palette.Health, character.MaxHp.ToString("0"));
                StatRow(button.transform, 1, "Escudo", character.MaxShield, 100f, Palette.Shield, character.MaxShield.ToString("0"));
                StatRow(button.transform, 2, "Velocidad", character.Speed, 9f, UiKit.GoldColor, character.Speed.ToString("0.0"));

                MoveInfo[] moves = _content.MoveSetOf(character);
                var list = new StringBuilder();
                for (int m = 0; m < moves.Length; m++)
                {
                    list.Append(m > 0 ? "  -  " : string.Empty).Append(MoveName(moves[m].Id));
                }
                Text moveText = UiKit.Label(button.transform, "Moves", list.ToString(), 18, TextAnchor.MiddleLeft, UiKit.TextDim);
                UiKit.Place(moveText.rectTransform, new Vector2(0f, 0f), new Vector2(350f, 26f), new Vector2(160f, 10f));
                _heroCards.Add(card);
            }
        }

        private static void StatRow(Transform parent, int row, string label, float value, float max, Color color, string number)
        {
            float y = -62f - row * 30f;
            Text caption = UiKit.Label(parent, "Stat " + label, label, 18, TextAnchor.MiddleLeft, UiKit.TextDim);
            UiKit.Place(caption.rectTransform, new Vector2(0f, 1f), new Vector2(100f, 24f), new Vector2(160f, y));
            var bar = new UiBar(parent, "Bar " + label, new Vector2(190f, 12f), color, 5);
            UiKit.Place(bar.Rect, new Vector2(0f, 1f), new Vector2(190f, 12f), new Vector2(262f, y - 6f));
            bar.Set(Mathf.Clamp01(value / max), 100f);
            Text digits = UiKit.Label(parent, "Value " + label, number, 20, TextAnchor.MiddleRight, Color.white);
            UiKit.Place(digits.rectTransform, new Vector2(0f, 1f), new Vector2(60f, 24f), new Vector2(456f, y));
            digits.fontStyle = FontStyle.Bold;
        }

        private static string MoveName(string id)
        {
            switch (id)
            {
                case "jump": return "Salto";
                case "crouch": return "Agacharse";
                case "roll": return "Rodar";
                case "dash": return "Dash";
                default: return id;
            }
        }

        private void BuildSlugs()
        {
            for (int i = _slugs.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(_slugs.GetChild(i).gameObject);
            }
            _countLabels.Clear();
            _counts.Clear();

            for (int i = 0; i < _content.SlugCount; i++)
            {
                int index = i;
                SlugInfo slug = _content.Slug(i);
                _counts.Add(0);
                int column = i % 2;
                int row = i / 2;
                Color element = Palette.Element(_content.ElementName((int)slug.Element));
                Image card = UiKit.Window(_slugs, "Slug " + slug.Id, new Color(element.r * 0.22f, element.g * 0.22f, element.b * 0.22f, 0.95f), 12);
                UiKit.Place(card.rectTransform, new Vector2(0f, 1f), new Vector2(372f, 84f), new Vector2(column * 388f, -row * 92f));
                Image stripe = UiKit.Card(card.transform, "Stripe", element, 4);
                UiKit.Place(stripe.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f, 62f), new Vector2(10f, 0f));
                stripe.rectTransform.pivot = new Vector2(0f, 0.5f);
                Text name = UiKit.Label(card.transform, "Name", slug.Name, 27, TextAnchor.MiddleLeft, UiKit.TextColor);
                UiKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(210f, 36f), new Vector2(30f, -8f));
                name.fontStyle = FontStyle.Bold;
                UiKit.AddShadow(name, 1.5f);
                Text kind = UiKit.Label(card.transform, "Kind", UiText.Element(_content.ElementName((int)slug.Element)) + " - " + RoleCaption(slug),
                    18, TextAnchor.MiddleLeft, Color.Lerp(element, Color.white, 0.4f));
                UiKit.Place(kind.rectTransform, new Vector2(0f, 1f), new Vector2(210f, 28f), new Vector2(30f, -46f));
                Button minus = UiKit.MakeButton(card.transform, "Minus", "-", new Vector2(46f, 46f), () => Change(index, -1),
                    ButtonStyle.Secondary, 30);
                UiKit.Place((RectTransform)minus.transform, new Vector2(1f, 0.5f), new Vector2(46f, 46f), new Vector2(-124f, 0f));
                Text count = UiKit.Label(card.transform, "Count", "0", 32, TextAnchor.MiddleCenter, UiKit.TextColor);
                UiKit.Place(count.rectTransform, new Vector2(1f, 0.5f), new Vector2(44f, 46f), new Vector2(-78f, 0f));
                count.fontStyle = FontStyle.Bold;
                Button plus = UiKit.MakeButton(card.transform, "Plus", "+", new Vector2(46f, 46f), () => Change(index, 1),
                    ButtonStyle.Primary, 30);
                UiKit.Place((RectTransform)plus.transform, new Vector2(1f, 0.5f), new Vector2(46f, 46f), new Vector2(-14f, 0f));
                _countLabels.Add(count);
            }
            RefreshLoadout();
        }

        private static string RoleCaption(SlugInfo slug)
        {
            return UiText.Role(slug.Role);
        }

        // ------------------------------------------------------------------------------------------ behaviour

        private void SetTeam(int team)
        {
            _team = team;
            for (int i = 0; i < _teamButtons.Length; i++)
            {
                ButtonStyle style = i - 1 == _team ? ButtonStyle.Primary : ButtonStyle.Secondary;
                UiKit.Restyle(_teamButtons[i], style);
            }
        }

        private void SelectCharacter(int index)
        {
            _characterIndex = Mathf.Clamp(index, 0, Mathf.Max(0, _heroCards.Count - 1));
            for (int i = 0; i < _heroCards.Count; i++)
            {
                bool selected = i == _characterIndex;
                UiKit.SetColor(_heroCards[i].Background.GetComponent<Button>(),
                    selected ? new Color(0.1f, 0.3f, 0.5f, 1f) : UiKit.ButtonColor);
                if (_heroCards[i].Edge != null)
                {
                    _heroCards[i].Edge.color = selected ? UiKit.AccentColor : new Color(1f, 1f, 1f, 0.14f);
                }
            }
        }

        private void Change(int slug, int delta)
        {
            if (_ready)
            {
                return;
            }
            int total = Total();
            if (delta > 0 && (total >= LoadoutSize || _counts[slug] >= MaxPerSlug))
            {
                return;
            }
            _counts[slug] = Mathf.Max(0, _counts[slug] + delta);
            RefreshLoadout();
        }

        private void ClearLoadout()
        {
            if (_ready)
            {
                return;
            }
            for (int i = 0; i < _counts.Count; i++)
            {
                _counts[i] = 0;
            }
            RefreshLoadout();
        }

        private int Total()
        {
            int total = 0;
            foreach (int count in _counts)
            {
                total += count;
            }
            return total;
        }

        private void RefreshLoadout()
        {
            int total = Total();
            for (int i = 0; i < _countLabels.Count; i++)
            {
                _countLabels[i].text = _counts[i].ToString();
                _countLabels[i].color = _counts[i] > 0 ? Color.white : UiKit.TextDim;
            }
            _loadoutTotal.text = total == 0
                ? UiText.Loadout + "  -  al azar"
                : UiText.Loadout + "  -  " + total + "/" + LoadoutSize;
            // The belt: one tile per chosen slug, in the colour of its element.
            int tile = 0;
            for (int i = 0; i < _counts.Count && tile < LoadoutSize; i++)
            {
                Color element = Palette.Element(_content.ElementName((int)_content.Slug(i).Element));
                for (int n = 0; n < _counts[i] && tile < LoadoutSize; n++)
                {
                    _belt[tile++].color = element;
                }
            }
            for (; tile < LoadoutSize; tile++)
            {
                _belt[tile].color = new Color(0.1f, 0.13f, 0.2f, 0.9f);
            }
            _readyButton.interactable = _ready || total == 0 || total == LoadoutSize;
        }

        private void OnReadyClicked()
        {
            if (_ready)
            {
                _ready = false;
                RefreshReady();
                ReadyCancelled?.Invoke();
                return;
            }
            var slugs = new List<string>();
            for (int i = 0; i < _counts.Count; i++)
            {
                for (int n = 0; n < _counts[i]; n++)
                {
                    slugs.Add(_content.Manifest.SlugIds[i]);
                }
            }
            _ready = true;
            RefreshReady();
            ReadyClicked?.Invoke(SelectedCharacterId, slugs.ToArray());
        }

        private void RefreshReady()
        {
            UiKit.SetCaption(_readyButton, _ready ? UiText.NotReady : UiText.Ready);
            UiKit.Restyle(_readyButton, _ready ? ButtonStyle.Danger : ButtonStyle.Success);
            RefreshLoadout();
        }

        /// <summary>Called when the server refused the loadout, so the player can fix it.</summary>
        public void ResetReady()
        {
            _ready = false;
            if (_content != null)
            {
                RefreshReady();
            }
        }
    }
}
