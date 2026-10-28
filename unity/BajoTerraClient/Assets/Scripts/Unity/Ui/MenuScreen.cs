using System;
using UnityEngine;
using UnityEngine.UI;

namespace Bajoterra.Unity
{
    /// <summary>
    /// The main menu: the game's name over a slowly turning view of the cavern, the four doors (Play, Controls,
    /// Settings, Quit) and, on the right, the page that the chosen door opens: the connection form, the key list or
    /// the options. A rotating tip at the bottom teaches the rules a few words at a time.
    /// </summary>
    public sealed class MenuScreen : UiScreen
    {
        private enum Page
        {
            Home,
            Play,
            Controls,
            Settings
        }

        private static readonly string[] Tips =
        {
            "Una babosa que disparas queda bloqueada durante los siguientes 3 disparos: reparte bien tu cinturón.",
            "Sube por las rampas para ganar altura: desde la plataforma ves y disparas por encima de la cobertura.",
            "Doc y Boon Doc curan a tus aliados. Apunta a un compañero herido para restaurar su vida.",
            "El escudo se agota antes que la vida: cuando está a cero, cada impacto cuenta el doble.",
            "Las coberturas bajas detienen los disparos rectos, pero una babosa lanzada en arco las sobrevuela.",
            "Rueda (Shift) para esquivar: durante un instante eres invulnerable.",
            "Las cápsulas de vida, escudo y sobreescudo reaparecen: controla el mapa y úsalas por turnos."
        };

        private readonly GameSettings _settings;
        private readonly RectTransform _homeButtons;
        private readonly Button[] _doors = new Button[4];
        private readonly RectTransform _playWindow;
        private readonly RectTransform _controlsWindow;
        private readonly RectTransform _settingsWindow;
        private readonly SettingsPanel _options;
        private readonly InputField _host;
        private readonly InputField _port;
        private readonly InputField _name;
        private readonly Text _status;
        private readonly Text _tip;
        private readonly Button _connect;
        private readonly Button _local;
        private float _tipTimer;
        private int _tipIndex;
        private bool _busy;
        private float _busyPhase;

        public event Action<string, int, string> ConnectClicked;

        public event Action<int, string> PlayLocalClicked;

        public event Action QuitClicked;

        /// <summary>Raised when an option changed (the game applies volume and field of view at once).</summary>
        public event Action SettingsChanged;

        public MenuScreen(UiRoot ui, GameSettings settings) : base(ui, "Menu")
        {
            _settings = settings;

            // A dark side panel with a feathered edge, so the title reads over the 3D backdrop.
            Image side = UiKit.Panel(Root, "Side", new Color(0.02f, 0.03f, 0.055f, 0.9f));
            side.rectTransform.anchorMin = new Vector2(0f, 0f);
            side.rectTransform.anchorMax = new Vector2(0f, 1f);
            side.rectTransform.pivot = new Vector2(0f, 0.5f);
            side.rectTransform.sizeDelta = new Vector2(760f, 0f);
            Image feather = UiKit.Panel(Root, "Feather", new Color(0.02f, 0.03f, 0.055f, 0.9f));
            feather.sprite = UiSprites.FadeRight();
            feather.rectTransform.anchorMin = new Vector2(0f, 0f);
            feather.rectTransform.anchorMax = new Vector2(0f, 1f);
            feather.rectTransform.pivot = new Vector2(0f, 0.5f);
            feather.rectTransform.sizeDelta = new Vector2(360f, 0f);
            feather.rectTransform.anchoredPosition = new Vector2(760f, 0f);

            // Logo.
            Text tag = UiKit.Label(Root, "Tag", "ARENA DE BABOSAS   -   3 CONTRA 3", 24, TextAnchor.MiddleLeft, UiKit.GoldColor);
            UiKit.Place(tag.rectTransform, new Vector2(0f, 1f), new Vector2(660f, 36f), new Vector2(110f, -92f));
            tag.fontStyle = FontStyle.Bold;
            Text title = UiKit.Label(Root, "Title", "BAJOTERRA", 132, TextAnchor.MiddleLeft, Color.white);
            UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(700f, 150f), new Vector2(104f, -126f));
            title.fontStyle = FontStyle.Bold;
            UiKit.AddOutline(title, 3f);
            Text title2 = UiKit.Label(title.transform, "Glow", "BAJOTERRA", 132, TextAnchor.MiddleLeft,
                new Color(0.22f, 0.72f, 1f, 0.35f));
            UiKit.Stretch(title2.rectTransform);
            title2.rectTransform.anchoredPosition = new Vector2(0f, -5f);
            title2.fontStyle = FontStyle.Bold;
            title2.transform.SetAsFirstSibling();
            Text sub = UiKit.Label(Root, "Subtitle", "A R E N A", 54, TextAnchor.MiddleLeft, UiKit.AccentColor);
            UiKit.Place(sub.rectTransform, new Vector2(0f, 1f), new Vector2(600f, 70f), new Vector2(114f, -268f));
            sub.fontStyle = FontStyle.Bold;
            UiKit.AddShadow(sub, 2f);

