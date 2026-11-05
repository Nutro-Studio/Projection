using System;
using System.Collections.Generic;
using Core.Map;
using Core.Units;

namespace Core
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

        public Unit FindUnit(int id)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i].Id == id) return _units[i];
            }

            return null;
        }

        /// <summary>Units in the province; null faction/type means "any".</summary>
        public List<Unit> UnitsIn(Province province, Faction owner = null, UnitType? type = null)
        {
            var result = new List<Unit>(8);
            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit.Location != province) continue;
                if (owner != null && unit.Owner != owner) continue;
                if (type.HasValue && unit.Type != type.Value) continue;
                result.Add(unit);
            }

            return result;
        }

        public int CountUnits(Faction owner, Province province)
        {
            int count = 0;
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i].Owner == owner && _units[i].Location == province) count++;
            }

            return count;
        }

        /// <summary>The faction other than <paramref name="viewer"/> with combat units in the province, if any.</summary>
        public Faction HostileOccupant(Province province, Faction viewer)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit.Location == province && unit.IsCombatant && unit.Owner != viewer) return unit.Owner;
            }

            return null;
        }

        public void Write(string message) => Log?.Invoke(message);
    }
}
