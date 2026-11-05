using System;
using System.Collections.Generic;
using System.Linq;
using Projection.Core.Combat;
using Projection.Core.Map;

namespace Projection.Core.Units
{
    /// <summary>
    /// Validates and executes unit movement between adjacent provinces. Entering enemy troops triggers combat
    /// through <see cref="ICombatResolver"/>; entering an undefended foreign province with military captures it.
    /// </summary>
    public class MovementService
    {
        private readonly GameState _state;
        private readonly ICombatResolver _combat;

        /// <summary>(province, previous owner, new owner). Hook for VP scoring and the crisis check.</summary>
        public event Action<Province, Faction, Faction> ProvinceCaptured;

        public MovementService(GameState state, ICombatResolver combat)
        {
            _state = state;
            _combat = combat;
        }

        public List<Unit> ReadyUnits(Faction faction, Province province, UnitType type) =>
            _state.UnitsOf(faction, province).Where(u => u.Type == type && !u.HasMovedThisTurn).ToList();

        public ActionResult CanMove(Faction faction, Province from, Province to, int count, UnitType type = UnitType.Military)
        {
            if (from == null || to == null) return ActionResult.Fail("Select both provinces.");
            if (count <= 0) return ActionResult.Fail("Choose at least one unit.");
            if (from == to) return ActionResult.Fail("Units are already there.");
            if (!_state.Map.AreAdjacent(from, to))
                return ActionResult.Fail($"{to} does not border {from}. Units move one province per turn.");

            int ready = ReadyUnits(faction, from, type).Count;
            if (ready < count)
                return ActionResult.Fail($"{from} has only {ready} {type} unit(s) of {faction} that can still move.");

            if (type != UnitType.Military && _state.HostileOccupant(to, faction) != null)
                return ActionResult.Fail($"{type} cannot enter {to}: enemy troops are there.");

            return ActionResult.Ok();
        }

        public ActionResult Move(Faction faction, Province from, Province to, int count, UnitType type = UnitType.Military)
        {
            var check = CanMove(faction, from, to, count, type);
            if (!check.Success) return check;

            var movers = ReadyUnits(faction, from, type).Take(count).ToList();
            foreach (var unit in movers) unit.HasMovedThisTurn = true;

            var defender = _state.HostileOccupant(to, faction);
            if (defender != null)
                return Attack(faction, defender, from, to, movers);

            foreach (var unit in movers) unit.Location = to;
            string message = $"{faction}: moved {count} {type} {from} -> {to}";
            _state.Write(message);

            if (type == UnitType.Military && to.Owner != faction)
                Capture(faction, to);

            return ActionResult.Ok(message);
        }

        private ActionResult Attack(Faction attacker, Faction defender, Province from, Province to, List<Unit> attackers)
        {
            var defenders = _state.UnitsOf(defender, to).Where(u => u.IsCombatant).ToList();

            var outcome = _combat.Resolve(new CombatContext
            {
                Province = to,
                Attacker = attacker,
                Defender = defender,
                AttackingUnits = attackers,
                DefendingUnits = defenders,
                DefenderFortified = to.HasFortification && to.Owner == defender &&
                                    defender.Archetype == FactionArchetype.Militants
            });

            int attackerLosses = Math.Min(outcome.AttackerLosses, attackers.Count);
            int defenderLosses = Math.Min(outcome.DefenderLosses, defenders.Count);
            RemoveCasualties(attackers, attackerLosses);
            RemoveCasualties(defenders, defenderLosses);

            _state.Write($"Battle for {to}: {attacker} ({attackers.Count + attackerLosses}) vs {defender} " +
                         $"({defenders.Count + defenderLosses}). {outcome.Description}. " +
                         $"Losses {attacker} -{attackerLosses}, {defender} -{defenderLosses}");

            // The side with higher losses retreats; ties go to the defender.
            bool attackerWins = attackers.Count > 0 && (defenders.Count == 0 || defenderLosses > attackerLosses);
            if (!attackerWins)
            {
                // Attackers never left: survivors stay in the province they attacked from.
                string held = $"{defender} holds {to}";
                _state.Write(held);
                return ActionResult.Ok(held);
            }

            if (defenders.Count > 0) Retreat(defender, to, defenders);

            foreach (var unit in attackers) unit.Location = to;
            if (to.Owner != attacker) Capture(attacker, to);
            return ActionResult.Ok($"{attacker} takes {to}");
        }

        private void Retreat(Faction faction, Province from, List<Unit> units)
        {
            var refuge = _state.Map.Neighbors(from)
                .FirstOrDefault(p => p.Owner == faction && _state.HostileOccupant(p, faction) == null);

            if (refuge == null)
            {
                foreach (var unit in units) _state.RemoveUnit(unit);
                _state.Write($"{faction}: {units.Count} unit(s) had nowhere to retreat and were destroyed");
                return;
            }

            foreach (var unit in units) unit.Location = refuge;
            _state.Write($"{faction}: {units.Count} unit(s) retreated {from} -> {refuge}");
        }

        private void Capture(Faction newOwner, Province province)
        {
            var previous = province.Owner;
            province.Owner = newOwner;

            // Undefended non-combat units (e.g. missionaries) of the old owner are lost with the province.
            foreach (var unit in _state.UnitsIn(province).Where(u => u.Owner != newOwner && !u.IsCombatant).ToList())
                _state.RemoveUnit(unit);

            _state.Write($"{newOwner}: captured {province} from {(previous != null ? previous.Name : "neutrals")}");
            ProvinceCaptured?.Invoke(province, previous, newOwner);
        }

        private void RemoveCasualties(List<Unit> units, int losses)
        {
            for (int i = 0; i < losses; i++)
            {
                _state.RemoveUnit(units[units.Count - 1]);
                units.RemoveAt(units.Count - 1);
            }
        }
    }
}
