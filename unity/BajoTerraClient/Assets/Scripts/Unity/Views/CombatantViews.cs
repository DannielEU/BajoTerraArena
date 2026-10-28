using System.Collections.Generic;
using Bajoterra.Client.Content;
using Bajoterra.Client.Interpolation;
using Bajoterra.Client.Session;
using Bajoterra.Protocol;
using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>The views of every combatant of a match, keyed by combatant id.</summary>
    public sealed class CombatantViews
    {
        private readonly Transform _root;
        private readonly Dictionary<int, CombatantView> _views = new Dictionary<int, CombatantView>();
        private readonly Dictionary<int, ContentLookup> _lookups = new Dictionary<int, ContentLookup>();

        public CombatantViews(Transform parent, MatchSession match)
        {
            _root = new GameObject("Combatants").transform;
            _root.SetParent(parent, false);
            for (int i = 0; i < match.Slots.Count; i++)
            {
                Slot slot = match.Slots[i];
                Bajoterra.Protocol.CharacterInfo character = match.Content.FindCharacter(slot.CharacterId);
                Color team = Palette.Team(slot.Team, match.YourTeam);
                _views[i] = new CombatantView(_root, slot, character, team, i == match.YourId, slot.Team == match.YourTeam,
                    match.Map, match.Content);
                _lookups[i] = new ContentLookup(slot.MaxHp, slot.MaxShield);
            }
        }

        public void Apply(WorldFrame frame, float deltaTime, Camera camera)
        {
            for (int i = 0; i < frame.CombatantCount; i++)
            {
                int id = frame.Combatants[i].Id;
                if (_views.TryGetValue(id, out CombatantView view))
                {
                    view.Apply(frame.Combatants[i], deltaTime, camera, _lookups[id]);
                }
            }
        }

        public bool TryGetCenter(int id, out Vector3 center)
        {
            if (_views.TryGetValue(id, out CombatantView view))
            {
                center = view.Center;
                return true;
            }
            center = Vector3.zero;
            return false;
        }

        public bool TryGetHead(int id, out Vector3 head)
        {
            if (_views.TryGetValue(id, out CombatantView view))
            {
                head = view.Head;
                return true;
            }
            head = Vector3.zero;
            return false;
        }

        public void Destroy()
        {
            foreach (CombatantView view in _views.Values)
            {
                view.Destroy();
            }
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
            }
        }
    }
}
