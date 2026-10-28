using Bajoterra.Client;
using Bajoterra.Client.Content;
using Bajoterra.Client.Interpolation;
using Bajoterra.Client.Session;
using UnityEngine;
using UnityEngine.UI;

namespace Bajoterra.Unity
{
    /// <summary>
    /// A small top-down map of the arena: walls, cover, platforms and ramps drawn from the same boxes the server
    /// simulates, spawn pads, pickups (dimmed while they wait to respawn), you with an arrow, your allies, and the
    /// enemies you can see. An enemy behind a wall is not shown, so the map is no wall hack. North (+z) is up.
    /// </summary>
    public sealed class HudMinimap
    {
        private const float Width = 270f;
        private const float Height = 190f;
        private const float Inset = 10f;

        private readonly RectTransform _frame;
        private RectTransform _map;
        private Image[] _pickups = new Image[0];
        private Image[] _dots = new Image[0];
        private Image _arrow;
        private float _scale = 1f;
        private float _centerX;
        private float _centerZ;

        public HudMinimap(RectTransform parent)
        {
            Image window = UiKit.Window(parent, "Minimap", new Color(0.03f, 0.045f, 0.08f, 0.82f), 16);
            UiKit.Place(window.rectTransform, new Vector2(0f, 0f), new Vector2(Width, Height), new Vector2(30f, 30f));
            _frame = window.rectTransform;
        }

        /// <summary>Draws the static part of the map for a match (call once per match).</summary>
        public void Build(MatchSession match)
        {
            if (_map != null)
            {
                Object.Destroy(_map.gameObject);
            }
            ArenaMap arena = match.Map;
            float mapWidth = arena.MaxX - arena.MinX;
            float mapDepth = arena.MaxZ - arena.MinZ;
            _scale = Mathf.Min((Width - Inset * 2f) / mapWidth, (Height - Inset * 2f) / mapDepth);
            _centerX = (arena.MinX + arena.MaxX) * 0.5f;
            _centerZ = (arena.MinZ + arena.MaxZ) * 0.5f;

            _map = UiKit.Rect("Map", _frame);
            UiKit.Place(_map, new Vector2(0.5f, 0.5f), new Vector2(mapWidth * _scale, mapDepth * _scale), Vector2.zero);
            Image floor = UiKit.Panel(_map, "Floor", new Color(0.09f, 0.11f, 0.17f, 1f));
            UiKit.Stretch(floor.rectTransform);

            foreach (ObstacleBox box in arena.Obstacles)
            {
                Image rect = UiKit.Panel(_map, "Obstacle " + box.Id, ObstacleColor(box));
                Vector3 center = box.Center.ToUnity();
                Vector3 size = box.Size.ToUnity();
                UiKit.Place(rect.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(size.x * _scale, size.z * _scale),
                    ToMap(center.x, center.z));
                rect.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                if (box.IsRamp)
                {
                    // A lighter stripe on the high end shows which way a ramp climbs.
                    Image tip = UiKit.Panel(rect.transform, "Tip", new Color(1f, 1f, 1f, 0.35f));
                    PlaceRampTip(tip.rectTransform, box.Rise);
                }
            }
            foreach (SpawnPoint spawn in arena.Spawns)
            {
                Image pad = UiKit.Panel(_map, "Spawn", Palette.WithAlpha(spawn.Team == match.YourTeam ? Palette.TeamOwn : Palette.TeamOther, 0.55f));
                pad.sprite = UiSprites.Circle();
                Vector3 at = spawn.Position.ToUnity();
                UiKit.Place(pad.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(11f, 11f), ToMap(at.x, at.z));
                pad.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }

            _pickups = new Image[arena.Pickups.Length];
            for (int i = 0; i < _pickups.Length; i++)
            {
                PickupSlot slot = arena.Pickups[i];
                Image gem = UiKit.Panel(_map, "Pickup " + i, Palette.Pickup(match.Content.Pickup(slot.PickupIndex).Id));
                gem.sprite = UiSprites.Diamond();
                Vector3 at = slot.Position.ToUnity();
                UiKit.Place(gem.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(11f, 11f), ToMap(at.x, at.z));
                gem.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _pickups[i] = gem;
            }

            _dots = new Image[match.Slots.Count];
            for (int i = 0; i < _dots.Length; i++)
            {
                Image dot = UiKit.Panel(_map, "Combatant " + i, Color.white);
                dot.sprite = UiSprites.Circle();
                UiKit.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(12f, 12f), Vector2.zero);
                dot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                UiKit.AddOutlineTo(dot, 1f);
                _dots[i] = dot;
            }
            _arrow = UiKit.Panel(_map, "Arrow", Color.white);
            UiKit.Place(_arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(16f, 5f), Vector2.zero);
            _arrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        public void Refresh(MatchSession match)
        {
            WorldFrame frame = match.Frame;
            if (_map == null || !frame.Valid)
            {
                return;
            }
            for (int i = 0; i < _pickups.Length; i++)
            {
                bool available = true;
                for (int k = 0; k < frame.PickupCount; k++)
                {
                    if (frame.Pickups[k].Index == i)
                    {
                        available = frame.Pickups[k].Available;
                        break;
                    }
                }
                Color color = _pickups[i].color;
                color.a = available ? 1f : 0.25f;
                _pickups[i].color = color;
            }

            int ownIndex = frame.IndexOf(match.YourId);
            Vec3 eye = ownIndex >= 0 ? frame.Combatants[ownIndex].Position + new Vec3(0f, 1.5f, 0f) : default;
            for (int i = 0; i < _dots.Length; i++)
            {
                int index = frame.IndexOf(i);
                if (index < 0)
                {
                    _dots[i].gameObject.SetActive(false);
                    continue;
                }
                CombatantFrame c = frame.Combatants[index];
                bool you = i == match.YourId;
                bool ally = match.TeamOf(i) == match.YourTeam;
                bool visible = you || ally;
                if (!you && !ally && c.Alive && ownIndex >= 0)
                {
                    visible = InSight(match.Map, eye, c.Position + new Vec3(0f, 1.2f, 0f));
                }
                if (!c.Alive && !ally)
                {
                    visible = false;
                }
                _dots[i].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }
                Vector3 at = c.Position.ToUnity();
                _dots[i].rectTransform.anchoredPosition = ToMap(at.x, at.z);
                _dots[i].color = !c.Alive ? new Color(0.45f, 0.45f, 0.5f, 0.8f)
                    : you ? Color.white : ally ? Palette.TeamOwn : Palette.TeamOther;
                float size = you ? 14f : 11f;
                _dots[i].rectTransform.sizeDelta = new Vector2(size, size);
                if (you)
                {
                    _arrow.gameObject.SetActive(c.Alive);
                    Vector2 dir = new Vector2(Mathf.Sin(c.Yaw), Mathf.Cos(c.Yaw));
                    _arrow.rectTransform.anchoredPosition = ToMap(at.x, at.z) + dir * 11f;
                    _arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                }
            }
        }

