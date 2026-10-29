namespace Bajoterra.Unity
{
    /// <summary>
    /// Every string shown to the player, in one place (Spanish). They talk about the game, never about how it runs:
    /// what went wrong behind the scenes goes to the log, and the player reads what to do next.
    /// </summary>
    public static class UiText
    {
        public const string Title = "BAJOTERRA";
        public const string TitleTab = "ARENA";
        public const string Tagline = "Elige tu munición... ¡y a la batalla!";
        public const string PlayerName = "Tu nombre de lanzador";
        public const string QuickMatch = "¡A LA ARENA!";
        public const string QuickMatchHint = "3 contra 3: los huecos los ocupan lanzadores de la caverna.";
        public const string JoinFriendDivider = "o únete a la partida de un amigo";
        public const string FriendCode = "Código de tu amigo";
        public const string JoinFriend = "Unirse";
        public const string OpeningCave = "Abriendo la caverna";
        public const string EnteringCave = "Entrando en la caverna";
        public const string CaveUnavailable = "No se pudo abrir la arena en este equipo. Inténtalo de nuevo.";
        public const string CouldNotEnter = "No se pudo entrar en la partida. Revisa el código e inténtalo otra vez.";
        public const string InviteCode = "Código para invitar a tus amigos";
        public const string Room = "Nombre de la sala";
        public const string Team = "Tu equipo";
        public const string TeamAuto = "Al azar";
        public const string Character = "Personaje";
        public const string Loadout = "Tus 10 babosas";
        public const string RandomLoadout = "Elegir al azar";
        public const string Ready = "¡LISTO!";
        public const string NotReady = "Cancelar";
        public const string Leave = "Salir de la sala";
        public const string BackToRoom = "Volver a la sala";
        public const string Resume = "Reanudar";
        public const string LeaveMatch = "Abandonar la partida";
        public const string Victory = "¡VICTORIA!";
        public const string Defeat = "DERROTA";
        public const string Draw = "EMPATE";
        public const string RoundWon = "¡RONDA GANADA!";
        public const string RoundLost = "RONDA PERDIDA";
        public const string RoundDraw = "RONDA EMPATADA";
        public const string Round = "Ronda";
        public const string You = "Tú";
        public const string Allies = "Aliados";
        public const string Spectating = "Observando a";
        public const string Eliminated = "Has sido derrotado. Clic para cambiar de observado.";
        public const string Paused = "PAUSA";
        public const string Disconnected = "¡SE CORTÓ LA PARTIDA!";
        public const string DisconnectedDetail = "Se perdió el contacto con la caverna. Vuelve al menú y entra de nuevo.";
        public const string BackToMenu = "Volver al menú";
        public const string LockedTemplate = "vuelve en {0}";
        public const string Defeated = "¡DERROTADO!";
        public const string DefeatedBy = "por";
        public const string Fell = "Caíste";
        public const string SpectatingHint = "Clic: siguiente compañero";
        public const string Scoreboard = "MARCADOR";
        public const string Ready2 = "LISTO";
        public const string Blaster = "BLASTER";
        public const string FightNow = "¡A LUCHAR!";
        public const string GetReady = "PREPÁRATE";
        public const string CaveFighter = "lanzador de la caverna";
        public const string Critical = "¡CRÍTICO!";
        public const string Grenade = "Granada";
        public const string CourageBanner = "¡CORAJE!";

        /// <summary>The badge over your bars while your team is outnumbered.</summary>
        public static string CourageBadge(int level)
        {
            return level <= 0 ? string.Empty : level == 1 ? "CORAJE" : "CORAJE x2";
        }

        /// <summary>What courage gives, under the banner when it starts.</summary>
        public static string CourageDetail(int level)
        {
            return level >= 2
                ? "¡Último en pie! Mucho más daño y disparo, y aguantas más"
                : "Tu equipo va en desventaja: más daño, disparo más rápido y escudo extra";
        }

        /// <summary>The warning while an enemy grenade is stuck to you.</summary>
        public static string GrenadeOnYou(float seconds)
        {
            return "¡GRANADA PEGADA!  " + seconds.ToString("0.0") + " s";
        }

        /// <summary>
        /// What the player reads when the game refused something, by the code the game sent. The game's own message
        /// is for the log only.
        /// </summary>
        public static string Problem(uint code)
        {
            switch (code)
            {
                case 1: return "Tu versión del juego no es la misma que la de esta partida. Actualízala para jugar.";
                case 3: return "Esa sala no está disponible ahora mismo. Prueba con otro nombre.";
                case 4: return "Esa combinación de babosas no vale: revisa tu cinturón.";
                case 5: return "Ese héroe no está disponible. Elige otro.";
                case 6: return "Primero entra en una sala.";
                default: return "Algo salió mal. Inténtalo otra vez.";
            }
        }

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
