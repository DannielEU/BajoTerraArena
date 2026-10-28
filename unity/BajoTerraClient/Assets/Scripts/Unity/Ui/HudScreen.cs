using System.Text;
using Bajoterra.Client.Hud;
using Bajoterra.Client.Session;
using Bajoterra.Protocol;
using UnityEngine;
using UnityEngine.UI;

namespace Bajoterra.Unity
{
    /// <summary>
    /// The in-match interface. Bottom centre: your shield and health over the ten-slug belt. Bottom right: the selected
    /// slug and the blaster's wait. Bottom left: the minimap. Top centre: the score and the clock. Top left: your
    /// allies. Top right: the kill feed. In the middle: the crosshair with its recharge ring, banners and the
    /// countdown. When you are down, a panel says who got you and the camera follows a teammate. Holding Tab shows the
    /// full table. It only copies values from <see cref="HudModel"/> and <see cref="MatchStats"/> onto widgets and
    /// redraws text when a number actually changes.
    /// </summary>
    public sealed class HudScreen : UiScreen
    {
        private const int SlotWidth = 92;
        private const int SlotGap = 8;
        private const int MaxFeed = KillFeed.MaxEntries;
        private const int MaxChips = 6;
        private const int MaxAllies = 3;
        private const float VitalsWidth = 640f;

        private sealed class SlotWidgets
        {
            public RectTransform Holder;
            public Image Background;
            public Image Sheen;
            public Image Border;
            public Text Key;
            public Text Name;
            public Image Lock;
            public Text LockText;
            public int LastShots = -1;
            public int LastSlug = -1;
            public bool LastSelected;
        }

        private sealed class AllyCard
        {
            public RectTransform Root;
            public Image Avatar;
            public Text Initial;
            public Text Name;
            public UiBar Health;
            public UiBar Shield;
            public Image Dim;
            public Text Down;
            public int Id = -1;
        }

        private sealed class Capsule
        {
            public Text Label;
            public Text Score;
            public Image[] Pips = new Image[MaxAllies + 1];
        }

        private readonly SlotWidgets[] _slots = new SlotWidgets[HudModel.SlotCount];
        private readonly Image[] _feedBack = new Image[MaxFeed];
        private readonly Text[] _feed = new Text[MaxFeed];
        private readonly string[] _feedVictim = new string[MaxFeed];
        private readonly double[] _feedExpiry = new double[MaxFeed];
        private readonly Image[] _chips = new Image[MaxChips];
        private readonly Text[] _chipText = new Text[MaxChips];
        private readonly AllyCard[] _allies = new AllyCard[MaxAllies];
        private readonly Capsule[] _capsules = new Capsule[2];
        private readonly StringBuilder _builder = new StringBuilder(96);

        private readonly HudCrosshair _crosshair;
        private readonly HudMinimap _minimap;
        private readonly HudScoreboard _scoreboard;
        private readonly RectTransform _scoreboardLayer;
        private readonly RectTransform _vitals;
        private readonly UiBar _health;
        private readonly UiBar _shield;
        private readonly UiBar _over;
        private readonly Text _heroName;
        private readonly Text _timer;
        private readonly Text _round;
        private readonly Text _banner;
        private readonly Text _bannerSub;
        private readonly Text _countdown;
        private readonly Text _debug;
        private readonly Image _flash;
        private readonly Image _lowHealth;
        private readonly RectTransform _spectateBar;
        private readonly Text _spectateText;
        private readonly RectTransform _death;
        private readonly CanvasGroup _deathGroup;
        private readonly Image _deathTint;
        private readonly Text _deathTitle;
        private readonly Text _deathBy;
        private readonly Text _deathDetail;
        private readonly Image _deathBadge;
        private readonly Text _slugName;
        private readonly Text _slugKind;
        private readonly Text _slugReady;
        private readonly Image[] _belt = new Image[HudModel.SlotCount];
        private readonly UiBar _blaster;
        private readonly Image _slugAccent;

        private MatchSession _bound;
        private int _lastHp = -1;
        private int _lastShield = -1;
        private int _lastOver = -1;
        private int _lastSeconds = -1;
        private int _lastScoreKey = -1;
        private int _lastSelected = -2;
        private int _lastReady = -1;
        private int _lastCountdownShown = -1;
        private float _bannerTime;
        private float _bannerTotal = 1f;
        private float _flashAlpha;
        private float _deathTime;
        private float _countdownPulse;
        private float _scoreboardTimer;
        private bool _debugVisible;
        private bool _scoreboardVisible;
        private bool _dead;
        private bool _crosshairVisible = true;
        private string _spectated;