            // The four doors.
            _homeButtons = UiKit.Rect("Doors", Root);
            UiKit.Place(_homeButtons, new Vector2(0f, 0.5f), new Vector2(520f, 440f), new Vector2(110f, -70f));
            string[] captions = { "JUGAR", "CONTROLES", "AJUSTES", "SALIR" };
            ButtonStyle[] styles = { ButtonStyle.Primary, ButtonStyle.Secondary, ButtonStyle.Secondary, ButtonStyle.Danger };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                _doors[i] = UiKit.MakeButton(_homeButtons, "Door " + captions[i], captions[i], new Vector2(520f, 84f),
                    () => OnDoor(index), styles[i], 34);
                UiKit.Place((RectTransform)_doors[i].transform, new Vector2(0f, 1f), new Vector2(520f, 84f),
                    new Vector2(0f, -i * 112f));
            }

            // Tip and footer.
            _tip = UiKit.Label(Root, "Tip", string.Empty, 23, TextAnchor.MiddleLeft, UiKit.TextDim);
            UiKit.Place(_tip.rectTransform, new Vector2(0f, 0f), new Vector2(640f, 100f), new Vector2(110f, 70f));
            _tip.horizontalOverflow = HorizontalWrapMode.Wrap;
            Text footer = UiKit.Label(Root, "Footer", "Bajoterra Arena  -  servidor autoritativo en Java", 20,
                TextAnchor.MiddleLeft, new Color(0.45f, 0.52f, 0.65f));
            UiKit.Place(footer.rectTransform, new Vector2(0f, 0f), new Vector2(640f, 30f), new Vector2(110f, 30f));

            // Status line under the pages.
            _status = UiKit.Label(Root, "Status", string.Empty, 27, TextAnchor.MiddleCenter, UiKit.GoldColor);
            UiKit.Place(_status.rectTransform, new Vector2(1f, 0f), new Vector2(980f, 70f), new Vector2(-110f, 60f));
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.AddShadow(_status, 2f);

