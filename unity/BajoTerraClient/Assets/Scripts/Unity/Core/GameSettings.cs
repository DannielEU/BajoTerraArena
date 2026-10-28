using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>Player preferences, kept in PlayerPrefs.</summary>
    public sealed class GameSettings
    {
        private const string Prefix = "bajoterra.";

        public string Host = "127.0.0.1";
        public int Port = 7777;
        public string PlayerName = "Jugador";
        public string RoomId = "arena";
        public string CharacterId = "eli";

        /// <summary>Radians of camera turn per pixel of mouse movement.</summary>
        public float LookSensitivity = 0.0025f;

        public bool InvertY;
        public bool Prediction = true;

        /// <summary>Master volume of the game, 0 to 1.</summary>
        public float MasterVolume = 0.85f;

        /// <summary>Volume of the cavern drone, 0 to 1.</summary>
        public float AmbienceVolume = 0.45f;

        /// <summary>Vertical field of view of the game camera, in degrees.</summary>
        public float FieldOfView = 72f;

        /// <summary>Whether the fps and ping readout stays on screen.</summary>
        public bool ShowStats;

        /// <summary>Path of the server jar used by "play against bots"; empty means search next to the project.</summary>
        public string ServerJar = string.Empty;

        public static GameSettings Load()
        {
            var settings = new GameSettings
            {
                Host = PlayerPrefs.GetString(Prefix + "host", "127.0.0.1"),
                Port = PlayerPrefs.GetInt(Prefix + "port", 7777),
                PlayerName = PlayerPrefs.GetString(Prefix + "name", "Jugador"),
                RoomId = PlayerPrefs.GetString(Prefix + "room", "arena"),
                CharacterId = PlayerPrefs.GetString(Prefix + "character", "eli"),
                LookSensitivity = PlayerPrefs.GetFloat(Prefix + "sensitivity", 0.0025f),
                InvertY = PlayerPrefs.GetInt(Prefix + "invertY", 0) == 1,
                Prediction = PlayerPrefs.GetInt(Prefix + "prediction", 1) == 1,
                MasterVolume = PlayerPrefs.GetFloat(Prefix + "volume", 0.85f),
                AmbienceVolume = PlayerPrefs.GetFloat(Prefix + "ambience", 0.45f),
                FieldOfView = PlayerPrefs.GetFloat(Prefix + "fov", 72f),
                ShowStats = PlayerPrefs.GetInt(Prefix + "stats", 0) == 1,
                ServerJar = PlayerPrefs.GetString(Prefix + "jar", string.Empty)
            };
            return settings;
        }

        public void Save()
        {
            PlayerPrefs.SetString(Prefix + "host", Host);
            PlayerPrefs.SetInt(Prefix + "port", Port);
            PlayerPrefs.SetString(Prefix + "name", PlayerName);
            PlayerPrefs.SetString(Prefix + "room", RoomId);
            PlayerPrefs.SetString(Prefix + "character", CharacterId);
            PlayerPrefs.SetFloat(Prefix + "sensitivity", LookSensitivity);
            PlayerPrefs.SetInt(Prefix + "invertY", InvertY ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "prediction", Prediction ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "volume", MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "ambience", AmbienceVolume);
            PlayerPrefs.SetFloat(Prefix + "fov", FieldOfView);
            PlayerPrefs.SetInt(Prefix + "stats", ShowStats ? 1 : 0);
            PlayerPrefs.SetString(Prefix + "jar", ServerJar);
            PlayerPrefs.Save();
        }
    }
}
