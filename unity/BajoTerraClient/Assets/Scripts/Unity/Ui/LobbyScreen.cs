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
        private static readonly Color EmptyTile = new Color(0.06f, 0.16f, 0.27f, 0.92f);

        private readonly Text _error;
        private readonly Text _joinInvite;
        private readonly Text _members;
        private readonly Text _loadoutTotal;
        private readonly Button _readyButton;
        private readonly RectTransform _characters;
        private readonly RectTransform _slugs;
        private readonly Image[] _belt = new Image[LoadoutSize];
        private readonly RawImage[] _beltSlugs = new RawImage[LoadoutSize];
        private readonly List<HeroCard> _heroCards = new List<HeroCard>();
        private readonly List<Text> _countLabels = new List<Text>();
        private readonly List<int> _counts = new List<int>();
        private readonly HeroPortraits _portraits;
        private ContentCatalog _content;
        private int _team = -1;
        private int _characterIndex;
        private bool _ready;
        private string _inviteCode;

        public event Action<string, int> JoinClicked;

        public event Action<string, string[]> ReadyClicked;

        public event Action ReadyCancelled;

        public event Action LeaveClicked;

        public LobbyScreen(UiRoot ui) : base(ui, "Lobby")
        {
            _portraits = new HeroPortraits(ui.Transform.parent);
            Image background = UiKit.Panel(Root, "Background", new Color(0.0f, 0.05f, 0.08f, 0.84f));
            UiKit.Stretch(background.rectTransform);
            background.raycastTarget = true;

            // ----- step 1: room and team
            Image join = UiKit.Window(Root, "Join", UiKit.PanelColor, 24);
            UiKit.Place(join.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(780f, 640f), Vector2.zero);
            _joinPanel = join.rectTransform;
            Text joinTitle = UiKit.Label(_joinPanel, "Title", "SALA DE JUEGO", 60, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(joinTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(700f, 80f), new Vector2(0f, -26f));
            UiKit.Fire(joinTitle);
            Text roomLabel = UiKit.Label(_joinPanel, "RoomLabel", UiText.Room, 25, TextAnchor.MiddleLeft, UiKit.TextDim);
            UiKit.Place(roomLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(660f, 30f), new Vector2(0f, -124f));
            _room = UiKit.MakeField(_joinPanel, "Room", "arena", new Vector2(660f, 62f));
            UiKit.Place((RectTransform)_room.transform, new Vector2(0.5f, 1f), new Vector2(660f, 62f), new Vector2(0f, -158f));
            Text teamLabel = UiKit.Label(_joinPanel, "TeamLabel", UiText.Team, 25, TextAnchor.MiddleLeft, UiKit.TextDim);
            UiKit.Place(teamLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(660f, 30f), new Vector2(0f, -240f));
            string[] teamCaptions = { UiText.TeamAuto, "Equipo 1", "Equipo 2" };
            for (int i = 0; i < 3; i++)
            {
                int value = i - 1;
                _teamButtons[i] = UiKit.MakeButton(_joinPanel, "Team " + i, teamCaptions[i], new Vector2(212f, 66f),
                    () => SetTeam(value), ButtonStyle.Secondary, 28);
                UiKit.Place((RectTransform)_teamButtons[i].transform, new Vector2(0.5f, 1f), new Vector2(212f, 66f),
                    new Vector2(-224f + i * 224f, -274f));
            }
            Button joinButton = UiKit.MakeButton(_joinPanel, "JoinButton", "¡ENTRAR!", new Vector2(660f, 96f),
                () => JoinClicked?.Invoke(_room.text.Trim(), _team), ButtonStyle.Primary, 46);
            UiKit.Place((RectTransform)joinButton.transform, new Vector2(0.5f, 1f), new Vector2(660f, 96f), new Vector2(0f, -372f));
            Button back = UiKit.MakeButton(_joinPanel, "BackButton", UiText.BackToMenu, new Vector2(340f, 64f),
                () => LeaveClicked?.Invoke(), ButtonStyle.Ghost, 28);
            UiKit.Place((RectTransform)back.transform, new Vector2(0.5f, 1f), new Vector2(340f, 64f), new Vector2(0f, -486f));
            _joinInvite = UiKit.Label(_joinPanel, "Invite", string.Empty, 23, TextAnchor.MiddleCenter, UiKit.AccentColor);
            UiKit.Place(_joinInvite.rectTransform, new Vector2(0.5f, 0f), new Vector2(700f, 50f), new Vector2(0f, 24f));
            _joinInvite.supportRichText = true;
            UiKit.AddShadow(_joinInvite, 1.5f);
            SetTeam(-1);

            // ----- step 2: inside the room
            _roomPanel = UiKit.Rect("RoomPanel", Root);
            UiKit.Stretch(_roomPanel, 50f);

            Heading(_roomPanel, "ELIGE TU HÉROE", new Vector2(0f, 1f), new Vector2(40f, -6f));
            _characters = UiKit.Rect("Characters", _roomPanel);
            UiKit.Place(_characters, new Vector2(0f, 1f), new Vector2(520f, 860f), new Vector2(40f, -70f));

            _loadoutTotal = Heading(_roomPanel, UiText.Loadout, new Vector2(0f, 1f), new Vector2(620f, -6f));
            _loadoutTotal.fontSize = 36;
            Button random = UiKit.MakeButton(_roomPanel, "Random", UiText.RandomLoadout, new Vector2(250f, 54f), ClearLoadout,
                ButtonStyle.Ghost, 24);
            UiKit.Place((RectTransform)random.transform, new Vector2(0f, 1f), new Vector2(250f, 54f), new Vector2(1128f, -4f));
            for (int i = 0; i < LoadoutSize; i++)
            {
                Image tile = UiKit.Card(_roomPanel, "Belt " + i, EmptyTile, 8);
                UiKit.Place(tile.rectTransform, new Vector2(0f, 1f), new Vector2(66f, 54f), new Vector2(620f + i * 76f, -70f));
                UiKit.Edge(tile.transform, UiKit.Ink, 8, 3);
                _belt[i] = tile;
                // The chosen slug itself, peeking out of its tile.
                var face = new GameObject("Slug", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                face.transform.SetParent(tile.transform, false);
                UiKit.Place(face.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(58f, 58f), new Vector2(0f, 2f));
                face.raycastTarget = false;
                face.enabled = false;
                _beltSlugs[i] = face;
            }
            _slugs = UiKit.Rect("Slugs", _roomPanel);
            UiKit.Place(_slugs, new Vector2(0f, 1f), new Vector2(760f, 760f), new Vector2(620f, -144f));

            Image membersPanel = UiKit.Window(_roomPanel, "MembersPanel", UiKit.PanelColor, 20);
            UiKit.Place(membersPanel.rectTransform, new Vector2(1f, 1f), new Vector2(420f, 560f), new Vector2(0f, -30f));
            Text membersTitle = UiKit.Tag(membersPanel.transform, "Title", "JUGADORES", new Vector2(220f, 44f));
            UiKit.Place((RectTransform)membersTitle.transform.parent, new Vector2(0.5f, 1f), new Vector2(220f, 44f),
                new Vector2(0f, 22f));
            _members = UiKit.Label(membersPanel.transform, "Members", string.Empty, 26, TextAnchor.UpperLeft, UiKit.TextColor);
            UiKit.Place(_members.rectTransform, new Vector2(0f, 1f), new Vector2(364f, 480f), new Vector2(30f, -46f));
            _members.horizontalOverflow = HorizontalWrapMode.Wrap;
            _members.supportRichText = true;
            UiKit.AddShadow(_members, 1.5f);

            _readyButton = UiKit.MakeButton(_roomPanel, "Ready", UiText.Ready, new Vector2(420f, 112f), OnReadyClicked,
                ButtonStyle.Success, 56);
            UiKit.Place((RectTransform)_readyButton.transform, new Vector2(1f, 0f), new Vector2(420f, 112f), new Vector2(0f, 96f));
            Button leave = UiKit.MakeButton(_roomPanel, "Leave", UiText.Leave, new Vector2(420f, 72f), () => LeaveClicked?.Invoke(),
                ButtonStyle.Danger, 32);
            UiKit.Place((RectTransform)leave.transform, new Vector2(1f, 0f), new Vector2(420f, 72f), Vector2.zero);

            _error = UiKit.Label(Root, "Error", string.Empty, 28, TextAnchor.MiddleCenter, new Color(1f, 0.62f, 0.5f));
            UiKit.Place(_error.rectTransform, new Vector2(0.5f, 0f), new Vector2(1500f, 50f), new Vector2(0f, 14f));
            UiKit.Strong(_error);
        }

        /// <summary>Releases the hero portraits (when the game shuts down).</summary>
        public void Destroy()
        {
            _portraits.Destroy();
        }

        protected override void OnTick(float deltaTime)
        {
            bool showing = _roomPanel.gameObject.activeSelf;
            _portraits.SetRunning(showing);
            if (showing)
            {
                _portraits.Tick(deltaTime);
            }
        }

        protected override void OnHidden()
        {
            _portraits.SetRunning(false);
        }

        /// <summary>
        /// The code friends type to join this computer's game, or null when it is somebody else's game (or there is no
        /// code to share). It is shown on both steps.
        /// </summary>
        public void SetInviteCode(string code)
        {
            _inviteCode = code;
            _joinInvite.text = code == null ? string.Empty : UiText.InviteCode + ":  <color=#ffd84a>" + code + "</color>";
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
            text.Append("<color=#9fd3e0>Sala</color>  <color=#ffd84a>").Append(room.RoomId).Append("</color>\n\n");
            foreach (Slot member in room.Slots)
            {
                string character = content.FindCharacter(member.CharacterId)?.Name ?? member.CharacterId;
                text.Append("<color=")
                    .Append(member.Team == 0 ? "#6fb4ff" : "#ff7a6b").Append(">")
                    .Append(member.Name).Append("</color>  <color=#9fd3e0>")
                    .Append(character).Append("</color>")
                    .Append(member.Ready ? "  <color=#9ef04a>listo</color>" : string.Empty)
                    .Append(member.You ? "  <color=#ffc847>(tú)</color>" : string.Empty).Append('\n');
            }
            text.Append("\n<color=#9fd3e0>Los huecos se completan con lanzadores de la caverna hasta 3 contra 3.</color>");
            if (_inviteCode != null)
            {
                text.Append("\n\n<color=#9fd3e0>").Append(UiText.InviteCode).Append(":</color>\n<color=#ffd84a>")
                    .Append(_inviteCode).Append("</color>");
            }
            _members.text = text.ToString();
        }

        // ------------------------------------------------------------------------------------------ building

        private static Text Heading(RectTransform parent, string caption, Vector2 anchor, Vector2 offset)
        {
            Text heading = UiKit.Label(parent, "Heading", caption, 42, TextAnchor.MiddleLeft, Color.white);
            UiKit.Place(heading.rectTransform, anchor, new Vector2(640f, 56f), offset);
            UiKit.Fire(heading);
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
                var card = new HeroCard();
                Button button = UiKit.MakeButton(_characters, "Hero " + character.Id, string.Empty, new Vector2(520f, 196f),
                    () => SelectCharacter(index), ButtonStyle.Secondary);
                UiKit.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(520f, 196f),
                    new Vector2(0f, -i * 210f));
                card.Background = button.GetComponent<Image>();

                // The hero's live portrait in a navy frame.
                Image frame = UiKit.Card(button.transform, "Portrait", UiKit.Ink, 12);
                UiKit.Place(frame.rectTransform, new Vector2(0f, 0.5f), new Vector2(150f, 172f), new Vector2(14f, 3f));
                frame.rectTransform.pivot = new Vector2(0f, 0.5f);
                RawImage portrait = UiKit.Rect("Picture", frame.transform).gameObject.AddComponent<RawImage>();
                portrait.raycastTarget = false;
                portrait.color = Color.white;
                UiKit.Stretch(portrait.rectTransform, 4f);
                portrait.texture = _portraits.Get(character.Id, character.HitRadius, character.HitHeight);
                Image shade = UiKit.Panel(frame.transform, "Shade", Palette.WithAlpha(UiKit.Ink, 0.55f));
                shade.sprite = UiSprites.Vignette();
                UiKit.Stretch(shade.rectTransform, 4f);
                UiKit.Edge(frame.transform, Palette.WithAlpha(UiKit.EdgeColor, 0.7f), 12, 2);

                card.Edge = UiKit.Edge(button.transform, UiKit.GoldColor, 16, 4);

                Text name = UiKit.Label(button.transform, "Name", character.Name, 30, TextAnchor.MiddleLeft, Color.white);
                UiKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(330f, 42f), new Vector2(180f, -14f));
                UiKit.Heading(name);

                StatRow(button.transform, 0, "Vida", character.MaxHp, 160f, Palette.Health, character.MaxHp.ToString("0"));
                StatRow(button.transform, 1, "Escudo", character.MaxShield, 100f, Palette.Shield, character.MaxShield.ToString("0"));
                StatRow(button.transform, 2, "Velocidad", character.Speed, 9f, UiKit.GoldColor, character.Speed.ToString("0.0"));

                MoveInfo[] moves = _content.MoveSetOf(character);
                var list = new StringBuilder();
                for (int m = 0; m < moves.Length; m++)
                {
                    list.Append(m > 0 ? "  -  " : string.Empty).Append(MoveName(moves[m].Id));
                }
                Text moveText = UiKit.Label(button.transform, "Moves", list.ToString(), 19, TextAnchor.MiddleLeft, UiKit.TextDim);
                UiKit.Place(moveText.rectTransform, new Vector2(0f, 0f), new Vector2(330f, 26f), new Vector2(180f, 14f));
                UiKit.AddShadow(moveText, 1.2f);
                _heroCards.Add(card);
            }
        }

        private static void StatRow(Transform parent, int row, string label, float value, float max, Color color, string number)
        {
            float y = -62f - row * 31f;
            Text caption = UiKit.Label(parent, "Stat " + label, label, 19, TextAnchor.MiddleLeft, UiKit.TextDim);
            UiKit.Place(caption.rectTransform, new Vector2(0f, 1f), new Vector2(100f, 24f), new Vector2(180f, y));
            UiKit.AddShadow(caption, 1.2f);
            var bar = new UiBar(parent, "Bar " + label, new Vector2(170f, 14f), color, 5);
            UiKit.Place(bar.Rect, new Vector2(0f, 1f), new Vector2(170f, 14f), new Vector2(278f, y - 5f));
            bar.Set(Mathf.Clamp01(value / max), 100f);
            Text digits = UiKit.Label(parent, "Value " + label, number, 22, TextAnchor.MiddleRight, Color.white);
            UiKit.Place(digits.rectTransform, new Vector2(0f, 1f), new Vector2(60f, 24f), new Vector2(456f, y));
            UiKit.Strong(digits);
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
                // The slug's 3D model, photographed for the card.
                Texture2D portrait = SlugPortraits.Get(slug.Id);
                float textLeft = 30f;
                if (portrait != null)
                {
                    var face = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                    face.transform.SetParent(card.transform, false);
                    face.texture = portrait;
                    face.raycastTarget = false;
                    UiKit.Place(face.rectTransform, new Vector2(0f, 0.5f), new Vector2(72f, 72f), new Vector2(16f, 0f));
                    textLeft = 86f;
                }
                // The name and its kind end where the -/+ buttons start, shrinking to fit when they are long.
                float textWidth = 196f - textLeft;
                Text name = UiKit.Label(card.transform, "Name", slug.Name, 27, TextAnchor.MiddleLeft, UiKit.TextColor);
                UiKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(textWidth, 36f), new Vector2(textLeft, -8f));
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                name.verticalOverflow = VerticalWrapMode.Truncate;
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 16;
                name.resizeTextMaxSize = 27;
                UiKit.Strong(name);
                UiKit.AddShadow(name, 1.5f);
                Text kind = UiKit.Label(card.transform, "Kind", UiText.Element(_content.ElementName((int)slug.Element)) + " - " + RoleCaption(slug),
                    18, TextAnchor.MiddleLeft, Color.Lerp(element, Color.white, 0.4f));
                UiKit.Place(kind.rectTransform, new Vector2(0f, 1f), new Vector2(textWidth, 28f), new Vector2(textLeft, -46f));
                kind.horizontalOverflow = HorizontalWrapMode.Wrap;
                kind.verticalOverflow = VerticalWrapMode.Truncate;
                kind.resizeTextForBestFit = true;
                kind.resizeTextMinSize = 11;
                kind.resizeTextMaxSize = 18;
                Button minus = UiKit.MakeButton(card.transform, "Minus", "-", new Vector2(46f, 46f), () => Change(index, -1),
                    ButtonStyle.Secondary, 44);
                UiKit.Place((RectTransform)minus.transform, new Vector2(1f, 0.5f), new Vector2(46f, 46f), new Vector2(-124f, 0f));
                Text count = UiKit.Label(card.transform, "Count", "0", 32, TextAnchor.MiddleCenter, UiKit.TextColor);
                UiKit.Place(count.rectTransform, new Vector2(1f, 0.5f), new Vector2(44f, 46f), new Vector2(-78f, 0f));
                UiKit.Strong(count);
                Button plus = UiKit.MakeButton(card.transform, "Plus", "+", new Vector2(46f, 46f), () => Change(index, 1),
                    ButtonStyle.Primary, 44);
                UiKit.Place((RectTransform)plus.transform, new Vector2(1f, 0.5f), new Vector2(46f, 46f), new Vector2(-14f, 0f));
                _countLabels.Add(count);
            }
            RefreshLoadout();
        }

        private static string RoleCaption(SlugInfo slug)
        {
            if (slug.FlashRadius > 0f)
            {
                return UiText.Blinding;
            }
            if (slug.RaisesWall)
            {
                return UiText.WallMaker;
            }
            return slug.Sticky ? UiText.Grenade : UiText.Role(slug.Role);
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
                    selected ? new Color(0.08f, 0.5f, 0.66f, 1f) : new Color(0.1f, 0.26f, 0.5f, 1f));
                _heroCards[i].Edge.color = selected ? UiKit.GoldColor : new Color(1f, 1f, 1f, 0f);
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
                ? UiText.Loadout + " · al azar"
                : UiText.Loadout + " · " + total + "/" + LoadoutSize;
            // The belt: one tile per chosen slug, in the colour of its element, with the slug in it.
            int tile = 0;
            for (int i = 0; i < _counts.Count && tile < LoadoutSize; i++)
            {
                Color element = Palette.Element(_content.ElementName((int)_content.Slug(i).Element));
                Texture2D portrait = SlugPortraits.Get(_content.Slug(i).Id);
                for (int n = 0; n < _counts[i] && tile < LoadoutSize; n++)
                {
                    _beltSlugs[tile].texture = portrait;
                    _beltSlugs[tile].enabled = portrait != null;
                    _belt[tile++].color = element;
                }
            }
            for (; tile < LoadoutSize; tile++)
            {
                _belt[tile].color = EmptyTile;
                _beltSlugs[tile].enabled = false;
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
