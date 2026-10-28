using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Bajoterra.Client.Content;
using Bajoterra.Protocol;

namespace Bajoterra.Client.Tests
{
    /// <summary>One recorded server tick: the input that was applied and where the combatant ended up.</summary>
    public sealed class RecordedTick
    {
        public int Number;
        public float MoveX;
        public float MoveZ;
        public float Yaw;
        public uint Mask;
        public float X;
        public float Y;
        public float Z;
        public int Posture;
        public int ActiveMove;
        public bool Grounded;
    }

    /// <summary>The authoritative state the server would have sent after <see cref="Number"/> inputs.</summary>
    public sealed class RecordedSnap
    {
        public int Number;
        public int ServerTick;
        public CombatantState State;
        public LocalState Local;
    }

    public sealed class RecordedScenario
    {
        public string Name;
        public string Character;
        public readonly List<RecordedTick> Ticks = new List<RecordedTick>();
        public readonly List<RecordedSnap> Snaps = new List<RecordedSnap>();
    }

    /// <summary>
    /// Movement recorded from the real Java server by <c>MovementParityFixtureTest</c>: the content and arena as the
    /// server sends them, plus scripted scenarios with the position after every tick.
    /// </summary>
    public sealed class ParityFixture
    {
        private static ParityFixture _cached;

        public ContentDetails Details;
        public ContentCatalog Content;
        public ArenaMap Map;
        public readonly List<RecordedScenario> Scenarios = new List<RecordedScenario>();

        public static ParityFixture Load()
        {
            if (_cached != null)
            {
                return _cached;
            }
            var fixture = new ParityFixture();
            RecordedScenario current = null;
            foreach (string line in File.ReadAllLines(FixtureLocator.Find("movement-parity.txt")))
            {
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }
                string[] f = line.Split(' ');
                switch (f[0])
                {
                    case "details":
                        fixture.Details = ContentDetails.Parser.ParseFrom(Convert.FromBase64String(f[1]));
                        fixture.Content = new ContentCatalog(TestContent.ManifestFor(fixture.Details), fixture.Details);
                        break;
                    case "map":
                        fixture.Map = new ArenaMap(MapData.Parser.ParseFrom(Convert.FromBase64String(f[1])));
                        break;
                    case "scenario":
                        current = new RecordedScenario { Name = f[1], Character = f[2] };
                        fixture.Scenarios.Add(current);
                        break;
                    case "snap":
                        current.Snaps.Add(new RecordedSnap
                        {
                            Number = int.Parse(f[1], CultureInfo.InvariantCulture),
                            ServerTick = int.Parse(f[2], CultureInfo.InvariantCulture),
                            State = CombatantState.Parser.ParseFrom(Convert.FromBase64String(f[3])),
                            Local = LocalState.Parser.ParseFrom(Convert.FromBase64String(f[4]))
                        });
                        break;
                    case "tick":
                        // tick n moveX moveZ yaw mask | x y z posture activeMove grounded
                        current.Ticks.Add(new RecordedTick
                        {
                            Number = int.Parse(f[1], CultureInfo.InvariantCulture),
                            MoveX = Float(f[2]),
                            MoveZ = Float(f[3]),
                            Yaw = Float(f[4]),
                            Mask = uint.Parse(f[5], CultureInfo.InvariantCulture),
                            X = Float(f[7]),
                            Y = Float(f[8]),
                            Z = Float(f[9]),
                            Posture = int.Parse(f[10], CultureInfo.InvariantCulture),
                            ActiveMove = int.Parse(f[11], CultureInfo.InvariantCulture),
                            Grounded = f[12] == "1"
                        });
                        break;
                }
            }
            _cached = fixture;
            return fixture;
        }

        private static float Float(string text)
        {
            return float.Parse(text, CultureInfo.InvariantCulture);
        }
    }
}