        public HudScreen(UiRoot ui) : base(ui, "Hud")
        {
            _flash = UiKit.Panel(Root, "DamageFlash", new Color(1f, 0.1f, 0.05f, 0f));
            _flash.sprite = UiSprites.Vignette();
            UiKit.Stretch(_flash.rectTransform);
            _lowHealth = UiKit.Panel(Root, "LowHealth", new Color(0.9f, 0.05f, 0.05f, 0f));
            _lowHealth.sprite = UiSprites.Vignette();
            UiKit.Stretch(_lowHealth.rectTransform);

            _crosshair = new HudCrosshair(Root);
            _minimap = new HudMinimap(Root);

            // ----- top centre: clock and score
            Image clock = UiKit.Window(Root, "Clock", new Color(0.03f, 0.05f, 0.09f, 0.9f), 16);
            UiKit.Place(clock.rectTransform, new Vector2(0.5f, 1f), new Vector2(176f, 70f), new Vector2(0f, -18f));
            _timer = UiKit.Label(clock.transform, "Timer", "0:00", 44, TextAnchor.MiddleCenter, UiKit.TextColor);
            _timer.fontStyle = FontStyle.Bold;
            UiKit.Stretch(_timer.rectTransform);
            UiKit.AddShadow(_timer, 2f);
            _round = UiKit.Label(Root, "Round", string.Empty, 21, TextAnchor.MiddleCenter, UiKit.TextDim);
            UiKit.Place(_round.rectTransform, new Vector2(0.5f, 1f), new Vector2(300f, 30f), new Vector2(0f, -94f));
            UiKit.AddShadow(_round, 1.5f);
            _capsules[0] = BuildCapsule(-1, "NOSOTROS", Palette.TeamOwn);
            _capsules[1] = BuildCapsule(1, "RIVALES", Palette.TeamOther);

            // ----- banners
            _banner = UiKit.Shadowed(Root, "Banner", string.Empty, 104, TextAnchor.MiddleCenter, Color.white, true);
            UiKit.Place(_banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1600f, 150f), new Vector2(0f, 330f));
            UiKit.AddOutline(_banner, 3f);
            _bannerSub = UiKit.Shadowed(Root, "BannerSub", string.Empty, 38, TextAnchor.MiddleCenter, UiKit.TextColor, true);
            UiKit.Place(_bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1200f, 60f), new Vector2(0f, 250f));
            _countdown = UiKit.Shadowed(Root, "Countdown", string.Empty, 230, TextAnchor.MiddleCenter, Color.white, true);
            UiKit.Place(_countdown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(600f, 280f), new Vector2(0f, 150f));
            UiKit.AddOutline(_countdown, 4f);

            // ----- bottom centre: vitals
            _vitals = UiKit.Rect("Vitals", Root);
            UiKit.Place(_vitals, new Vector2(0.5f, 0f), new Vector2(VitalsWidth, 110f), new Vector2(0f, 148f));
            _heroName = UiKit.Label(_vitals, "Hero", string.Empty, 25, TextAnchor.MiddleLeft, UiKit.GoldColor);
            UiKit.Place(_heroName.rectTransform, new Vector2(0f, 1f), new Vector2(300f, 30f), new Vector2(4f, 0f));
            _heroName.fontStyle = FontStyle.Bold;
            UiKit.AddShadow(_heroName, 1.5f);
            _over = new UiBar(_vitals, "Overshield", new Vector2(VitalsWidth, 8f), Palette.Overshield, 4);
            UiKit.Place(_over.Rect, new Vector2(0f, 1f), new Vector2(VitalsWidth, 8f), new Vector2(0f, -34f));
            _shield = new UiBar(_vitals, "Shield", new Vector2(VitalsWidth, 20f), Palette.Shield, 9, 17);
            UiKit.Place(_shield.Rect, new Vector2(0f, 1f), new Vector2(VitalsWidth, 20f), new Vector2(0f, -44f));
            _health = new UiBar(_vitals, "Health", new Vector2(VitalsWidth, 38f), Palette.Health, 14, 25);
            UiKit.Place(_health.Rect, new Vector2(0f, 1f), new Vector2(VitalsWidth, 38f), new Vector2(0f, -68f));
            _shield.SetGhostColor(new Color(0.8f, 0.95f, 1f, 0.6f));
            _health.SetGhostColor(new Color(1f, 0.85f, 0.5f, 0.7f));
            for (int i = 0; i < MaxChips; i++)
            {
                Image chip = UiKit.Card(_vitals, "Chip " + i, Color.white, 8);
                UiKit.Place(chip.rectTransform, new Vector2(1f, 1f), new Vector2(126f, 26f), new Vector2(-i * 132f, 0f));
                Text caption = UiKit.Label(chip.transform, "Name", string.Empty, 15, TextAnchor.MiddleCenter, Color.white);
                caption.fontStyle = FontStyle.Bold;
                UiKit.Stretch(caption.rectTransform);
                UiKit.AddShadow(caption, 1f);
                chip.gameObject.SetActive(false);
                _chips[i] = chip;
                _chipText[i] = caption;
            }

            // ----- the slug belt
            BuildSlots();