            // Page: play.
            _playWindow = Window("Play", 760f, 700f, "JUGAR");
            Text nameLabel = UiKit.Label(_playWindow, "NameLabel", "Tu nombre", 23, TextAnchor.MiddleLeft, UiKit.TextDim);
            UiKit.Place(nameLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(660f, 30f), new Vector2(0f, -112f));
            _name = UiKit.MakeField(_playWindow, "NameField", settings.PlayerName, new Vector2(660f, 58f));
            UiKit.Place((RectTransform)_name.transform, new Vector2(0.5f, 1f), new Vector2(660f, 58f), new Vector2(0f, -148f));
            _name.characterLimit = 16;
            _local = UiKit.MakeButton(_playWindow, "Local", "JUGAR CONTRA BOTS", new Vector2(660f, 92f), OnLocal,
                ButtonStyle.Primary, 32);
            UiKit.Place((RectTransform)_local.transform, new Vector2(0.5f, 1f), new Vector2(660f, 92f), new Vector2(0f, -240f));
            Text localHint = UiKit.Label(_playWindow, "LocalHint", "Arranca el servidor en tu ordenador y rellena los huecos con bots.",
                21, TextAnchor.MiddleCenter, UiKit.TextDim);
            UiKit.Place(localHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(660f, 30f), new Vector2(0f, -340f));
            Text or = UiKit.Label(_playWindow, "Or", "- o conéctate a un servidor -", 22, TextAnchor.MiddleCenter,
                new Color(0.5f, 0.58f, 0.72f));
            UiKit.Place(or.rectTransform, new Vector2(0.5f, 1f), new Vector2(660f, 30f), new Vector2(0f, -400f));
            _host = UiKit.MakeField(_playWindow, "HostField", settings.Host, new Vector2(440f, 56f));
            UiKit.Place((RectTransform)_host.transform, new Vector2(0f, 1f), new Vector2(440f, 56f), new Vector2(50f, -450f));
            _port = UiKit.MakeField(_playWindow, "PortField", settings.Port.ToString(), new Vector2(200f, 56f));
            UiKit.Place((RectTransform)_port.transform, new Vector2(1f, 1f), new Vector2(200f, 56f), new Vector2(-50f, -450f));
            _port.contentType = InputField.ContentType.IntegerNumber;
            _connect = UiKit.MakeButton(_playWindow, "Connect", "Conectar", new Vector2(660f, 68f), OnConnect,
                ButtonStyle.Secondary, 28);
            UiKit.Place((RectTransform)_connect.transform, new Vector2(0.5f, 1f), new Vector2(660f, 68f), new Vector2(0f, -530f));
            BackButton(_playWindow, -616f);

            // Page: controls.
            _controlsWindow = Window("Controls", 900f, 860f, "CONTROLES");
            BuildControls(_controlsWindow);
            BackButton(_controlsWindow, -776f);

            // Page: settings.
            _settingsWindow = Window("Settings", 860f, 800f, "AJUSTES");
            _options = new SettingsPanel(_settingsWindow, settings, 760f);
            UiKit.Place(_options.Root, new Vector2(0.5f, 1f), new Vector2(760f, 76f * 7f), new Vector2(0f, -112f));
            _options.Changed += () => SettingsChanged?.Invoke();
            BackButton(_settingsWindow, -716f);

