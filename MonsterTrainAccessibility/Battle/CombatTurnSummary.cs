using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Accumulates what happened between two player turns (automated combat,
    /// the enemies' move, the next wave arriving) for CombatSummaryMode, so it
    /// can be spoken as one summary right before "Your turn":
    ///
    ///   per floor: damage your units dealt and took, kills, losses, and the HP
    ///   left on every unit that was hit and survived; then enemy movement, new
    ///   arrivals, and pyre damage.
    ///
    /// Owned by <see cref="BattleAccessibility"/>; reset after each summary and
    /// whenever a battle starts or ends.
    /// </summary>
    internal class CombatTurnSummary
    {
        private class FloorStats
        {
            public int Dealt;
            public int Taken;
            public readonly List<string> Killed = new List<string>();
            public readonly List<string> Lost = new List<string>();
        }

        private class HitUnit
        {
            public CharacterState Unit;
            public string Name;
            public bool IsEnemy;
        }

        private readonly Dictionary<int, FloorStats> _floors = new Dictionary<int, FloorStats>();
        private readonly Dictionary<int, HitUnit> _hitUnits = new Dictionary<int, HitUnit>();
        private readonly List<string> _moves = new List<string>();
        private readonly Dictionary<int, List<string>> _arrivals = new Dictionary<int, List<string>>();
        private readonly List<string> _yourArrivals = new List<string>();

        private int _pyreDamageTotal;
        private int _pyreRemaining = -1;

        /// <summary>Only new arrivals - nothing fought, moved or hit the pyre.</summary>
        public bool OnlyArrivals =>
            _floors.Count == 0 && _hitUnits.Count == 0 && _moves.Count == 0 && _pyreDamageTotal == 0;

        public bool IsEmpty =>
            _floors.Count == 0 && _hitUnits.Count == 0 && _moves.Count == 0 &&
            _arrivals.Count == 0 && _yourArrivals.Count == 0 && _pyreDamageTotal == 0;

        public void Reset()
        {
            _floors.Clear();
            _hitUnits.Clear();
            _moves.Clear();
            _arrivals.Clear();
            _yourArrivals.Clear();
            _pyreDamageTotal = 0;
            _pyreRemaining = -1;
        }

        private FloorStats Floor(int roomIndex)
        {
            if (!_floors.TryGetValue(roomIndex, out var stats))
                _floors[roomIndex] = stats = new FloorStats();
            return stats;
        }

        /// <summary>A hit during automated combat. <paramref name="targetIsEnemy"/>: an enemy was hit (you dealt it).</summary>
        public void AddDamage(object target, string targetName, bool targetIsEnemy, int roomIndex, int amount)
        {
            if (amount <= 0) return;
            var floor = Floor(roomIndex);
            if (targetIsEnemy) floor.Dealt += amount;
            else floor.Taken += amount;

            if (target is CharacterState unit)
            {
                int id = RuntimeHelpers.GetHashCode(unit);
                if (!_hitUnits.ContainsKey(id))
                    _hitUnits[id] = new HitUnit { Unit = unit, Name = targetName, IsEnemy = targetIsEnemy };
            }
        }

        public void AddDeath(string unitName, bool isEnemy, int roomIndex)
        {
            if (string.IsNullOrEmpty(unitName)) return;
            var floor = Floor(roomIndex);
            (isEnemy ? floor.Killed : floor.Lost).Add(unitName);
        }

        public void AddMove(string unitName, int toRoomIndex, bool up)
        {
            if (string.IsNullOrEmpty(unitName)) return;
            string where = toRoomIndex == 3 ? "the pyre room" : BattleAccessibility.RoomIndexToFloorName(toRoomIndex).ToLower();
            _moves.Add($"{unitName} {(up ? "up" : "down")} to {where}");
        }

        public void AddArrival(string unitName, bool isEnemy, int roomIndex)
        {
            if (string.IsNullOrEmpty(unitName)) return;
            if (!isEnemy)
            {
                _yourArrivals.Add(unitName);
                return;
            }
            if (!_arrivals.TryGetValue(roomIndex, out var list))
                _arrivals[roomIndex] = list = new List<string>();
            list.Add(unitName);
        }

        public void AddPyre(int damage, int remaining)
        {
            if (damage <= 0) return;
            _pyreDamageTotal += damage;
            _pyreRemaining = remaining;
        }

        /// <summary>
        /// Build the summary sentences. <paramref name="includeDamage"/> follows the
        /// Damage announcement setting.
        /// </summary>
        public List<string> Build(bool includeDamage)
        {
            var parts = new List<string>();

            // Survivors that were hit, grouped by the floor they are on now
            var survivorsByFloor = new Dictionary<int, List<string>>();
            foreach (var hit in _hitUnits.Values)
            {
                var unit = hit.Unit;
                if (unit == null || !unit.IsAlive || unit.GetHP() <= 0) continue;
                int room = Patches.CharacterStateHelper.GetRoomIndex(unit);
                if (!survivorsByFloor.TryGetValue(room, out var list))
                    survivorsByFloor[room] = list = new List<string>();
                string who = hit.IsEnemy ? hit.Name : $"your {hit.Name}";
                list.Add($"{who} at {unit.GetHP()} of {unit.GetMaxHP()}");
            }

            var rooms = new SortedSet<int>(_floors.Keys);
            rooms.UnionWith(survivorsByFloor.Keys);
            foreach (int room in rooms)
            {
                if (room < 0 || room > 3) continue;
                var floorParts = new List<string>();
                if (_floors.TryGetValue(room, out var stats))
                {
                    if (includeDamage && (stats.Dealt > 0 || stats.Taken > 0))
                    {
                        if (stats.Dealt > 0 && stats.Taken > 0)
                            floorParts.Add($"you dealt {stats.Dealt}, took {stats.Taken}");
                        else if (stats.Dealt > 0)
                            floorParts.Add($"you dealt {stats.Dealt}");
                        else
                            floorParts.Add($"you took {stats.Taken}");
                    }
                    if (stats.Killed.Count > 0)
                        floorParts.Add("killed " + JoinNames(stats.Killed));
                    if (stats.Lost.Count > 0)
                        floorParts.Add("lost " + JoinNames(stats.Lost));
                }
                if (survivorsByFloor.TryGetValue(room, out var survivors))
                    floorParts.AddRange(survivors);
                if (floorParts.Count == 0) continue;

                string floorName = room == 3 ? "Pyre room" : BattleAccessibility.RoomIndexToFloorName(room);
                parts.Add($"{floorName}: {string.Join(", ", floorParts)}");
            }

            // Deaths whose floor couldn't be pinned down
            if (_floors.TryGetValue(-1, out var unknown))
            {
                if (unknown.Killed.Count > 0) parts.Add("Killed " + JoinNames(unknown.Killed));
                if (unknown.Lost.Count > 0) parts.Add("Lost " + JoinNames(unknown.Lost));
            }

            if (_moves.Count > 0)
                parts.Add("Moved: " + string.Join(", ", _moves));

            if (includeDamage && _pyreDamageTotal > 0)
                parts.Add(_pyreRemaining >= 0
                    ? $"Pyre took {_pyreDamageTotal}, {_pyreRemaining} left"
                    : $"Pyre took {_pyreDamageTotal}");

            foreach (var kv in _arrivals.OrderBy(k => k.Key))
            {
                string where = kv.Key >= 0 && kv.Key <= 2
                    ? $" on {BattleAccessibility.RoomIndexToFloorName(kv.Key).ToLower()}"
                    : "";
                parts.Add($"Arrived{where}: {JoinNames(kv.Value)}");
            }
            if (_yourArrivals.Count > 0)
                parts.Add("Joined you: " + JoinNames(_yourArrivals));

            return parts;
        }

        /// <summary>"Imp, Imp, Guard" becomes "2 Imp, Guard".</summary>
        private static string JoinNames(List<string> names)
        {
            return string.Join(", ", names
                .GroupBy(n => n)
                .Select(g => g.Count() > 1 ? $"{g.Count()} {g.Key}" : g.Key));
        }
    }
}
