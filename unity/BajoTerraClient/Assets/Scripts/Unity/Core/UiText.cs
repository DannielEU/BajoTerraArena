namespace Bajoterra.Unity
{
    /// <summary>Every string shown to the player, in one place (Spanish).</summary>
    public static class UiText
    {
        public const string Title = "BAJOTERRA ARENA";
        public const string Connect = "Conectar";
        public const string PlayLocal = "Jugar contra bots (servidor local)";
        public const string Quit = "Salir";
        public const string Host = "Servidor";
        public const string Port = "Puerto";
        public const string PlayerName = "Nombre";
        public const string Connecting = "Conectando...";
        public const string StartingServer = "Iniciando el servidor local...";
        public const string Room = "Sala";
        public const string Team = "Equipo";
        public const string TeamAuto = "Automático";
        public const string Character = "Personaje";
        public const string Loadout = "Tus 10 babosas";
        public const string RandomLoadout = "Aleatorias equilibradas";
        public const string Ready = "¡Listo!";
        public const string NotReady = "Cancelar";
        public const string Leave = "Salir de la sala";
        public const string BackToRoom = "Volver a la sala";
        public const string Resume = "Reanudar";
        public const string LeaveMatch = "Abandonar la partida";
        public const string Victory = "¡VICTORIA!";
        public const string Defeat = "DERROTA";
        public const string Draw = "EMPATE";
        public const string RoundWon = "Ronda ganada";
        public const string RoundLost = "Ronda perdida";
        public const string RoundDraw = "Ronda empatada";
        public const string Round = "Ronda";
        public const string You = "Tú";
        public const string Allies = "Aliados";
        public const string Spectating = "Observando a";
        public const string Eliminated = "Has sido derrotado. Clic para cambiar de observado.";
        public const string Paused = "PAUSA";
        public const string Disconnected = "Conexión perdida";
        public const string BackToMenu = "Volver al menú";
        public const string JavaMissing = "No se encontró el servidor local. Compílalo con: mvn -DskipTests package";
        public const string LockedTemplate = "vuelve en {0}";
        public const string Defeated = "ELIMINADO";
        public const string DefeatedBy = "por";
        public const string Fell = "Caíste";
        public const string SpectatingHint = "Clic: siguiente compañero";
        public const string Scoreboard = "MARCADOR";
        public const string Ready2 = "LISTO";
        public const string Blaster = "BLASTER";
        public const string FightNow = "¡A LUCHAR!";
        public const string GetReady = "PREPÁRATE";

        /// <summary>The short Spanish name of a status effect for its chip.</summary>
        public static string Status(string id)
        {
            switch (id)
            {
                case "burn": return "ARDIENDO";
                case "slow": return "LENTO";
                case "freeze": return "CONGELADO";
                case "stun": return "ATURDIDO";
                case "regen": return "REGENERA";
                case "blind": return "CEGADO";
                case "overshield": return "SOBREESCUDO";
                default: return id == null ? string.Empty : id.ToUpperInvariant();
            }
        }

        /// <summary>The Spanish name of an element.</summary>
        public static string Element(string id)
        {
            switch (id)
            {
                case "NEUTRAL": return "Neutro";
                case "FIRE": return "Fuego";
                case "WATER": return "Agua";
                case "EARTH": return "Tierra";
                case "AIR": return "Aire";
                case "ENERGY": return "Energía";
                case "ICE": return "Hielo";
                case "METAL": return "Metal";
                case "LIGHT": return "Luz";
                default: return id ?? string.Empty;
            }
        }

        /// <summary>The Spanish name of a slug role.</summary>
        public static string Role(string role)
        {
            switch (role)
            {
                case "SUPPORT": return "Apoyo";
                case "CONTROL": return "Control";
                default: return "Daño";
            }
        }
    }
}
