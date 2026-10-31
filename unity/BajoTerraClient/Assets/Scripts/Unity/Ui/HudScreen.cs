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
            public RawImage Face;
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
        private readonly Image _blind;
        private readonly Image _blindBurn;
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
        private readonly Text _critical;
        private readonly Image _courageBadge;
        private readonly Text _courageText;
        private readonly Text _grenadeWarning;

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
        private float _blindShown;
        private float _deathTime;
        private float _countdownPulse;
        private float _criticalTime = 99f;
        private int _lastCourage = -1;
        private int _lastGrenadeTenths = -1;
        private float _scoreboardTimer;
        private bool _debugVisible;
        private bool _statsVisible;
        private string _lastDebugSource;
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
            Image clock = UiKit.Window(Root, "Clock", UiKit.PanelColor, 16);
            UiKit.Place(clock.rectTransform, new Vector2(0.5f, 1f), new Vector2(184f, 76f), new Vector2(0f, -16f));
            _timer = UiKit.Label(clock.transform, "Timer", "0:00", 46, TextAnchor.MiddleCenter, UiKit.TextColor);
            UiKit.Stretch(_timer.rectTransform);
            _timer.rectTransform.offsetMin = new Vector2(0f, 2f);
            UiKit.Heading(_timer);
            Image roundTab = UiKit.Card(Root, "RoundTab", UiKit.Ink, 8);
            UiKit.Place(roundTab.rectTransform, new Vector2(0.5f, 1f), new Vector2(150f, 34f), new Vector2(0f, -88f));
            Image roundGlow = UiKit.Card(roundTab.transform, "Glow", Color.white, 6);
            UiKit.Stretch(roundGlow.rectTransform, 3f);
            UiKit.Gradient(roundGlow, new Color(0.45f, 1f, 0.92f), new Color(0.02f, 0.5f, 0.55f));
            _round = UiKit.Label(roundTab.transform, "Round", string.Empty, 22, TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(_round.rectTransform);
            _round.font = UiKit.DisplayFont;
            UiKit.Strong(_round);
            _capsules[0] = BuildCapsule(-1, "NOSOTROS", Palette.TeamOwn);
            _capsules[1] = BuildCapsule(1, "RIVALES", Palette.TeamOther);

            // ----- banners
            _banner = UiKit.Label(Root, "Banner", string.Empty, 112, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(_banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1600f, 160f), new Vector2(0f, 330f));
            UiKit.Fire(_banner);
            _bannerSub = UiKit.Label(Root, "BannerSub", string.Empty, 46, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(_bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1200f, 70f), new Vector2(0f, 240f));
            UiKit.Painted(_bannerSub, new Color(0.7f, 1f, 0.95f), new Color(0.15f, 0.8f, 0.78f));
            _countdown = UiKit.Label(Root, "Countdown", string.Empty, 240, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(_countdown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(600f, 300f), new Vector2(0f, 150f));
            UiKit.Fire(_countdown);

            // ----- around the crosshair: headshots and a grenade stuck to you
            _critical = UiKit.Label(Root, "Critical", UiText.Critical, 44, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(_critical.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(520f, 70f), new Vector2(0f, 86f));
            UiKit.Painted(_critical, UiKit.Lighter(Palette.Critical, 0.6f), UiKit.Darker(Palette.Critical, 0.25f));
            _critical.gameObject.SetActive(false);
            _grenadeWarning = UiKit.Label(Root, "Grenade", string.Empty, 50, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(_grenadeWarning.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1000f, 80f), new Vector2(0f, -150f));
            UiKit.Painted(_grenadeWarning, new Color(1f, 0.75f, 0.6f), Palette.Danger);
            _grenadeWarning.gameObject.SetActive(false);

            // ----- bottom centre: vitals
            _vitals = UiKit.Rect("Vitals", Root);
            UiKit.Place(_vitals, new Vector2(0.5f, 0f), new Vector2(VitalsWidth, 110f), new Vector2(0f, 148f));
            _heroName = UiKit.Label(_vitals, "Hero", string.Empty, 28, TextAnchor.MiddleLeft, Color.white);
            UiKit.Place(_heroName.rectTransform, new Vector2(0f, 1f), new Vector2(360f, 34f), new Vector2(4f, 4f));
            UiKit.Fire(_heroName);
            _over = new UiBar(_vitals, "Overshield", new Vector2(VitalsWidth, 8f), Palette.Overshield, 4);
            UiKit.Place(_over.Rect, new Vector2(0f, 1f), new Vector2(VitalsWidth, 8f), new Vector2(0f, -34f));
            _shield = new UiBar(_vitals, "Shield", new Vector2(VitalsWidth, 20f), Palette.Shield, 9, 17);
            UiKit.Place(_shield.Rect, new Vector2(0f, 1f), new Vector2(VitalsWidth, 20f), new Vector2(0f, -44f));
            _health = new UiBar(_vitals, "Health", new Vector2(VitalsWidth, 38f), Palette.Health, 14, 25);
            UiKit.Place(_health.Rect, new Vector2(0f, 1f), new Vector2(VitalsWidth, 38f), new Vector2(0f, -68f));
            _shield.SetGhostColor(new Color(0.8f, 0.95f, 1f, 0.6f));
            _health.SetGhostColor(new Color(1f, 0.85f, 0.5f, 0.7f));
            // Courage: a glowing badge above the hero's name while your team is outnumbered.
            _courageBadge = UiKit.Card(_vitals, "Courage", Color.white, 8);
            UiKit.Edge(_courageBadge.transform, UiKit.Ink, 8, 2);
            UiKit.Gradient(_courageBadge, UiKit.Lighter(Palette.Courage, 0.45f), UiKit.Darker(Palette.Courage, 0.2f));
            UiKit.Place(_courageBadge.rectTransform, new Vector2(0.5f, 1f), new Vector2(230f, 34f), new Vector2(0f, 46f));
            _courageText = UiKit.Label(_courageBadge.transform, "Text", string.Empty, 22, TextAnchor.MiddleCenter, Color.white);
            _courageText.font = UiKit.DisplayFont;
            UiKit.Strong(_courageText);
            UiKit.Stretch(_courageText.rectTransform);
            _courageBadge.gameObject.SetActive(false);
            for (int i = 0; i < MaxChips; i++)
            {
                Image chip = UiKit.Card(_vitals, "Chip " + i, Color.white, 8);
                UiKit.Edge(chip.transform, UiKit.Ink, 8, 2);
                UiKit.Place(chip.rectTransform, new Vector2(1f, 1f), new Vector2(126f, 26f), new Vector2(-i * 132f, 0f));
                Text caption = UiKit.Label(chip.transform, "Name", string.Empty, 15, TextAnchor.MiddleCenter, Color.white);
                UiKit.Strong(caption);
                UiKit.Stretch(caption.rectTransform);
                UiKit.AddShadow(caption, 1f);
                chip.gameObject.SetActive(false);
                _chips[i] = chip;
                _chipText[i] = caption;
            }

            // ----- the slug belt
            BuildSlots();

            // ----- bottom right: selected slug
            Image card = UiKit.Window(Root, "SlugCard", UiKit.PanelColor, 16);
            UiKit.Place(card.rectTransform, new Vector2(1f, 0f), new Vector2(350f, 190f), new Vector2(-30f, 30f));
            _slugAccent = UiKit.Card(card.transform, "Accent", UiKit.AccentColor, 4);
            UiKit.Place(_slugAccent.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f, 150f), new Vector2(14f, 0f));
            _slugAccent.rectTransform.pivot = new Vector2(0f, 0.5f);
            Text cardHeader = UiKit.Label(card.transform, "Header", "BABOSA ELEGIDA", 18, TextAnchor.MiddleLeft, UiKit.TextDim);
            UiKit.AddShadow(cardHeader, 1.2f);
            UiKit.Place(cardHeader.rectTransform, new Vector2(0f, 1f), new Vector2(300f, 24f), new Vector2(36f, -12f));
            _slugName = UiKit.Label(card.transform, "Name", string.Empty, 38, TextAnchor.MiddleLeft, Color.white);
            UiKit.Place(_slugName.rectTransform, new Vector2(0f, 1f), new Vector2(300f, 48f), new Vector2(36f, -34f));
            UiKit.Heading(_slugName);
            _slugKind = UiKit.Label(card.transform, "Kind", string.Empty, 21, TextAnchor.MiddleLeft, UiKit.TextDim);
            UiKit.AddShadow(_slugKind, 1.2f);
            UiKit.Place(_slugKind.rectTransform, new Vector2(0f, 1f), new Vector2(300f, 28f), new Vector2(36f, -80f));
            _slugReady = UiKit.Label(card.transform, "Ready", string.Empty, 20, TextAnchor.MiddleLeft, UiKit.TextColor);
            UiKit.AddShadow(_slugReady, 1.2f);
            UiKit.Place(_slugReady.rectTransform, new Vector2(0f, 1f), new Vector2(150f, 26f), new Vector2(36f, -112f));
            for (int i = 0; i < HudModel.SlotCount; i++)
            {
                Image pip = UiKit.Card(card.transform, "Pip " + i, UiKit.SuccessColor, 3);
                UiKit.Place(pip.rectTransform, new Vector2(1f, 1f), new Vector2(13f, 18f), new Vector2(-18f - (9 - i) * 17f, -114f));
                _belt[i] = pip;
            }
            Text blasterLabel = UiKit.Label(card.transform, "BlasterLabel", UiText.Blaster, 17, TextAnchor.MiddleLeft, UiKit.TextDim);
            UiKit.AddShadow(blasterLabel, 1.2f);
            UiKit.Place(blasterLabel.rectTransform, new Vector2(0f, 0f), new Vector2(100f, 20f), new Vector2(36f, 30f));
            _blaster = new UiBar(card.transform, "BlasterBar", new Vector2(290f, 20f), UiKit.GoldColor, 8, 14);
            UiKit.Place(_blaster.Rect, new Vector2(0f, 0f), new Vector2(290f, 20f), new Vector2(36f, 10f));

            // ----- top left: allies
            for (int i = 0; i < MaxAllies; i++)
            {
                _allies[i] = BuildAlly(i);
            }
            _debug = UiKit.Label(Root, "Debug", string.Empty, 21, TextAnchor.UpperLeft, new Color(0.8f, 1f, 0.9f));
            UiKit.Place(_debug.rectTransform, new Vector2(0f, 1f), new Vector2(560f, 160f), new Vector2(34f, -290f));
            UiKit.AddShadow(_debug, 1.5f);
            _debug.gameObject.SetActive(false);

            // ----- top right: kill feed
            for (int i = 0; i < MaxFeed; i++)
            {
                Image back = UiKit.Card(Root, "FeedBack " + i, new Color(0.03f, 0.12f, 0.2f, 0.85f), 10);
                UiKit.Place(back.rectTransform, new Vector2(1f, 1f), new Vector2(440f, 40f), new Vector2(-30f, -30f - i * 46f));
                UiKit.Edge(back.transform, Palette.WithAlpha(UiKit.Ink, 0.9f), 10, 2);
                Text line = UiKit.Label(back.transform, "Feed", string.Empty, 23, TextAnchor.MiddleRight, Color.white);
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
            Image spectateBack = UiKit.Window(_spectateBar, "Back", UiKit.PanelColor, 14);
            UiKit.Stretch(spectateBack.rectTransform);
            _spectateText = UiKit.Label(_spectateBar, "Text", string.Empty, 28, TextAnchor.MiddleCenter, UiKit.GoldColor);
            UiKit.Stretch(_spectateText.rectTransform);
            UiKit.Strong(_spectateText);
            _spectateBar.gameObject.SetActive(false);

            // ----- defeat panel
            _deathTint = UiKit.Panel(Root, "DeathTint", new Color(0.35f, 0f, 0f, 0f));
            _deathTint.sprite = UiSprites.Vignette();
            UiKit.Stretch(_deathTint.rectTransform);
            _deathTint.transform.SetAsFirstSibling();
            _death = UiKit.Rect("Death", Root);
            _deathGroup = _death.gameObject.AddComponent<CanvasGroup>();
            UiKit.Place(_death, new Vector2(0.5f, 0.5f), new Vector2(820f, 190f), new Vector2(0f, 20f));
            Image deathBack = UiKit.Window(_death, "Back", new Color(0.36f, 0.06f, 0.08f, 0.94f), 20);
            UiKit.Stretch(deathBack.rectTransform);
            _deathTitle = UiKit.Label(_death, "Title", UiText.Defeated, 76, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(_deathTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(780f, 92f), new Vector2(0f, -10f));
            UiKit.Painted(_deathTitle, new Color(1f, 0.72f, 0.6f), new Color(0.95f, 0.16f, 0.12f));
            _deathBadge = UiKit.Panel(_death, "Badge", Color.white);
            _deathBadge.sprite = UiSprites.Circle();
            UiKit.Place(_deathBadge.rectTransform, new Vector2(0.5f, 1f), new Vector2(26f, 26f), new Vector2(-170f, -118f));
            _deathBy = UiKit.Label(_death, "By", string.Empty, 34, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(_deathBy.rectTransform, new Vector2(0.5f, 1f), new Vector2(700f, 44f), new Vector2(18f, -104f));
            UiKit.Strong(_deathBy);
            _deathDetail = UiKit.Label(_death, "Detail", string.Empty, 22, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.8f));
            UiKit.AddShadow(_deathDetail, 1.2f);
            UiKit.Place(_deathDetail.rectTransform, new Vector2(0.5f, 1f), new Vector2(740f, 30f), new Vector2(0f, -150f));
            _death.gameObject.SetActive(false);

            // ----- Tab table (on top of everything else)
            _scoreboardLayer = UiKit.Rect("ScoreboardLayer", Root);
            UiKit.Stretch(_scoreboardLayer);
            Image dim = UiKit.Panel(_scoreboardLayer, "Dim", new Color(0f, 0.04f, 0.06f, 0.6f));
            UiKit.Stretch(dim.rectTransform);
            Text boardTitle = UiKit.Label(_scoreboardLayer, "Title", UiText.Scoreboard, 64, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(boardTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(700f, 84f), new Vector2(0f, -120f));
            UiKit.Fire(boardTitle);
            _scoreboard = new HudScoreboard(_scoreboardLayer);
            _scoreboardLayer.gameObject.SetActive(false);

            // ----- blinded by a flash: everything goes white, with a burnt-in glow that lingers as it fades
            _blind = UiKit.Panel(Root, "Blind", new Color(1f, 0.98f, 0.92f, 0f));
            UiKit.Stretch(_blind.rectTransform);
            _blind.raycastTarget = false;
            _blindBurn = UiKit.Panel(Root, "BlindBurn", new Color(1f, 0.93f, 0.62f, 0f));
            _blindBurn.sprite = UiSprites.Glow();
            UiKit.Place(_blindBurn.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1400f, 1100f), Vector2.zero);
            _blindBurn.raycastTarget = false;
        }

        /// <summary>How white the screen is right now because of a flash (0 none, 1 blinded); for tests.</summary>
        public float BlindShown
        {
            get { return _blindShown; }
        }

        // ------------------------------------------------------------------------------------------ public

        /// <summary>Shows or hides the full diagnostic readout (a developer key, not listed in the controls).</summary>
        public void ToggleDebug()
        {
            _debugVisible = !_debugVisible;
            _lastDebugSource = null;
            _debug.gameObject.SetActive(_debugVisible || _statsVisible);
        }

        /// <summary>Keeps the frame rate on screen (the "show FPS" option): only the first line of the readout.</summary>
        public void SetStatsVisible(bool visible)
        {
            _statsVisible = visible;
            _lastDebugSource = null;
            _debug.gameObject.SetActive(_debugVisible || _statsVisible);
        }

        /// <summary>Shows a big message in the middle of the screen for a few seconds.</summary>
        public void Banner(string text, Color color, float seconds = 2.2f, string sub = null)
        {
            _banner.text = text;
            _banner.color = Color.white;
            if (color == Color.white)
            {
                UiKit.Gradient(_banner, UiKit.FireTop, UiKit.FireBottom);
            }
            else
            {
                UiKit.Gradient(_banner, UiKit.Lighter(color, 0.55f), UiKit.Darker(color, 0.1f));
            }
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

        /// <summary>Pops "¡CRÍTICO!" over the crosshair: your slug hit an enemy in the head.</summary>
        public void Critical()
        {
            _criticalTime = 0f;
            _critical.gameObject.SetActive(true);
            _crosshair.Critical();
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
                _spectateText.text = "OBSERVANDO  -  " + name + "\n<size=20><color=#bfe9f2>" + UiText.SpectatingHint + "</color></size>";
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
            if ((_debugVisible || _statsVisible) && !ReferenceEquals(debugText, _lastDebugSource))
            {
                _lastDebugSource = debugText;
                int firstLine = debugText.IndexOf('\n');
                _debug.text = _debugVisible || firstLine < 0 ? debugText : debugText.Substring(0, firstLine);
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
                bool hurry = model.Phase == MatchPhase.RoundActive && model.SecondsLeft <= 10;
                _timer.color = hurry ? new Color(1f, 0.55f, 0.42f) : UiKit.TextColor;
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
                    w.Background.color = new Color(element.r * 0.62f, element.g * 0.62f, element.b * 0.62f, 0.96f);
                    w.Sheen.color = new Color(Mathf.Lerp(element.r, 1f, 0.4f), Mathf.Lerp(element.g, 1f, 0.4f),
                        Mathf.Lerp(element.b, 1f, 0.4f), 0.5f);
                    w.Name.text = view.Name;
                    Texture2D portrait = view.Id == null ? null : SlugPortraits.Get(view.Id);
                    w.Face.texture = portrait;
                    w.Face.enabled = portrait != null;
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
                    w.Border.color = view.Selected ? UiKit.GoldColor : Palette.WithAlpha(UiKit.Ink, 0.95f);
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
                _countdown.color = new Color(1f, 1f, 1f, 0.5f + 0.5f * (1f - _countdownPulse * 0.5f));
            }
            else if (_lastCountdownShown >= 0)
            {
                _lastCountdownShown = -1;
                _countdown.text = string.Empty;
            }
        }

        /// <summary>
        /// The white-out of a flash: it snaps to white the moment you are blinded and eases out with the blindness,
        /// the last of it a warm afterglow in the middle of the view, the way a bright light burns into your eyes.
        /// </summary>
        private void RefreshBlind(HudModel model, float deltaTime)
        {
            float target = model.BlindLevel;
            _blindShown = target > _blindShown ? target : Mathf.MoveTowards(_blindShown, target, deltaTime * 2.5f);
            float white = Mathf.Clamp01(_blindShown * 1.15f);
            _blind.color = new Color(1f, 0.98f, 0.92f, white * white * 0.985f);
            float burn = Mathf.Clamp01(_blindShown * 2.2f) * (1f - white * white * 0.6f);
            _blindBurn.color = new Color(1f, 0.93f, 0.62f, burn * 0.75f);
            _blindBurn.enabled = burn > 0.001f;
            _blind.enabled = white > 0.001f;
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

            RefreshCritical(deltaTime);
            RefreshCourage(model);
            RefreshGrenade(model);
            RefreshBlind(model, deltaTime);

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

        private void RefreshCritical(float deltaTime)
        {
            if (_criticalTime > 0.7f)
            {
                return;
            }
            _criticalTime += deltaTime;
            float t = Mathf.Clamp01(_criticalTime / 0.7f);
            float pop = 1f + 0.5f * Mathf.Clamp01(1f - _criticalTime / 0.12f);
            _critical.rectTransform.localScale = new Vector3(pop, pop, 1f);
            _critical.rectTransform.anchoredPosition = new Vector2(0f, 86f + 26f * t);
            _critical.color = new Color(1f, 1f, 1f, 1f - t * t);
            if (t >= 1f)
            {
                _critical.gameObject.SetActive(false);
            }
        }

        private void RefreshCourage(HudModel model)
        {
            int level = model.Alive ? model.Courage : 0;
            if (level != _lastCourage)
            {
                _lastCourage = level;
                _courageBadge.gameObject.SetActive(level > 0);
                _courageText.text = UiText.CourageBadge(level);
            }
            if (level > 0)
            {
                float beat = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * (5f + 2f * level));
                _courageBadge.rectTransform.localScale = new Vector3(beat, beat, 1f);
            }
        }

        private void RefreshGrenade(HudModel model)
        {
            bool show = model.Alive && model.GrenadeOnYou;
            if (_grenadeWarning.gameObject.activeSelf != show)
            {
                _grenadeWarning.gameObject.SetActive(show);
                _lastGrenadeTenths = -1;
            }
            if (!show)
            {
                return;
            }
            int tenths = Mathf.CeilToInt(model.GrenadeSeconds * 10f);
            if (tenths != _lastGrenadeTenths)
            {
                _lastGrenadeTenths = tenths;
                _grenadeWarning.text = UiText.GrenadeOnYou(tenths / 10f);
            }
            float urgency = Mathf.Lerp(6f, 16f, 1f - Mathf.Clamp01(model.GrenadeSeconds / 1.6f));
            float pulse = 1f + 0.08f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * urgency));
            _grenadeWarning.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
        }

        // ------------------------------------------------------------------------------------------ building

        private Capsule BuildCapsule(int side, string label, Color accent)
        {
            var capsule = new Capsule();
            Image back = UiKit.Window(Root, "Capsule " + label, Palette.WithAlpha(accent * 0.62f, 0.95f), 16);
            UiKit.Place(back.rectTransform, new Vector2(0.5f, 1f), new Vector2(330f, 66f), new Vector2(side * 266f, -20f));
            capsule.Label = UiKit.Label(back.transform, "Label", label, 22, side < 0 ? TextAnchor.UpperLeft : TextAnchor.UpperRight,
                Color.white);
            UiKit.Stretch(capsule.Label.rectTransform);
            capsule.Label.rectTransform.offsetMin = new Vector2(26f, 6f);
            capsule.Label.rectTransform.offsetMax = new Vector2(-26f, -9f);
            capsule.Label.font = UiKit.DisplayFont;
            UiKit.Strong(capsule.Label);
            capsule.Score = UiKit.Label(back.transform, "Score", "0", 54, side < 0 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft,
                Color.white);
            UiKit.Stretch(capsule.Score.rectTransform);
            capsule.Score.rectTransform.offsetMin = new Vector2(22f, 0f);
            capsule.Score.rectTransform.offsetMax = new Vector2(-22f, 0f);
            UiKit.Fire(capsule.Score);
            for (int i = 0; i < capsule.Pips.Length; i++)
            {
                Image pip = UiKit.Panel(back.transform, "Pip " + i, accent);
                pip.sprite = UiSprites.Circle();
                UiKit.AddOutlineTo(pip, 1.5f);
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
            Image back = UiKit.Window(Root, "Ally " + index, UiKit.PanelColor, 14);
            UiKit.Place(back.rectTransform, new Vector2(0f, 1f), new Vector2(330f, 84f), new Vector2(30f, -24f - index * 94f));
            card.Root = back.rectTransform;
            Image ring = UiKit.Panel(back.transform, "AvatarRing", UiKit.Ink);
            ring.sprite = UiSprites.Circle();
            UiKit.Place(ring.rectTransform, new Vector2(0f, 0.5f), new Vector2(62f, 62f), new Vector2(12f, 0f));
            ring.rectTransform.pivot = new Vector2(0f, 0.5f);
            card.Avatar = UiKit.Panel(ring.transform, "Avatar", Palette.TeamOwn);
            card.Avatar.sprite = UiSprites.Circle();
            UiKit.Stretch(card.Avatar.rectTransform, 4f);
            UiKit.Gradient(card.Avatar, Color.white, new Color(0.6f, 0.6f, 0.65f));
            card.Initial = UiKit.Label(card.Avatar.transform, "Initial", "?", 32, TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(card.Initial.rectTransform);
            UiKit.Heading(card.Initial);
            card.Name = UiKit.Label(back.transform, "Name", string.Empty, 25, TextAnchor.MiddleLeft, UiKit.TextColor);
            UiKit.Place(card.Name.rectTransform, new Vector2(0f, 1f), new Vector2(230f, 30f), new Vector2(86f, -9f));
            UiKit.Strong(card.Name);
            card.Health = new UiBar(back.transform, "Hp", new Vector2(222f, 16f), Palette.Health, 6);
            UiKit.Place(card.Health.Rect, new Vector2(0f, 1f), new Vector2(222f, 16f), new Vector2(88f, -42f));
            card.Shield = new UiBar(back.transform, "Shield", new Vector2(222f, 10f), Palette.Shield, 4);
            UiKit.Place(card.Shield.Rect, new Vector2(0f, 1f), new Vector2(222f, 10f), new Vector2(88f, -61f));
            card.Dim = UiKit.Card(back.transform, "Dim", new Color(0f, 0f, 0f, 0.62f), 14);
            UiKit.Stretch(card.Dim.rectTransform);
            card.Down = UiKit.Label(back.transform, "Down", "ABATIDO", 26, TextAnchor.MiddleRight, new Color(1f, 0.45f, 0.4f));
            card.Down.font = UiKit.DisplayFont;
            UiKit.Strong(card.Down);
            UiKit.Stretch(card.Down.rectTransform);
            card.Down.rectTransform.offsetMax = new Vector2(-18f, 0f);
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
                w.Sheen = UiKit.Card(w.Background.transform, "Sheen", new Color(1f, 1f, 1f, 0.3f), 10);
                UiKit.Gradient(w.Sheen, Color.white, new Color(1f, 1f, 1f, 0f));
                UiKit.Inset(w.Sheen.rectTransform, 4f, 0f, 4f, 4f);
                w.Sheen.rectTransform.anchorMin = new Vector2(0f, 0.45f);
                // The slug itself, its 3D model photographed for the tile.
                w.Face = new GameObject("Face", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                w.Face.transform.SetParent(w.Background.transform, false);
                UiKit.Place(w.Face.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(70f, 70f), new Vector2(4f, 8f));
                w.Face.raycastTarget = false;
                w.Face.enabled = false;
                w.Border = UiKit.Edge(w.Background.transform, Palette.WithAlpha(UiKit.Ink, 0.95f), 12, 4);
                w.Key = UiKit.Label(w.Background.transform, "Key", ((i + 1) % 10).ToString(), 24, TextAnchor.UpperLeft,
                    new Color(1f, 1f, 1f, 0.9f));
                UiKit.Stretch(w.Key.rectTransform, 8f);
                w.Key.font = UiKit.DisplayFont;
                UiKit.Strong(w.Key);
                w.Name = UiKit.Label(w.Background.transform, "Name", string.Empty, 17, TextAnchor.LowerCenter, UiKit.TextColor);
                UiKit.Stretch(w.Name.rectTransform, 5f);
                w.Name.horizontalOverflow = HorizontalWrapMode.Wrap;
                w.Name.verticalOverflow = VerticalWrapMode.Truncate;
                w.Name.resizeTextForBestFit = true;
                w.Name.resizeTextMinSize = 11;
                w.Name.resizeTextMaxSize = 18;
                UiKit.Strong(w.Name);
                w.Lock = UiKit.Card(w.Background.transform, "Lock", new Color(0.01f, 0.04f, 0.1f, 0.74f), 12);
                UiKit.Stretch(w.Lock.rectTransform);
                w.LockText = UiKit.Label(w.Lock.transform, "LockText", string.Empty, 48, TextAnchor.MiddleCenter, Color.white);
                UiKit.Heading(w.LockText);
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
