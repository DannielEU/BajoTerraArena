using System;
using UnityEngine;
using UnityEngine.UI;

namespace Bajoterra.Unity
{
    /// <summary>
    /// The main menu: the game's logo on its metal banner over a slowly turning view of the cavern, the four doors
    /// (Play, Controls, Settings, Quit) and, on the right, the page that the chosen door opens: playing (straight into
    /// the arena, or joining a friend's game with the code they read out), the key list or the options. A rotating tip
    /// at the bottom teaches the rules a few words at a time.
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
            "Las cápsulas de vida, escudo y sobreescudo reaparecen: controla el mapa y úsalas por turnos.",
            "Apunta a la cabeza: un impacto directo en la cabeza es CRÍTICO y hace la mitad más de daño.",
            "Si tu equipo se queda en desventaja ganas CORAJE: más daño, disparo más rápido y escudo extra.",
            "Grenuke se pega donde choca y explota al poco: el círculo rojo marca hasta dónde llega. ¡Sal de él!",
            "Los lanzadores de la caverna se adaptan a ti: si pierdes una ronda aflojan, y si ganas, aprietan.",
            "Al hacer daño o curar se llena la barra de FUSIÓN: con Q disparas tu babosa y la siguiente a la vez.",
            "Tormato se convierte en un tornado: atrapa a tus rivales y desvía sus babosas. ¡Métete en el tuyo para saltar alto!",
            "Arachnet enreda a quien toca. Si falla, teje una telaraña trampa en el suelo. El fuego la quema.",
            "A mitad de ronda aparece el cristal de MEGAMORFOSIS en el centro: babosas gigantes durante unos segundos.",
            "Dispara una babosa de hielo a un tornado y se vuelve un TORNADO CONGELANTE que congela a quien atrapa.",
            "Bubbaleone encierra a su víctima en una burbuja que la eleva. Cualquier impacto la revienta.",
            "El lazo de Lariat arrastra a tu rival hasta ti, pero solo llega a media distancia.",
            "Pulsa B para bailar. Al ganar una ronda, tu equipo lo celebra bailando.",
            "En el aire, pegado a un muro, vuelve a pulsar Espacio: REBOTE EN PARED. Hasta 2 seguidos, cada uno en otro muro."
        };

        private readonly GameSettings _settings;
        private readonly RectTransform _homeButtons;
        private readonly Button[] _doors = new Button[4];
        private readonly RectTransform _playWindow;
        private readonly RectTransform _controlsWindow;
        private readonly RectTransform _settingsWindow;
        private readonly SettingsPanel _options;
        private readonly InputField _code;
        private readonly InputField _name;
        private readonly Text _status;
        private readonly Text _tip;
        private readonly Button _connect;
        private readonly Button _local;
        private readonly RectTransform _logo;
        private float _tipTimer;
        private int _tipIndex;
        private bool _busy;
        private float _busyPhase;
        private float _age;
        private string _statusBase = string.Empty;

        public event Action<string, int, string> ConnectClicked;

        public event Action<int, string> PlayLocalClicked;

        public event Action QuitClicked;

        /// <summary>Raised when an option changed (the game applies volume and field of view at once).</summary>
        public event Action SettingsChanged;

        public MenuScreen(UiRoot ui, GameSettings settings) : base(ui, "Menu")
        {
            _settings = settings;

            // A deep teal shade on the left with a feathered edge, so the logo and doors read over the cavern.
            Color shade = new Color(0.01f, 0.05f, 0.08f, 0.86f);
            Image side = UiKit.Panel(Root, "Side", shade);
            side.rectTransform.anchorMin = new Vector2(0f, 0f);
            side.rectTransform.anchorMax = new Vector2(0f, 1f);
            side.rectTransform.pivot = new Vector2(0f, 0.5f);
            side.rectTransform.sizeDelta = new Vector2(720f, 0f);
            Image feather = UiKit.Panel(Root, "Feather", shade);
            feather.sprite = UiSprites.FadeRight();
            feather.rectTransform.anchorMin = new Vector2(0f, 0f);
            feather.rectTransform.anchorMax = new Vector2(0f, 1f);
            feather.rectTransform.pivot = new Vector2(0f, 0.5f);
            feather.rectTransform.sizeDelta = new Vector2(420f, 0f);
            feather.rectTransform.anchoredPosition = new Vector2(720f, 0f);

            // Logo and tagline.
            _logo = UiKit.Logo(Root, UiText.Title, UiText.TitleTab, 680f, 124);
            UiKit.Place(_logo, new Vector2(0f, 1f), _logo.sizeDelta, new Vector2(70f, -46f));
            // Pivot in the middle so the drop-in bounce grows from the centre of the banner.
            _logo.pivot = new Vector2(0.5f, 0.5f);
            _logo.anchoredPosition = new Vector2(70f + _logo.sizeDelta.x * 0.5f, -46f - _logo.sizeDelta.y * 0.5f);
            Text tagline = UiKit.Label(Root, "Tagline", UiText.Tagline, 34, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(tagline.rectTransform, new Vector2(0f, 1f), new Vector2(680f, 50f), new Vector2(70f, -300f));
            UiKit.Fire(tagline);

            // The four doors.
            _homeButtons = UiKit.Rect("Doors", Root);
            UiKit.Place(_homeButtons, new Vector2(0f, 0.5f), new Vector2(520f, 420f), new Vector2(150f, 130f));
            _homeButtons.pivot = new Vector2(0f, 1f);
            string[] captions = { "JUGAR", "CONTROLES", "AJUSTES", "SALIR" };
            ButtonStyle[] styles = { ButtonStyle.Primary, ButtonStyle.Secondary, ButtonStyle.Secondary, ButtonStyle.Danger };
            float y = 0f;
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                bool main = i == 0;
                var size = new Vector2(main ? 520f : 470f, main ? 104f : 80f);
                _doors[i] = UiKit.MakeButton(_homeButtons, "Door " + captions[i], captions[i], size,
                    () => OnDoor(index), styles[i], main ? 50 : 36);
                UiKit.Place((RectTransform)_doors[i].transform, new Vector2(0f, 1f), size, new Vector2(main ? 0f : 25f, y));
                y -= size.y + 18f;
            }

            // Tip.
            Image tipPlate = UiKit.Window(Root, "TipPlate", new Color(0.05f, 0.2f, 0.3f, 0.9f), 14);
            UiKit.Place(tipPlate.rectTransform, new Vector2(0f, 0f), new Vector2(620f, 124f), new Vector2(100f, 46f));
            Text tipTag = UiKit.Tag(tipPlate.transform, "TipTag", "CONSEJO", new Vector2(170f, 40f));
            UiKit.Place((RectTransform)tipTag.transform.parent, new Vector2(0f, 1f), new Vector2(170f, 40f), new Vector2(22f, 22f));
            _tip = UiKit.Label(tipPlate.transform, "Tip", string.Empty, 24, TextAnchor.MiddleLeft, UiKit.TextColor);
            UiKit.Inset(_tip.rectTransform, 26f, 12f, 26f, 22f);
            _tip.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Status line under the pages.
            _status = UiKit.Label(Root, "Status", string.Empty, 30, TextAnchor.MiddleCenter, UiKit.GoldColor);
            UiKit.Place(_status.rectTransform, new Vector2(1f, 0f), new Vector2(980f, 70f), new Vector2(-110f, 50f));
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Strong(_status);

            // Page: play.
            _playWindow = Window("Play", 760f, 690f, "JUGAR");
            Text nameLabel = UiKit.Label(_playWindow, "NameLabel", UiText.PlayerName, 25, TextAnchor.MiddleLeft, UiKit.TextDim);
            UiKit.Place(nameLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(660f, 30f), new Vector2(0f, -118f));
            _name = UiKit.MakeField(_playWindow, "NameField", settings.PlayerName, new Vector2(660f, 62f));
            UiKit.Place((RectTransform)_name.transform, new Vector2(0.5f, 1f), new Vector2(660f, 62f), new Vector2(0f, -154f));
            _name.characterLimit = 16;
            _local = UiKit.MakeButton(_playWindow, "Local", UiText.QuickMatch, new Vector2(660f, 108f), OnLocal,
                ButtonStyle.Primary, 52);
            UiKit.Place((RectTransform)_local.transform, new Vector2(0.5f, 1f), new Vector2(660f, 108f), new Vector2(0f, -244f));
            Text localHint = UiKit.Label(_playWindow, "LocalHint", UiText.QuickMatchHint, 23, TextAnchor.MiddleCenter,
                UiKit.TextDim);
            UiKit.Place(localHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(660f, 30f), new Vector2(0f, -364f));

            Divider(_playWindow, UiText.JoinFriendDivider, -424f);
            _code = UiKit.MakeField(_playWindow, "CodeField", FriendCodeOf(settings), new Vector2(430f, 62f),
                UiText.FriendCode);
            UiKit.Place((RectTransform)_code.transform, new Vector2(0f, 1f), new Vector2(430f, 62f), new Vector2(50f, -470f));
            _connect = UiKit.MakeButton(_playWindow, "Connect", UiText.JoinFriend, new Vector2(214f, 68f), OnConnect,
                ButtonStyle.Secondary, 34);
            UiKit.Place((RectTransform)_connect.transform, new Vector2(1f, 1f), new Vector2(214f, 68f), new Vector2(-50f, -467f));
            BackButton(_playWindow, -582f);

            // Page: controls.
            _controlsWindow = Window("Controls", 900f, 800f, "CONTROLES");
            BuildControls(_controlsWindow);
            BackButton(_controlsWindow, -704f);

            // Page: settings.
            _settingsWindow = Window("Settings", 860f, 800f, "AJUSTES");
            _options = new SettingsPanel(_settingsWindow, settings, 760f);
            UiKit.Place(_options.Root, new Vector2(0.5f, 1f), new Vector2(760f, 76f * 7f), new Vector2(0f, -124f));
            _options.Changed += () => SettingsChanged?.Invoke();
            BackButton(_settingsWindow, -704f);

            ShowPage(Page.Home);
            NextTip();
        }

        public void SetStatus(string message)
        {
            _statusBase = message ?? string.Empty;
            _status.text = _statusBase;
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
            _age = 0f;
        }

        protected override void OnTick(float deltaTime)
        {
            _tipTimer -= deltaTime;
            if (_tipTimer <= 0f)
            {
                NextTip();
            }
            if (_busy && _statusBase.Length > 0)
            {
                _busyPhase += deltaTime;
                int dots = 1 + (int)(_busyPhase * 2.5f) % 3;
                _status.text = _statusBase + new string('.', dots);
            }

            // The logo drops in with a little bounce, then floats.
            _age += deltaTime;
            float t = Mathf.Clamp01(_age / 0.55f);
            float bounce = 1f + Mathf.Sin(t * Mathf.PI) * 0.12f * (1f - t);
            float scale = Mathf.Lerp(0.7f, 1f, 1f - (1f - t) * (1f - t)) * bounce;
            _logo.localScale = new Vector3(scale, scale, 1f);
            _logo.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_age * 0.8f) * 0.6f);
        }

        // ------------------------------------------------------------------------------------------ pages

        private RectTransform Window(string name, float width, float height, string title)
        {
            Image window = UiKit.Window(Root, name, UiKit.PanelColor, 24);
            UiKit.Place(window.rectTransform, new Vector2(1f, 0.5f), new Vector2(width, height), new Vector2(-110f, 30f));
            Text caption = UiKit.Label(window.transform, "Title", title, 60, TextAnchor.MiddleLeft, Color.white);
            UiKit.Place(caption.rectTransform, new Vector2(0f, 1f), new Vector2(width - 100f, 76f), new Vector2(50f, -22f));
            UiKit.Fire(caption);
            Image rule = UiKit.Card(window.transform, "Rule", UiKit.AccentColor, 3);
            UiKit.Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(width - 100f, 5f), new Vector2(50f, -98f));
            window.gameObject.SetActive(false);
            return window.rectTransform;
        }

        private static void Divider(RectTransform window, string caption, float y)
        {
            Text text = UiKit.Label(window, "Divider", caption, 23, TextAnchor.MiddleCenter, UiKit.AccentColor);
            UiKit.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(660f, 30f), new Vector2(0f, y));
            for (int side = -1; side <= 1; side += 2)
            {
                Image line = UiKit.Panel(window, "DividerLine", Palette.WithAlpha(UiKit.AccentColor, 0.45f));
                UiKit.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(120f, 3f), new Vector2(side * 270f, y - 14f));
            }
        }

        private void BackButton(RectTransform window, float y)
        {
            Button back = UiKit.MakeButton(window, "Back", "Volver", new Vector2(300f, 66f), () => ShowPage(Page.Home),
                ButtonStyle.Ghost, 32);
            UiKit.Place((RectTransform)back.transform, new Vector2(0.5f, 1f), new Vector2(300f, 66f), new Vector2(0f, y));
        }

        private void ShowPage(Page page)
        {
            _playWindow.gameObject.SetActive(page == Page.Play);
            _controlsWindow.gameObject.SetActive(page == Page.Controls);
            _settingsWindow.gameObject.SetActive(page == Page.Settings);
            for (int i = 1; i < 3; i++)
            {
                bool current = (int)page - 1 == i;
                UiKit.Restyle(_doors[i], current ? ButtonStyle.Success : ButtonStyle.Secondary);
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
            _tip.text = Tips[_tipIndex];
        }

        private static void BuildControls(RectTransform window)
        {
            string[,] rows =
            {
                { "W A S D", "Moverte" },
                { "Ratón", "Apuntar: la mira marca donde llega la babosa" },
                { "Clic izquierdo", "Disparar (mantén pulsado)" },
                { "1 - 5  /  Rueda", "Elegir babosa del cinturón" },
                { "Espacio", "Saltar; en el aire junto a un muro: rebote" },
                { "C  /  Ctrl", "Agacharte (mantén)" },
                { "Shift", "Rodar: breve invulnerabilidad" },
                { "F", "Dash, según el personaje" },
                { "Q  /  Clic derecho", "Disparo fusión: dos babosas a la vez" },
                { "Tab", "Marcador de la partida" },
                { "B", "Bailar para celebrar (otra vez: otro baile)" },
                { "Esc", "Pausa y opciones" }
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                float y = -120f - i * 48f;
                Image chip = UiKit.Card(window, "Key " + i, UiKit.Ink, 10);
                UiKit.Place(chip.rectTransform, new Vector2(0f, 1f), new Vector2(290f, 42f), new Vector2(50f, y));
                Image face = UiKit.Card(chip.transform, "Face", Color.white, 8);
                UiKit.Inset(face.rectTransform, 3f, 5f, 3f, 3f);
                UiKit.Gradient(face, new Color(0.45f, 0.74f, 1f), new Color(0.13f, 0.35f, 0.68f));
                Text key = UiKit.Label(face.transform, "Name", rows[i, 0], 24, TextAnchor.MiddleCenter, Color.white);
                UiKit.Strong(key);
                UiKit.Stretch(key.rectTransform);
                Text what = UiKit.Label(window, "Action " + i, rows[i, 1], 26, TextAnchor.MiddleLeft, UiKit.TextColor);
                UiKit.Place(what.rectTransform, new Vector2(0f, 1f), new Vector2(520f, 42f), new Vector2(366f, y));
                UiKit.AddShadow(what, 1.5f);
            }
        }

        // ------------------------------------------------------------------------------------------ actions

        /// <summary>The code to show in the friend field: the last friend's address, never this computer's own.</summary>
        private static string FriendCodeOf(GameSettings settings)
        {
            string host = settings.Host ?? string.Empty;
            if (host.Length == 0 || host == "127.0.0.1" || host == "localhost")
            {
                return string.Empty;
            }
            return settings.Port == LanAddress.DefaultPort ? host : host + ":" + settings.Port;
        }

        private void OnConnect()
        {
            LanAddress.Parse(_code.text, out string host, out int port);
            if (host.Length == 0)
            {
                SetStatus(UiText.FriendCode + "?");
                return;
            }
            ConnectClicked?.Invoke(host, port, _name.text.Trim());
        }

        private void OnLocal()
        {
            int port = _settings.Port > 0 ? _settings.Port : LanAddress.DefaultPort;
            PlayLocalClicked?.Invoke(port, _name.text.Trim());
        }
    }
}
