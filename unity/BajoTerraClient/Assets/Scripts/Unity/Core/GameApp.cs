using Bajoterra.Client.Session;
using Bajoterra.Client.Transport;
using Bajoterra.Protocol;
using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// The composition root and the screen flow of the game: menu, connecting, lobby, match, result. It owns the
    /// network client, the camera and light, the audio and the UI, and moves between screens as the client's phase
    /// changes. The match itself is delegated to a <see cref="MatchPresenter"/> created for each match; between
    /// matches a <see cref="MenuBackdrop"/> keeps the camera looking at something worth seeing.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        private const float MenuFieldOfView = 46f;

        public static GameApp Instance { get; private set; }

        private GameSettings _settings;
        private GameClient _client;
        private IControls _controls;
        private Camera _camera;
        private Light _sun;
        private UiRoot _ui;
        private AudioPlayer _audio;
        private MenuScreen _menu;
        private LobbyScreen _lobby;
        private HudScreen _hud;
        private OverlayScreen _overlay;
        private ResultScreen _result;
        private MatchPresenter _presenter;
        private MenuBackdrop _backdrop;
        private LocalServerLauncher _launcher;
        private bool _waitingForLocalServer;
        private bool _reportedCameraRepair;
        private int _localPort;
        private string _localName;
        private float _localTimeout;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            Instance = this;
            Application.runInBackground = true;
            _settings = GameSettings.Load();
            _controls = new UnityControls();
            _launcher = new LocalServerLauncher();

            CreateCamera();
            CreateLight();
            Atmosphere.Apply(_camera, _sun);
            _audio = new AudioPlayer(transform);
            ApplyAudioSettings();
            ButtonFx.Hovered = () => _audio.PlayUi(_audio.Hover, 0.5f);
            ButtonFx.Clicked = () => _audio.PlayUi(_audio.Click, 0.7f);

            _ui = new UiRoot(transform);
            _menu = new MenuScreen(_ui, _settings);
            _lobby = new LobbyScreen(_ui);
            _hud = new HudScreen(_ui);
            _overlay = new OverlayScreen(_ui, "Overlay", _settings);
            _result = new ResultScreen(_ui);

            _menu.ConnectClicked += (host, port, name) => BeginConnect(host, port, name);
            _menu.PlayLocalClicked += BeginLocal;
            _menu.QuitClicked += Quit;
            _menu.SettingsChanged += OnSettingsChanged;
            _overlay.SettingsChanged += OnSettingsChanged;
            _lobby.JoinClicked += OnJoin;
            _lobby.ReadyClicked += OnReady;
            _lobby.ReadyCancelled += () => _client.SetReady(false);
            _lobby.LeaveClicked += BackToMenu;

            ShowMenu(string.Empty);
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            double now = Time.realtimeSinceStartupAsDouble;
            EnsureCamera();
            PollLocalServer(deltaTime);
            _client?.Update(now);
            if (_presenter != null)
            {
                _presenter.Frame(deltaTime);
                if (_controls.PausePressed)
                {
                    TogglePause();
                }
            }
            else
            {
                _backdrop?.Tick(deltaTime);
            }
            _ui.Tick(deltaTime);
        }

        private void OnApplicationQuit()
        {
            Shutdown();
        }

        private void OnDestroy()
        {
            // Destroying the object without quitting (a scene reset, a test) must not leave the child server running.
            Shutdown();
            ButtonFx.Hovered = null;
            ButtonFx.Clicked = null;
        }

        private void Shutdown()
        {
            DisposeClient();
            _launcher?.Stop();
        }

        // ------------------------------------------------------------------------------------------ scene setup

        private void CreateCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            go.transform.SetParent(transform, false);
            _camera = go.GetComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Atmosphere.FogColor;
            _camera.fieldOfView = _settings.FieldOfView;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 250f;
            _camera.depth = 0f;
            _camera.cullingMask = ~0;
            _camera.targetDisplay = 0;
        }

        private void CreateLight()
        {
            var go = new GameObject("Skylight", typeof(Light));
            go.transform.SetParent(transform, false);
            _sun = go.GetComponent<Light>();
        }

        /// <summary>
        /// The Game view prints "Display 1 No cameras rendering" whenever no enabled camera draws to the display. The
        /// game keeps exactly one camera and looks after it every frame: it must exist, be active and enabled, draw
        /// everything to the screen (not to a texture) and sit on a valid transform. Anything else is put right, and
        /// said once in the console so the cause can be found.
        /// </summary>
        private void EnsureCamera()
        {
            bool recreated = false;
            if (_camera == null)
            {
                CreateCamera();
                Atmosphere.Apply(_camera, _sun);
                _presenter?.RebindCamera(_camera);
                recreated = true;
            }
            bool repaired = recreated;
            if (!_camera.gameObject.activeSelf)
            {
                _camera.gameObject.SetActive(true);
                repaired = true;
            }
            if (!_camera.enabled)
            {
                _camera.enabled = true;
                repaired = true;
            }
            if (_camera.targetTexture != null)
            {
                _camera.targetTexture = null;
                repaired = true;
            }
            if (_camera.targetDisplay != 0)
            {
                _camera.targetDisplay = 0;
                repaired = true;
            }
            if (_camera.cullingMask == 0)
            {
                _camera.cullingMask = ~0;
                repaired = true;
            }
            Vector3 position = _camera.transform.position;
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z))
            {
                _camera.transform.position = Vector3.zero;
                repaired = true;
            }
            if (repaired && !_reportedCameraRepair)
            {
                _reportedCameraRepair = true;
                Debug.LogWarning("Bajoterra: the game camera had to be repaired (it was missing, disabled, aimed at a "
                                 + "texture or on a bad transform).");
            }
        }

        // ------------------------------------------------------------------------------------------ settings

        private void ApplyAudioSettings()
        {
            _audio.MasterVolume = _settings.MasterVolume;
            _audio.AmbienceVolume = _settings.AmbienceVolume;
        }

        private void OnSettingsChanged()
        {
            ApplyAudioSettings();
            if (_presenter != null)
            {
                _camera.fieldOfView = _settings.FieldOfView;
                _hud.SetStatsVisible(_settings.ShowStats);
                _presenter.ApplyPrediction(_settings.Prediction);
            }
            _settings.Save();
        }

        // ------------------------------------------------------------------------------------------ backdrop

        private void EnsureBackdrop()
        {
            if (_backdrop == null)
            {
                _backdrop = new MenuBackdrop(transform, _camera);
            }
            _camera.fieldOfView = MenuFieldOfView;
        }

        private void DropBackdrop()
        {
            if (_backdrop != null)
            {
                _backdrop.Destroy();
                _backdrop = null;
            }
            _camera.fieldOfView = _settings.FieldOfView;
        }

        // ------------------------------------------------------------------------------------------ connecting

        private void BeginLocal(int port, string name)
        {
            if (!_launcher.Start(_settings.ServerJar, port, out string error))
            {
                _menu.SetStatus(error);
                return;
            }
            _waitingForLocalServer = true;
            _localPort = port;
            _localName = name;
            _localTimeout = 20f;
            _menu.SetBusy(true);
            _menu.SetStatus(UiText.StartingServer);
        }

        private void PollLocalServer(float deltaTime)
        {
            if (!_waitingForLocalServer)
            {
                return;
            }
            _localTimeout -= deltaTime;
            if (_launcher.Ready)
            {
                _waitingForLocalServer = false;
                BeginConnect("127.0.0.1", _localPort, _localName);
            }
            else if (_launcher.Exited || _localTimeout <= 0f)
            {
                // The process died (most likely the port is taken by a server that is already running): try it anyway.
                _waitingForLocalServer = false;
                BeginConnect("127.0.0.1", _localPort, _localName);
            }
        }

        private void BeginConnect(string host, int port, string name)
        {
            _settings.Host = host;
            _settings.Port = port;
            _settings.PlayerName = string.IsNullOrEmpty(name) ? "Jugador" : name;
            _settings.Save();

            DisposeClient();
            _client = new GameClient(new TcpClientTransport(), _settings.PlayerName);
            _client.Welcomed += OnWelcomed;
            _client.RoomUpdated += OnRoomUpdated;
            _client.MatchStarted += OnMatchStarted;
            _client.MatchEnded += OnMatchEnded;
            _client.ErrorReceived += OnServerError;
            _client.Disconnected += OnDisconnected;
            _menu.SetBusy(true);
            _menu.SetStatus(UiText.Connecting);
            _client.Connect(host, port);
        }

        private void DisposeClient()
        {
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }
        }

        // ------------------------------------------------------------------------------------------ client events

        private void OnWelcomed(Welcome welcome)
        {
            _menu.Hide();
            _menu.SetBusy(false);
            _lobby.ShowJoin(_settings.RoomId);
        }

        private void OnJoin(string room, int team)
        {
            _settings.RoomId = string.IsNullOrEmpty(room) ? "arena" : room;
            _settings.Save();
            _client.JoinRoom(_settings.RoomId, team);
        }

        private void OnReady(string characterId, string[] slugs)
        {
            _settings.CharacterId = characterId;
            _settings.Save();
            _client.SelectLoadout(characterId, slugs);
            _client.SetReady(true);
        }

        private void OnRoomUpdated(RoomState room)
        {
            if (_presenter != null)
            {
                return; // a match is running; the lobby is not on screen
            }
            if (!_lobby.RoomStepVisible && !_result.Visible)
            {
                _lobby.ShowRoom(_client.Content, _settings.CharacterId);
            }
            _lobby.UpdateRoom(room, _client.Content);
        }

        private void OnMatchStarted(MatchSession match)
        {
            _overlay.Hide();
            _result.Hide();
            _lobby.Hide();
            _menu.Hide();
            DropBackdrop();
            _presenter = new MatchPresenter(_client, _settings, _controls, _camera, _hud, _audio, transform);
            _hud.Show();
            SetCursorLocked(true);
        }

        private void OnMatchEnded(MatchEnd end)
        {
            EndPresenter();
            SetCursorLocked(false);
            MatchSession match = _client.Match;
            int own = match != null ? match.YourTeam : 0;
            int ours = own == 0 ? (int)end.WinsTeam0 : (int)end.WinsTeam1;
            int theirs = own == 0 ? (int)end.WinsTeam1 : (int)end.WinsTeam0;
            EnsureBackdrop();
            if (match == null)
            {
                _overlay.Present(UiText.Draw, Color.white, string.Empty, UiText.BackToRoom, BackToRoom,
                    UiText.BackToMenu, BackToMenu);
                return;
            }
            _audio.PlayUi(end.Winner == own ? _audio.Victory : _audio.Loss, 0.8f);
            _result.Present(match, end.Winner, ours, theirs, BackToRoom, BackToMenu);
        }

        private void OnServerError(ServerError error)
        {
            _lobby.ShowError(error.Message);
            _menu.SetStatus(error.Message);
            const uint InvalidLoadout = 4;
            const uint UnknownCharacter = 5;
            if (error.Code == InvalidLoadout || error.Code == UnknownCharacter)
            {
                // The server kept the previous loadout; do not let the match start with it by surprise.
                _client.SetReady(false);
                _lobby.ResetReady();
            }
        }

        private void OnDisconnected(string reason)
        {
            EndPresenter();
            SetCursorLocked(false);
            _hud.Hide();
            _lobby.Hide();
            _result.Hide();
            EnsureBackdrop();
            if (_menu.Visible)
            {
                _menu.SetBusy(false);
                _menu.SetStatus(reason);
                return;
            }
            _overlay.Present(UiText.Disconnected, Palette.TeamOther, reason, UiText.BackToMenu, BackToMenu);
        }

        // ------------------------------------------------------------------------------------------ navigation

        private void ShowMenu(string status)
        {
            EnsureBackdrop();
            _hud.Hide();
            _lobby.Hide();
            _overlay.Hide();
            _result.Hide();
            _menu.Show();
            _menu.ShowHome();
            _menu.SetBusy(false);
            _menu.SetStatus(status);
        }

        private void BackToMenu()
        {
            EndPresenter();
            SetCursorLocked(false);
            DisposeClient();
            _launcher.Stop();
            _waitingForLocalServer = false;
            ShowMenu(string.Empty);
        }

        private void BackToRoom()
        {
            _overlay.Hide();
            _result.Hide();
            _hud.Hide();
            EnsureBackdrop();
            _lobby.ShowRoom(_client.Content, _settings.CharacterId);
            if (_client.Room != null)
            {
                _lobby.UpdateRoom(_client.Room, _client.Content);
            }
        }

        private void TogglePause()
        {
            if (_presenter.Paused)
            {
                _presenter.Paused = false;
                _overlay.Hide();
                SetCursorLocked(true);
            }
            else
            {
                _presenter.Paused = true;
                SetCursorLocked(false);
                _overlay.PresentPause(TogglePause, BackToMenu);
            }
        }

        private void EndPresenter()
        {
            if (_presenter != null)
            {
                _presenter.Destroy();
                _presenter = null;
            }
            _overlay.Hide();
            _hud.Hide();
            EnsureBackdrop();
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