            // ----- bottom right: selected slug
            Image card = UiKit.Window(Root, "SlugCard", new Color(0.04f, 0.06f, 0.1f, 0.9f), 16);
            UiKit.Place(card.rectTransform, new Vector2(1f, 0f), new Vector2(350f, 190f), new Vector2(-30f, 30f));
            _slugAccent = UiKit.Card(card.transform, "Accent", UiKit.AccentColor, 4);
            UiKit.Place(_slugAccent.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f, 150f), new Vector2(14f, 0f));
            _slugAccent.rectTransform.pivot = new Vector2(0f, 0.5f);
            Text cardHeader = UiKit.Label(card.transform, "Header", "BABOSA ELEGIDA", 16, TextAnchor.MiddleLeft, UiKit.TextDim);
            UiKit.Place(cardHeader.rectTransform, new Vector2(0f, 1f), new Vector2(300f, 24f), new Vector2(36f, -12f));
            _slugName = UiKit.Label(card.transform, "Name", string.Empty, 36, TextAnchor.MiddleLeft, Color.white);
            UiKit.Place(_slugName.rectTransform, new Vector2(0f, 1f), new Vector2(300f, 46f), new Vector2(36f, -34f));
            _slugName.fontStyle = FontStyle.Bold;
            UiKit.AddShadow(_slugName, 2f);
            _slugKind = UiKit.Label(card.transform, "Kind", string.Empty, 20, TextAnchor.MiddleLeft, UiKit.TextDim);
            UiKit.Place(_slugKind.rectTransform, new Vector2(0f, 1f), new Vector2(300f, 28f), new Vector2(36f, -80f));
            _slugReady = UiKit.Label(card.transform, "Ready", string.Empty, 19, TextAnchor.MiddleLeft, UiKit.TextColor);
            UiKit.Place(_slugReady.rectTransform, new Vector2(0f, 1f), new Vector2(150f, 26f), new Vector2(36f, -112f));
            for (int i = 0; i < HudModel.SlotCount; i++)
            {
                Image pip = UiKit.Card(card.transform, "Pip " + i, UiKit.SuccessColor, 3);
                UiKit.Place(pip.rectTransform, new Vector2(1f, 1f), new Vector2(13f, 18f), new Vector2(-18f - (9 - i) * 17f, -114f));
                _belt[i] = pip;
            }
            Text blasterLabel = UiKit.Label(card.transform, "BlasterLabel", UiText.Blaster, 15, TextAnchor.MiddleLeft, UiKit.TextDim);
            UiKit.Place(blasterLabel.rectTransform, new Vector2(0f, 0f), new Vector2(100f, 20f), new Vector2(36f, 30f));
            _blaster = new UiBar(card.transform, "BlasterBar", new Vector2(290f, 20f), UiKit.GoldColor, 8, 14);
            UiKit.Place(_blaster.Rect, new Vector2(0f, 0f), new Vector2(290f, 20f), new Vector2(36f, 10f));

            // ----- top left: allies
            for (int i = 0; i < MaxAllies; i++)
            {
                _allies[i] = BuildAlly(i);
            }
            _debug = UiKit.Label(Root, "Debug", string.Empty, 20, TextAnchor.UpperLeft, new Color(0.8f, 1f, 0.8f));
            UiKit.Place(_debug.rectTransform, new Vector2(0f, 1f), new Vector2(560f, 160f), new Vector2(34f, -290f));
            UiKit.AddShadow(_debug, 1.5f);
            _debug.gameObject.SetActive(false);

            // ----- top right: kill feed
            for (int i = 0; i < MaxFeed; i++)
            {
                Image back = UiKit.Card(Root, "FeedBack " + i, new Color(0.02f, 0.03f, 0.06f, 0.78f), 10);
                UiKit.Place(back.rectTransform, new Vector2(1f, 1f), new Vector2(430f, 38f), new Vector2(-30f, -30f - i * 44f));
                Text line = UiKit.Label(back.transform, "Feed", string.Empty, 22, TextAnchor.MiddleRight, Color.white);
                line.supportRichText = true;
                UiKit.Stretch(line.rectTransform);
                line.rectTransform.offsetMax = new Vector2(-14f, 0f);
                UiKit.AddShadow(line, 1.2f);
                back.gameObject.SetActive(false);
                _feedBack[i] = back;
                _feed[i] = line;
            }

            // ----- spectating
            _spectateBar = UiKit.Rect("Spectate", Root);
            UiKit.Place(_spectateBar, new Vector2(0.5f, 0f), new Vector2(760f, 74f), new Vector2(0f, 270f));
            Image spectateBack = UiKit.Window(_spectateBar, "Back", new Color(0.04f, 0.06f, 0.1f, 0.9f), 14);
            UiKit.Stretch(spectateBack.rectTransform);
            _spectateText = UiKit.Label(_spectateBar, "Text", string.Empty, 26, TextAnchor.MiddleCenter, UiKit.GoldColor);
            UiKit.Stretch(_spectateText.rectTransform);
            _spectateText.fontStyle = FontStyle.Bold;
            _spectateBar.gameObject.SetActive(false);

            // ----- defeat panel
            _deathTint = UiKit.Panel(Root, "DeathTint", new Color(0.35f, 0f, 0f, 0f));
            _deathTint.sprite = UiSprites.Vignette();
            UiKit.Stretch(_deathTint.rectTransform);
            _deathTint.transform.SetAsFirstSibling();
            _death = UiKit.Rect("Death", Root);
            _deathGroup = _death.gameObject.AddComponent<CanvasGroup>();
            UiKit.Place(_death, new Vector2(0.5f, 0.5f), new Vector2(820f, 190f), new Vector2(0f, 20f));
            Image deathBack = UiKit.Window(_death, "Back", new Color(0.09f, 0.025f, 0.03f, 0.92f), 20);
            UiKit.Stretch(deathBack.rectTransform);
            UiKit.Edge(deathBack.transform, new Color(1f, 0.3f, 0.25f, 0.8f), 20, 3);
            _deathTitle = UiKit.Label(_death, "Title", UiText.Defeated, 68, TextAnchor.MiddleCenter, new Color(1f, 0.38f, 0.32f));
            UiKit.Place(_deathTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(780f, 84f), new Vector2(0f, -14f));
            _deathTitle.fontStyle = FontStyle.Bold;
            UiKit.AddOutline(_deathTitle, 2.5f);
            _deathBadge = UiKit.Panel(_death, "Badge", Color.white);
            _deathBadge.sprite = UiSprites.Circle();
            UiKit.Place(_deathBadge.rectTransform, new Vector2(0.5f, 1f), new Vector2(26f, 26f), new Vector2(-170f, -118f));
            _deathBy = UiKit.Label(_death, "By", string.Empty, 32, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(_deathBy.rectTransform, new Vector2(0.5f, 1f), new Vector2(700f, 44f), new Vector2(18f, -104f));
            _deathBy.fontStyle = FontStyle.Bold;
            UiKit.AddShadow(_deathBy, 2f);
            _deathDetail = UiKit.Label(_death, "Detail", string.Empty, 21, TextAnchor.MiddleCenter, UiKit.TextDim);
            UiKit.Place(_deathDetail.rectTransform, new Vector2(0.5f, 1f), new Vector2(740f, 30f), new Vector2(0f, -150f));
            _death.gameObject.SetActive(false);

            // ----- Tab table (on top of everything else)
            _scoreboardLayer = UiKit.Rect("ScoreboardLayer", Root);
            UiKit.Stretch(_scoreboardLayer);
            Image dim = UiKit.Panel(_scoreboardLayer, "Dim", new Color(0f, 0f, 0f, 0.55f));
            UiKit.Stretch(dim.rectTransform);
            Text boardTitle = UiKit.Shadowed(_scoreboardLayer, "Title", UiText.Scoreboard, 40, TextAnchor.MiddleCenter, Color.white, true);
            UiKit.Place(boardTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(600f, 60f), new Vector2(0f, -140f));
            _scoreboard = new HudScoreboard(_scoreboardLayer);
            _scoreboardLayer.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------------------------------ public

        public void ToggleDebug()
        {
            _debugVisible = !_debugVisible;
            _debug.gameObject.SetActive(_debugVisible);
        }

        /// <summary>Keeps the debug readout on screen (the "show fps and ping" option).</summary>
        public void SetStatsVisible(bool visible)
        {
            _debugVisible = visible;
            _debug.gameObject.SetActive(visible);
        }

        /// <summary>Shows a big message in the middle of the screen for a few seconds.</summary>
        public void Banner(string text, Color color, float seconds = 2.2f, string sub = null)
        {
            _banner.text = text;
            _banner.color = color;
            _bannerSub.text = sub ?? string.Empty;
            _bannerTime = seconds;
            _bannerTotal = Mathf.Max(0.1f, seconds);
        }

        public void Flash(float strength)
        {
            _flashAlpha = Mathf.Max(_flashAlpha, Mathf.Clamp01(0.3f + strength / 50f));
        }

        public void HitMarker(bool kill)
        {
            _crosshair.Hit(kill);
        }

        /// <summary>Shows or hides the table that Tab opens.</summary>
        public void SetScoreboard(bool visible)
        {
            if (visible == _scoreboardVisible)
            {
                return;
            }
            _scoreboardVisible = visible;
            _scoreboardLayer.gameObject.SetActive(visible);
            _scoreboardTimer = 0f;
        }

        /// <summary>
        /// Called every frame. Outside a match it is ignored; while spectating, <paramref name="name"/> is the player
        /// being followed.
        /// </summary>
        public void SetSpectating(string name)
        {
            bool spectating = name != null;
            if (_spectateBar.gameObject.activeSelf != spectating)
            {
                _spectateBar.gameObject.SetActive(spectating);
            }
            if (spectating && !string.Equals(name, _spectated))
            {
                _spectated = name;
                _spectateText.supportRichText = true;
                _spectateText.text = "OBSERVANDO  -  " + name + "\n<size=19>" + UiText.SpectatingHint + "</size>";
            }
            else if (!spectating)
            {
                _spectated = null;
            }
        }

        /// <summary>Shows or hides the defeat panel; the details say who got you and with what.</summary>
        public void SetDefeat(bool defeated, string attacker, Color attackerColor, string detail)
        {
            if (defeated && !_dead)
            {
                _deathTime = 0f;
            }
            _dead = defeated;
            _death.gameObject.SetActive(defeated);
            _crosshairVisible = !defeated;
            if (defeated)
            {
                _deathBy.text = attacker == null ? "Caíste" : UiText.DefeatedBy + "  " + attacker;
                _deathBadge.color = attackerColor;
                _deathBadge.gameObject.SetActive(attacker != null);
                _deathDetail.text = detail ?? string.Empty;
            }
        }

        /// <summary>Copies the model onto the widgets; call once per frame.</summary>
        public void Refresh(HudModel model, MatchSession match, float deltaTime, string debugText)
        {
            if (!ReferenceEquals(_bound, match))
            {
                Bind(match);
            }
            if (!model.HasState)
            {
                return;
            }
            RefreshVitals(model, deltaTime);
            RefreshScore(model, match);
            RefreshSlots(model);
            RefreshSlugCard(model, deltaTime);
            RefreshFeed(match);
            RefreshAllies(match, deltaTime);
            RefreshChips(match);
            RefreshBanner(deltaTime, model);
            RefreshOverlays(model, deltaTime);
            _crosshair.SetVisible(_crosshairVisible);
            _crosshair.Refresh(model.BlasterProgress, model.Slots[Mathf.Max(0, model.SelectedSlot)].Ready, deltaTime);
            _minimap.Refresh(match);
            if (_scoreboardVisible)
            {
                _scoreboardTimer -= deltaTime;
                if (_scoreboardTimer <= 0f)
                {
                    _scoreboardTimer = 0.25f;
                    _scoreboard.Refresh(match, model.WinsOwnTeam, model.WinsOtherTeam);
                }
            }
            if (_debugVisible)
            {
                _debug.text = debugText;
            }
        }

        protected override void OnShown()
        {
            _scoreboardLayer.gameObject.SetActive(false);
            _scoreboardVisible = false;
            _death.gameObject.SetActive(false);
            _spectateBar.gameObject.SetActive(false);
            _dead = false;
            _crosshairVisible = true;
        }

        // ------------------------------------------------------------------------------------------ binding

        /// <summary>Prepares the widgets that depend on who is in the match.</summary>
        private void Bind(MatchSession match)
        {
            _bound = match;
            _lastHp = _lastShield = _lastOver = _lastSeconds = _lastScoreKey = -1;
            _lastSelected = -2;
            _lastReady = -1;
            _lastCountdownShown = -1;
            _minimap.Build(match);
            int slot = 0;
            for (int i = 0; i < match.Slots.Count && slot < MaxAllies; i++)
            {
                if (i == match.YourId || match.Slots[i].Team != match.YourTeam)
                {
                    continue;
                }
                AllyCard card = _allies[slot++];
                card.Id = i;
                card.Root.gameObject.SetActive(true);
                card.Name.text = match.Slots[i].Name;
                CharacterStyle style = CharacterStyle.For(match.Slots[i].CharacterId);
                card.Avatar.color = style.Outfit;
                string hero = match.Content.FindCharacter(match.Slots[i].CharacterId)?.Name ?? match.Slots[i].CharacterId;
                card.Initial.text = hero.Length > 0 ? hero.Substring(0, 1).ToUpperInvariant() : "?";
            }
            for (; slot < MaxAllies; slot++)
            {
                _allies[slot].Id = -1;
                _allies[slot].Root.gameObject.SetActive(false);
            }
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].LastShots = -1;
                _slots[i].LastSlug = -1;
                _slots[i].LastSelected = false;
            }
        }

        // ------------------------------------------------------------------------------------------ parts

        private void RefreshVitals(HudModel model, float deltaTime)
        {
            _heroName.text = model.CharacterName ?? string.Empty;
            _health.Set(HudModel.Fraction(model.Hp, model.MaxHp), deltaTime);
            _shield.Set(HudModel.Fraction(model.Shield, model.MaxShield), deltaTime);
            _over.Set(HudModel.Fraction(model.Overshield, model.MaxShield), deltaTime);
            float hpFraction = HudModel.Fraction(model.Hp, model.MaxHp);
            _health.SetFillColor(hpFraction > 0.5f ? Palette.Health
                : hpFraction > 0.25f ? new Color(1f, 0.78f, 0.25f) : new Color(1f, 0.32f, 0.28f));
            int hp = Mathf.CeilToInt(model.Hp);
            int shield = Mathf.CeilToInt(model.Shield);
            int over = Mathf.CeilToInt(model.Overshield);
            if (hp != _lastHp || shield != _lastShield || over != _lastOver)
            {
                _lastHp = hp;
                _lastShield = shield;
                _lastOver = over;
                _health.SetText(hp + " / " + Mathf.CeilToInt(model.MaxHp));
                _shield.SetText(shield + " / " + Mathf.CeilToInt(model.MaxShield) + (over > 0 ? "   +" + over : string.Empty));
            }
        }

        private void RefreshScore(HudModel model, MatchSession match)
        {
            int scoreKey = model.Round * 10000 + model.WinsOwnTeam * 100 + model.WinsOtherTeam;
            if (scoreKey != _lastScoreKey)
            {
                _lastScoreKey = scoreKey;
                _capsules[0].Score.text = model.WinsOwnTeam.ToString();
                _capsules[1].Score.text = model.WinsOtherTeam.ToString();
                _round.text = UiText.Round.ToUpperInvariant() + " " + model.Round;
            }
            if (model.SecondsLeft != _lastSeconds)
            {
                _lastSeconds = model.SecondsLeft;
                _timer.text = HudModel.Clock(model.SecondsLeft);
                _timer.color = model.Phase == MatchPhase.RoundActive && model.SecondsLeft <= 10
                    ? new Color(1f, 0.5f, 0.4f) : UiKit.TextColor;
            }
            // Alive pips from the latest snapshot.
            Snapshot latest = match.Latest;
            int ownAlive = 0;
            int otherAlive = 0;
            int ownTotal = 0;
            int otherTotal = 0;
            for (int i = 0; i < match.Slots.Count; i++)
            {
                bool own = match.Slots[i].Team == match.YourTeam;
                bool alive = false;
                for (int k = 0; k < latest.Combatants.Count; k++)
                {
                    if (latest.Combatants[k].Id == (uint)i)
                    {
                        alive = latest.Combatants[k].Alive;
                        break;
                    }
                }
                if (own)
                {
                    ownTotal++;
                    ownAlive += alive ? 1 : 0;
                }
                else
                {
                    otherTotal++;
                    otherAlive += alive ? 1 : 0;
                }
            }
            SetPips(_capsules[0], ownTotal, ownAlive, Palette.TeamOwn);
            SetPips(_capsules[1], otherTotal, otherAlive, Palette.TeamOther);
        }

        private static void SetPips(Capsule capsule, int total, int alive, Color color)
        {
            for (int i = 0; i < capsule.Pips.Length; i++)
            {
                bool shown = i < total;
                capsule.Pips[i].gameObject.SetActive(shown);
                if (shown)
                {
                    capsule.Pips[i].color = i < alive ? color : new Color(0.25f, 0.27f, 0.32f, 0.9f);
                }
            }
        }

        private void RefreshSlots(HudModel model)
        {
            for (int i = 0; i < HudModel.SlotCount; i++)
            {
                SlotWidgets w = _slots[i];
                SlugSlotView view = model.Slots[i];
                if (view.SlugIndex != w.LastSlug)
                {
                    w.LastSlug = view.SlugIndex;
                    Color element = Palette.Element(view.Element);
                    w.Background.color = new Color(element.r * 0.5f, element.g * 0.5f, element.b * 0.5f, 0.95f);
                    w.Sheen.color = new Color(element.r, element.g, element.b, 0.45f);
                    w.Name.text = view.Name;
                }
                if (view.ShotsUntilReady != w.LastShots)
                {
                    w.LastShots = view.ShotsUntilReady;
                    w.Lock.gameObject.SetActive(!view.Ready);
                    w.LockText.text = view.Ready ? string.Empty : view.ShotsUntilReady.ToString();
                }
                if (view.Selected != w.LastSelected)
                {
                    w.LastSelected = view.Selected;
                    w.Border.color = view.Selected ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0.18f);
                    w.Holder.localScale = Vector3.one * (view.Selected ? 1.12f : 1f);
                    Vector2 position = w.Holder.anchoredPosition;
                    position.y = view.Selected ? 44f : 30f;
                    w.Holder.anchoredPosition = position;
                    w.Key.color = view.Selected ? Color.white : new Color(1f, 1f, 1f, 0.8f);
                }
            }
        }

        private void RefreshSlugCard(HudModel model, float deltaTime)
        {
            if (model.SelectedSlot != _lastSelected || model.ReadySlots != _lastReady)
            {
                _lastSelected = model.SelectedSlot;
                _lastReady = model.ReadySlots;
                if (model.SelectedSlot >= 0)
                {
                    SlugSlotView view = model.Slots[model.SelectedSlot];
                    Color element = Palette.Element(view.Element);
                    _slugName.text = view.Name;
                    _slugName.color = Color.Lerp(element, Color.white, 0.35f);
                    _slugAccent.color = element;
                    _slugKind.text = UiText.Element(view.Element) + "  -  " + UiText.Role(view.Role);
                }
                _slugReady.text = "Listas " + model.ReadySlots + "/" + HudModel.SlotCount;
                for (int i = 0; i < _belt.Length; i++)
                {
                    SlugSlotView view = model.Slots[i];
                    _belt[i].color = view.Ready ? Palette.Element(view.Element) : new Color(0.22f, 0.24f, 0.3f, 0.9f);
                }
            }
            _blaster.Set(model.BlasterProgress, deltaTime);
            _blaster.SetText(model.BlasterReady ? UiText.Ready2 : string.Empty);
            _blaster.SetFillColor(model.BlasterReady ? UiKit.SuccessColor : UiKit.GoldColor);
        }

        private void RefreshFeed(MatchSession match)
        {
            KillFeed feed = match.KillFeed;
            for (int i = 0; i < MaxFeed; i++)
            {
                if (i >= feed.Count)
                {
                    if (_feedVictim[i] != null)
                    {
                        _feedVictim[i] = null;
                        _feed[i].text = string.Empty;
                        _feedBack[i].gameObject.SetActive(false);
                    }
                    continue;
                }
                KillFeedEntry entry = feed[i];
                if (ReferenceEquals(_feedVictim[i], entry.Victim) && _feedExpiry[i] == entry.ExpiresAt)
                {
                    continue; // same line as last frame: no string to rebuild
                }
                _feedVictim[i] = entry.Victim;
                _feedExpiry[i] = entry.ExpiresAt;
                _feedBack[i].gameObject.SetActive(true);
                _builder.Length = 0;
                if (entry.Unattributed)
                {
                    _builder.Append("<color=#c9d4e6>-</color>");
                }
                else
                {
                    _builder.Append("<color=").Append(Hex(Palette.Team(entry.KillerTeam, match.YourTeam))).Append('>')
                        .Append(entry.Killer).Append("</color>");
                }
                _builder.Append("   <color=#ffd070>></color>   <color=").Append(Hex(Palette.Team(entry.VictimTeam, match.YourTeam)))
                    .Append('>').Append(entry.Victim).Append("</color>");
                _feed[i].text = _builder.ToString();
            }
        }

        private void RefreshAllies(MatchSession match, float deltaTime)
        {
            Snapshot latest = match.Latest;
            for (int a = 0; a < MaxAllies; a++)
            {
                AllyCard card = _allies[a];
                if (card.Id < 0)
                {
                    continue;
                }
                CombatantState state = null;
                for (int k = 0; k < latest.Combatants.Count; k++)
                {
                    if (latest.Combatants[k].Id == (uint)card.Id)
                    {
                        state = latest.Combatants[k];
                    }
                }
                Slot info = match.Slots[card.Id];
                bool alive = state != null && state.Alive;
                card.Health.Set(state == null ? 0f : HudModel.Fraction(state.Hp, info.MaxHp), deltaTime);
                card.Shield.Set(state == null ? 0f : HudModel.Fraction(state.Shield, info.MaxShield), deltaTime);
                card.Dim.gameObject.SetActive(!alive);
                card.Down.gameObject.SetActive(!alive);
            }
        }

        private void RefreshChips(MatchSession match)
        {
            CombatantState you = match.YourState;
            int count = you == null ? 0 : Mathf.Min(you.Statuses.Count, MaxChips);
            for (int i = 0; i < MaxChips; i++)
            {
                bool active = i < count;
                _chips[i].gameObject.SetActive(active);
                if (active)
                {
                    string id = you.Statuses[i].Id;
                    Color color = Palette.Status(id);
                    _chips[i].color = new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 0.95f);
                    _chipText[i].text = UiText.Status(id);
                }
            }
        }

        private void RefreshBanner(float deltaTime, HudModel model)
        {
            if (_bannerTime > 0f)
            {
                _bannerTime -= deltaTime;
                float t = Mathf.Clamp01(_bannerTime / 0.6f);
                float pop = 1f + 0.18f * Mathf.Clamp01((_bannerTime - (_bannerTotal - 0.25f)) / 0.25f);
                Color color = _banner.color;
                color.a = t;
                _banner.color = color;
                _banner.rectTransform.localScale = new Vector3(pop, pop, 1f);
                Color sub = _bannerSub.color;
                sub.a = t;
                _bannerSub.color = sub;
                if (_bannerTime <= 0f)
                {
                    _banner.text = string.Empty;
                    _bannerSub.text = string.Empty;
                }
            }

            // The countdown before each round: a big number that pops every second.
            bool counting = model.Phase == MatchPhase.Countdown && model.SecondsLeft > 0;
            if (counting)
            {
                if (model.SecondsLeft != _lastCountdownShown)
                {
                    _lastCountdownShown = model.SecondsLeft;
                    _countdown.text = model.SecondsLeft.ToString();
                    _countdownPulse = 1f;
                }
                _countdownPulse = Mathf.MoveTowards(_countdownPulse, 0f, deltaTime * 2.2f);
                float scale = 1f + _countdownPulse * 0.45f;
                _countdown.rectTransform.localScale = new Vector3(scale, scale, 1f);
                _countdown.color = new Color(1f, 1f, 1f, 0.35f + 0.65f * (1f - _countdownPulse * 0.5f));
            }
            else if (_lastCountdownShown >= 0)
            {
                _lastCountdownShown = -1;
                _countdown.text = string.Empty;
            }
        }

        private void RefreshOverlays(HudModel model, float deltaTime)
        {
            if (_flashAlpha > 0f)
            {
                _flashAlpha = Mathf.Max(0f, _flashAlpha - deltaTime * 1.5f);
            }
            _flash.color = new Color(1f, 0.1f, 0.05f, _flashAlpha * 0.85f);

            float fraction = HudModel.Fraction(model.Hp, model.MaxHp);
            float low = model.Alive && fraction < 0.3f ? (0.3f - fraction) / 0.3f : 0f;
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * (4f + low * 4f));
            _lowHealth.color = new Color(0.9f, 0.05f, 0.05f, low * pulse * 0.7f);

            if (_dead)
            {
                _deathTime += deltaTime;
                float rise = Mathf.Clamp01(_deathTime / 0.5f);
                float ease = 1f - (1f - rise) * (1f - rise);
                _death.localScale = Vector3.one * (0.8f + 0.2f * ease);
                _deathGroup.alpha = ease;
                _deathTint.color = new Color(0.4f, 0f, 0f, 0.42f * ease);
            }
            else
            {
                _deathTint.color = new Color(0.4f, 0f, 0f, 0f);
            }
        }

        // ------------------------------------------------------------------------------------------ building

        private Capsule BuildCapsule(int side, string label, Color accent)
        {
            var capsule = new Capsule();
            Image back = UiKit.Window(Root, "Capsule " + label, Palette.WithAlpha(accent * 0.28f, 0.92f), 16);
            UiKit.Place(back.rectTransform, new Vector2(0.5f, 1f), new Vector2(330f, 62f), new Vector2(side * 262f, -22f));
            UiKit.Edge(back.transform, Palette.WithAlpha(accent, 0.85f), 16, 3);
            capsule.Label = UiKit.Label(back.transform, "Label", label, 19, side < 0 ? TextAnchor.UpperLeft : TextAnchor.UpperRight,
                Color.Lerp(accent, Color.white, 0.55f));
            UiKit.Stretch(capsule.Label.rectTransform);
            capsule.Label.rectTransform.offsetMin = new Vector2(26f, 6f);
            capsule.Label.rectTransform.offsetMax = new Vector2(-26f, -9f);
            capsule.Label.fontStyle = FontStyle.Bold;
            capsule.Score = UiKit.Label(back.transform, "Score", "0", 50, side < 0 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft,
                Color.white);
            UiKit.Stretch(capsule.Score.rectTransform);
            capsule.Score.rectTransform.offsetMin = new Vector2(22f, 0f);
            capsule.Score.rectTransform.offsetMax = new Vector2(-22f, 0f);
            capsule.Score.fontStyle = FontStyle.Bold;
            UiKit.AddShadow(capsule.Score, 2f);
            for (int i = 0; i < capsule.Pips.Length; i++)
            {
                Image pip = UiKit.Panel(back.transform, "Pip " + i, accent);
                pip.sprite = UiSprites.Circle();
                float x = side < 0 ? 28f + i * 19f : -(28f + i * 19f);
                UiKit.Place(pip.rectTransform, side < 0 ? new Vector2(0f, 0f) : new Vector2(1f, 0f), new Vector2(14f, 14f),
                    new Vector2(x, 9f));
                capsule.Pips[i] = pip;
            }
            return capsule;
        }

        private AllyCard BuildAlly(int index)
        {
            var card = new AllyCard();
            Image back = UiKit.Window(Root, "Ally " + index, new Color(0.03f, 0.05f, 0.09f, 0.86f), 14);
            UiKit.Place(back.rectTransform, new Vector2(0f, 1f), new Vector2(320f, 80f), new Vector2(30f, -26f - index * 90f));
            card.Root = back.rectTransform;
            UiKit.Edge(back.transform, Palette.WithAlpha(Palette.TeamOwn, 0.7f), 14, 2);
            card.Avatar = UiKit.Panel(back.transform, "Avatar", Palette.TeamOwn);
            card.Avatar.sprite = UiSprites.Circle();
            UiKit.Place(card.Avatar.rectTransform, new Vector2(0f, 0.5f), new Vector2(54f, 54f), new Vector2(14f, 0f));
            card.Avatar.rectTransform.pivot = new Vector2(0f, 0.5f);
            card.Initial = UiKit.Label(card.Avatar.transform, "Initial", "?", 30, TextAnchor.MiddleCenter, Color.white);
            card.Initial.fontStyle = FontStyle.Bold;
            UiKit.Stretch(card.Initial.rectTransform);
            UiKit.AddShadow(card.Initial, 1.5f);
            card.Name = UiKit.Label(back.transform, "Name", string.Empty, 24, TextAnchor.MiddleLeft, UiKit.TextColor);
            UiKit.Place(card.Name.rectTransform, new Vector2(0f, 1f), new Vector2(230f, 30f), new Vector2(80f, -8f));
            card.Name.fontStyle = FontStyle.Bold;
            UiKit.AddShadow(card.Name, 1.5f);
            card.Health = new UiBar(back.transform, "Hp", new Vector2(226f, 14f), Palette.Health, 6);
            UiKit.Place(card.Health.Rect, new Vector2(0f, 1f), new Vector2(226f, 14f), new Vector2(80f, -42f));
            card.Shield = new UiBar(back.transform, "Shield", new Vector2(226f, 9f), Palette.Shield, 4);
            UiKit.Place(card.Shield.Rect, new Vector2(0f, 1f), new Vector2(226f, 9f), new Vector2(80f, -60f));
            card.Dim = UiKit.Card(back.transform, "Dim", new Color(0f, 0f, 0f, 0.62f), 14);
            UiKit.Stretch(card.Dim.rectTransform);
            card.Down = UiKit.Label(back.transform, "Down", "ABATIDO", 22, TextAnchor.MiddleRight, new Color(1f, 0.45f, 0.4f));
            card.Down.fontStyle = FontStyle.Bold;
            UiKit.Stretch(card.Down.rectTransform);
            card.Down.rectTransform.offsetMax = new Vector2(-18f, 0f);
            UiKit.AddShadow(card.Down, 1.5f);
            card.Root.gameObject.SetActive(false);
            return card;
        }

        private void BuildSlots()
        {
            float total = HudModel.SlotCount * SlotWidth + (HudModel.SlotCount - 1) * SlotGap;
            for (int i = 0; i < HudModel.SlotCount; i++)
            {
                var w = new SlotWidgets();
                RectTransform holder = UiKit.Rect("Slot " + i, Root);
                UiKit.Place(holder, new Vector2(0.5f, 0f), new Vector2(SlotWidth, 92f),
                    new Vector2(-total * 0.5f + SlotWidth * 0.5f + i * (SlotWidth + SlotGap), 30f));
                holder.pivot = new Vector2(0.5f, 0f);
                w.Holder = holder;
                w.Background = UiKit.Card(holder, "Background", new Color(0.2f, 0.2f, 0.25f, 0.95f), 12);
                UiKit.Stretch(w.Background.rectTransform);
                w.Sheen = UiKit.Panel(w.Background.transform, "Sheen", new Color(1f, 1f, 1f, 0.3f));
                w.Sheen.sprite = UiSprites.Gradient();
                UiKit.Stretch(w.Sheen.rectTransform, 3f);
                w.Border = UiKit.Edge(w.Background.transform, new Color(1f, 1f, 1f, 0.18f), 12, 3);
                w.Key = UiKit.Label(w.Background.transform, "Key", ((i + 1) % 10).ToString(), 21, TextAnchor.UpperLeft,
                    new Color(1f, 1f, 1f, 0.8f));
                UiKit.Stretch(w.Key.rectTransform, 8f);
                w.Key.fontStyle = FontStyle.Bold;
                UiKit.AddShadow(w.Key, 1.2f);
                w.Name = UiKit.Label(w.Background.transform, "Name", string.Empty, 17, TextAnchor.LowerCenter, UiKit.TextColor);
                UiKit.Stretch(w.Name.rectTransform, 5f);
                w.Name.horizontalOverflow = HorizontalWrapMode.Wrap;
                w.Name.verticalOverflow = VerticalWrapMode.Truncate;
                w.Name.resizeTextForBestFit = true;
                w.Name.resizeTextMinSize = 11;
                w.Name.resizeTextMaxSize = 18;
                w.Name.fontStyle = FontStyle.Bold;
                UiKit.AddShadow(w.Name, 1.2f);
                w.Lock = UiKit.Card(w.Background.transform, "Lock", new Color(0f, 0f, 0f, 0.72f), 12);
                UiKit.Stretch(w.Lock.rectTransform);
                w.LockText = UiKit.Label(w.Lock.transform, "LockText", string.Empty, 44, TextAnchor.MiddleCenter, Color.white);
                w.LockText.fontStyle = FontStyle.Bold;
                UiKit.Stretch(w.LockText.rectTransform);
                w.Lock.gameObject.SetActive(false);
                _slots[i] = w;
            }
        }

        private static string Hex(Color color)
        {
            Color32 c = color;
            return "#" + c.r.ToString("x2") + c.g.ToString("x2") + c.b.ToString("x2");
        }
    }
}