            ShowPage(Page.Home);
            NextTip();
        }

        public void SetStatus(string message)
        {
            _status.text = message;
        }

        public void SetBusy(bool busy)
        {
            _busy = busy;
            _connect.interactable = !busy;
            _local.interactable = !busy;
            if (busy)
            {
                // Show the play page so the player sees what is happening.
                ShowPage(Page.Play);
            }
        }

        /// <summary>Goes back to the front page (after leaving a game).</summary>
        public void ShowHome()
        {
            ShowPage(Page.Home);
        }

        protected override void OnShown()
        {
            _options.Refresh();
        }

        protected override void OnTick(float deltaTime)
        {
            _tipTimer -= deltaTime;
            if (_tipTimer <= 0f)
            {
                NextTip();
            }
            if (_busy)
            {
                _busyPhase += deltaTime;
                int dots = 1 + (int)(_busyPhase * 2.5f) % 3;
                string baseText = _status.text.TrimEnd('.');
                _status.text = baseText + new string('.', dots);
            }
        }

        // ------------------------------------------------------------------------------------------ pages

        private RectTransform Window(string name, float width, float height, string title)
        {
            Image window = UiKit.Window(Root, name, UiKit.PanelColor, 22);
            UiKit.Place(window.rectTransform, new Vector2(1f, 0.5f), new Vector2(width, height), new Vector2(-110f, 20f));
            Text caption = UiKit.Label(window.transform, "Title", title, 40, TextAnchor.MiddleLeft, UiKit.TextColor);
            UiKit.Place(caption.rectTransform, new Vector2(0f, 1f), new Vector2(width - 80f, 60f), new Vector2(46f, -26f));
            caption.fontStyle = FontStyle.Bold;
            UiKit.AddShadow(caption, 2f);
            Image rule = UiKit.Panel(window.transform, "Rule", new Color(0.22f, 0.72f, 1f, 0.6f));
            UiKit.Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(120f, 4f), new Vector2(50f, -88f));
            window.gameObject.SetActive(false);
            return window.rectTransform;
        }

        private void BackButton(RectTransform window, float y)
        {
            Button back = UiKit.MakeButton(window, "Back", "Volver", new Vector2(300f, 60f), () => ShowPage(Page.Home),
                ButtonStyle.Ghost, 26);
            UiKit.Place((RectTransform)back.transform, new Vector2(0.5f, 1f), new Vector2(300f, 60f), new Vector2(0f, y));
        }

        private void ShowPage(Page page)
        {
            _playWindow.gameObject.SetActive(page == Page.Play);
            _controlsWindow.gameObject.SetActive(page == Page.Controls);
            _settingsWindow.gameObject.SetActive(page == Page.Settings);
            for (int i = 0; i < _doors.Length; i++)
            {
                bool current = (int)page - 1 == i;
                UiKit.Restyle(_doors[i], i == 0 ? ButtonStyle.Primary : i == 3 ? ButtonStyle.Danger
                    : current ? ButtonStyle.Success : ButtonStyle.Secondary);
            }
            if (page == Page.Settings)
            {
                _options.Refresh();
            }
        }

        private void OnDoor(int index)
        {
            switch (index)
            {
                case 0:
                    ShowPage(Page.Play);
                    break;
                case 1:
                    ShowPage(Page.Controls);
                    break;
                case 2:
                    ShowPage(Page.Settings);
                    break;
                default:
                    QuitClicked?.Invoke();
                    break;
            }
        }

        private void NextTip()
        {
            _tipTimer = 7f;
            _tipIndex = (_tipIndex + 1) % Tips.Length;
            _tip.text = "CONSEJO\n" + Tips[_tipIndex];
        }

        private static void BuildControls(RectTransform window)
        {
            string[,] rows =
            {
                { "W A S D", "Moverte" },
                { "Ratón", "Apuntar: la mira marca donde llega la babosa" },
                { "Clic izquierdo", "Disparar (mantén pulsado)" },
                { "1 - 0  /  Rueda", "Elegir babosa del cinturón" },
                { "Espacio", "Saltar" },
                { "C  /  Ctrl", "Agacharte (mantén)" },
                { "Shift", "Rodar: breve invulnerabilidad" },
                { "F", "Dash, según el personaje" },
                { "Tab", "Marcador de la partida" },
                { "Esc", "Pausa y opciones" },
                { "F3", "Información de depuración" }
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                float y = -112f - i * 54f;
                Image chip = UiKit.Card(window, "Key " + i, new Color(0.12f, 0.17f, 0.27f, 1f), 10);
                UiKit.Place(chip.rectTransform, new Vector2(0f, 1f), new Vector2(290f, 46f), new Vector2(46f, y));
                UiKit.Edge(chip.transform, new Color(1f, 1f, 1f, 0.16f), 10, 2);
                Text key = UiKit.Label(chip.transform, "Name", rows[i, 0], 23, TextAnchor.MiddleCenter, Color.white);
                key.fontStyle = FontStyle.Bold;
                UiKit.Stretch(key.rectTransform);
                Text what = UiKit.Label(window, "Action " + i, rows[i, 1], 24, TextAnchor.MiddleLeft, UiKit.TextColor);
                UiKit.Place(what.rectTransform, new Vector2(0f, 1f), new Vector2(520f, 46f), new Vector2(360f, y));
            }
        }

        // ------------------------------------------------------------------------------------------ actions

        private void OnConnect()
        {
            int.TryParse(_port.text, out int port);
            ConnectClicked?.Invoke(_host.text.Trim(), port > 0 ? port : 7777, _name.text.Trim());
        }

        private void OnLocal()
        {
            int.TryParse(_port.text, out int port);
            PlayLocalClicked?.Invoke(port > 0 ? port : 7777, _name.text.Trim());
        }
    }
}