        private static bool InSight(ArenaMap map, Vec3 from, Vec3 to)
        {
            Vec3 delta = to - from;
            float distance = delta.Length;
            if (distance < 0.5f)
            {
                return true;
            }
            return map.SightDistance(from, delta * (1f / distance), distance, 0f) >= distance - 0.01f;
        }

        private Vector2 ToMap(float x, float z)
        {
            return new Vector2((x - _centerX) * _scale, (z - _centerZ) * _scale);
        }

        private static Color ObstacleColor(ObstacleBox box)
        {
            if (!box.BlocksMovement || !box.BlocksProjectiles)
            {
                return new Color(0.4f, 0.75f, 0.9f, 0.35f);
            }
            if (box.IsRamp)
            {
                return new Color(0.2f, 0.62f, 0.72f, 1f);
            }
            float height = box.MaxY - box.MinY;
            if (height <= 1.35f)
            {
                return new Color(0.52f, 0.38f, 0.22f, 1f);
            }
            if (height < 2.2f)
            {
                return new Color(0.5f, 0.45f, 0.4f, 1f);
            }
            return new Color(0.3f, 0.3f, 0.42f, 1f);
        }

        private static void PlaceRampTip(RectTransform tip, RampDirection rise)
        {
            tip.anchorMin = Vector2.zero;
            tip.anchorMax = Vector2.one;
            const float fraction = 0.28f;
            switch (rise)
            {
                case RampDirection.PosX:
                    tip.anchorMin = new Vector2(1f - fraction, 0f);
                    break;
                case RampDirection.NegX:
                    tip.anchorMax = new Vector2(fraction, 1f);
                    break;
                case RampDirection.PosZ:
                    tip.anchorMin = new Vector2(0f, 1f - fraction);
                    break;
                default:
                    tip.anchorMax = new Vector2(1f, fraction);
                    break;
            }
            tip.offsetMin = Vector2.zero;
            tip.offsetMax = Vector2.zero;
        }
    }
}
