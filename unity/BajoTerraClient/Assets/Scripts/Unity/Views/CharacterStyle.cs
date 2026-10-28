using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>How a character's head is dressed.</summary>
    public enum HeadGear
    {
        None,
        SpikyHair,
        Ponytail,
        Visor,
        Mole
    }

    /// <summary>
    /// The colours and silhouette of one character. Looks come from the character's id (the Shane gang have their
    /// own), and any other id the server may add later gets a stable look of its own from a hash, so new content
    /// never shows up as a plain capsule.
    /// </summary>
    public readonly struct CharacterStyle
    {
        public readonly Color Skin;
        public readonly Color Outfit;
        public readonly Color Trim;
        public readonly Color Pants;
        public readonly Color Hair;
        public readonly HeadGear Gear;

        /// <summary>Multiplier of the torso and arm width (a troll is broad, a molenoid is small).</summary>
        public readonly float Bulk;

        /// <summary>Multiplier of the head size.</summary>
        public readonly float HeadSize;

        public readonly bool ShoulderPlates;

        private CharacterStyle(Color skin, Color outfit, Color trim, Color pants, Color hair, HeadGear gear,
            float bulk, float headSize, bool shoulderPlates)
        {
            Skin = skin;
            Outfit = outfit;
            Trim = trim;
            Pants = pants;
            Hair = hair;
            Gear = gear;
            Bulk = bulk;
            HeadSize = headSize;
            ShoulderPlates = shoulderPlates;
        }

        public static CharacterStyle For(string characterId)
        {
            switch (characterId)
            {
                case "eli":
                    return new CharacterStyle(new Color(0.945f, 0.769f, 0.635f), new Color(0.2f, 0.21f, 0.26f),
                        new Color(1f, 0.48f, 0.11f), new Color(0.42f, 0.47f, 0.56f), new Color(0.11f, 0.12f, 0.2f),
                        HeadGear.SpikyHair, 1f, 1f, false);
                case "kord":
                    return new CharacterStyle(new Color(0.43f, 0.59f, 0.84f), new Color(0.87f, 0.88f, 0.91f),
                        new Color(1f, 0.48f, 0.11f), new Color(0.18f, 0.17f, 0.19f), new Color(0.7f, 0.74f, 0.78f),
                        HeadGear.Visor, 1.3f, 0.95f, true);
                case "trixie":
                    return new CharacterStyle(new Color(0.965f, 0.8f, 0.72f), new Color(0.35f, 0.44f, 0.18f),
                        new Color(1f, 0.48f, 0.11f), new Color(0.63f, 0.65f, 0.69f), new Color(0.66f, 0.2f, 0.1f),
                        HeadGear.Ponytail, 0.9f, 1.05f, false);
                case "pronto":
                    return new CharacterStyle(new Color(0.73f, 0.55f, 0.66f), new Color(0.18f, 0.38f, 0.69f),
                        new Color(0.91f, 0.47f, 0.1f), new Color(0.25f, 0.38f, 0.17f), new Color(0.42f, 0.29f, 0.23f),
                        HeadGear.Mole, 0.95f, 1.15f, false);
                default:
                    return Generated(characterId);
            }
        }

        private static CharacterStyle Generated(string id)
        {
            Color baseColor = Palette.Hashed(id);
            Color.RGBToHSV(baseColor, out float hue, out _, out _);
            Color outfit = Color.HSVToRGB(hue, 0.65f, 0.85f);
            Color trim = Color.HSVToRGB((hue + 0.5f) % 1f, 0.7f, 1f);
            return new CharacterStyle(new Color(0.9f, 0.72f, 0.6f), outfit, trim,
                Color.HSVToRGB(hue, 0.4f, 0.3f), Color.HSVToRGB((hue + 0.1f) % 1f, 0.5f, 0.4f), HeadGear.SpikyHair,
                1f, 1f, false);
        }
    }
}
