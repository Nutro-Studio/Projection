using System;
using System.Collections.Generic;
using System.Linq;
using Projection.Core.Map;
using Projection.Core.Units;

namespace Projection.Core
{
    /// <summary>Everything that describes a running match. Services mutate it; UI only reads it.</summary>
    public class GameState
    {
        private readonly List<Unit> _units = new List<Unit>();
        private int _nextUnitId = 1;

        public ProvinceGraph Map { get; }
        public IReadOnlyList<Faction> Factions { get; }
        public IReadOnlyList<Unit> Units => _units;

        public event Action<string> Log;

        public GameState(ProvinceGraph map, IReadOnlyList<Faction> factions)
        {
            Map = map;
            Factions = factions;
        }

        public Unit SpawnUnit(UnitType type, Faction owner, Province location)
        {
            var unit = new Unit(_nextUnitId++, type, owner, location);
            _units.Add(unit);
            return unit;
        }

        public void RemoveUnit(Unit unit) => _units.Remove(unit);

        public IEnumerable<Unit> UnitsIn(Province province) => _units.Where(u => u.Location == province);

        public IEnumerable<Unit> UnitsOf(Faction faction) => _units.Where(u => u.Owner == faction);

        public IEnumerable<Unit> UnitsOf(Faction faction, Province province) =>
            _units.Where(u => u.Owner == faction && u.Location == province);

        public IEnumerable<Unit> CombatantsIn(Province province) => UnitsIn(province).Where(u => u.IsCombatant);

        /// <summary>The non-owner faction with combat units in the province, if any.</summary>
        public Faction HostileOccupant(Province province, Faction viewer) =>
            CombatantsIn(province).Select(u => u.Owner).FirstOrDefault(f => f != viewer);

        public void Write(string message) => Log?.Invoke(message);
    }
}
