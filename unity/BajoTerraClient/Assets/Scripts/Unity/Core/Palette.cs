using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// All colours of the game in one place: elements, statuses, teams and pickups. Content the server adds later
    /// (a new element, a new status) still gets a colour through the hash fallback, so nothing breaks before an artist
    /// chooses one.
    /// </summary>
    public static class Palette
    {
        public static readonly Color TeamOwn = new Color(0.22f, 0.62f, 1f);
        public static readonly Color TeamOther = new Color(1f, 0.36f, 0.3f);
        public static readonly Color Health = new Color(0.35f, 0.9f, 0.4f);
        public static readonly Color Shield = new Color(0.35f, 0.75f, 1f);
        public static readonly Color Overshield = new Color(0.78f, 0.45f, 1f);
        public static readonly Color Damage = new Color(1f, 0.55f, 0.2f);
        /// <summary>Headshots: their damage numbers, the crosshair marker and the "¡CRÍTICO!" pop.</summary>
        public static readonly Color Critical = new Color(1f, 0.86f, 0.18f);
        /// <summary>Courage (your team is outnumbered): the aura, the HUD badge and the banner.</summary>
        public static readonly Color Courage = new Color(1f, 0.66f, 0.12f);
        /// <summary>A lit grenade: its blinking light and the blast circle on the floor.</summary>
        public static readonly Color Danger = new Color(1f, 0.16f, 0.1f);

        public static Color Team(int team, int ownTeam)
        {
            return team == ownTeam ? TeamOwn : TeamOther;
        }

        public static Color Element(string element)
        {
            switch (element)
            {
                case "NEUTRAL": return new Color(0.78f, 0.78f, 0.8f);
                case "FIRE": return new Color(1f, 0.42f, 0.12f);
                case "WATER": return new Color(0.2f, 0.5f, 1f);
                case "EARTH": return new Color(0.6f, 0.44f, 0.22f);
                case "AIR": return new Color(0.75f, 0.95f, 1f);
                case "ENERGY": return new Color(1f, 0.92f, 0.2f);
                case "ICE": return new Color(0.55f, 0.9f, 1f);
                case "METAL": return new Color(0.7f, 0.74f, 0.8f);
                case "LIGHT": return new Color(1f, 0.97f, 0.7f);
                default: return Hashed(element);
            }
        }

        public static Color Status(string statusId)
        {
            switch (statusId)
            {
                case "burn": return new Color(1f, 0.45f, 0.1f);
                case "slow": return new Color(0.4f, 0.55f, 1f);
                case "freeze": return new Color(0.6f, 0.95f, 1f);
                case "stun": return new Color(1f, 0.9f, 0.2f);
                case "regen": return new Color(0.4f, 1f, 0.5f);
                case "blind": return new Color(0.15f, 0.15f, 0.2f);
                case "overshield": return Overshield;
                default: return Hashed(statusId);
            }
        }

        public static Color Pickup(string pickupId)
        {
            switch (pickupId)
            {
                case "health_pack": return Health;
                case "shield_cell": return Shield;
                case "overshield_orb": return Overshield;
                default: return Hashed(pickupId);
            }
        }

        /// <summary>A stable, readable colour for any id.</summary>
        public static Color Hashed(string id)
        {
            int hash = 17;
            if (id != null)
            {
                for (int i = 0; i < id.Length; i++)
                {
                    hash = hash * 31 + id[i];
                }
            }
            float hue = Mathf.Abs(hash % 360) / 360f;
            return Color.HSVToRGB(hue, 0.6f, 1f);
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
