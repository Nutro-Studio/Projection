using System;
using System.Collections.Generic;
using Core.Combat;
using Core.Map;

namespace Core.Units
{
    /// <summary>
    /// Validates and executes unit movement between adjacent provinces. Entering enemy troops hands the battle to
    /// <see cref="ICombatService"/>; entering an undefended foreign province with military captures it.
    /// </summary>
    public class MovementService
    {
        private readonly GameState _state;
        private readonly ICombatService _combat;

        /// <summary>(province, previous owner, new owner). Hook for VP scoring and the crisis check.</summary>
        public event Action<Province, Faction, Faction> ProvinceCaptured;

        public event Action<CombatResult> CombatResolved;

        public MovementService(GameState state, ICombatService combat)
        {
            _state = state;
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public List<Unit> ReadyUnits(Faction faction, Province province, UnitType type)
        {
            var units = _state.UnitsIn(province, faction, type);
            for (int i = units.Count - 1; i >= 0; i--)
            {
                if (units[i].HasMovedThisTurn) units.RemoveAt(i);
            }

            return units;
        }

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

            var movers = ReadyUnits(faction, from, type);
            movers.RemoveRange(count, movers.Count - count);
            for (int i = 0; i < movers.Count; i++) movers[i].HasMovedThisTurn = true;

            var defender = _state.HostileOccupant(to, faction);
            if (defender != null)
                return Attack(faction, defender, from, to, movers);

            for (int i = 0; i < movers.Count; i++) movers[i].Location = to;
            string message = $"{faction}: moved {count} {type} {from} -> {to}";
            _state.Write(message);

            if (type == UnitType.Military && to.Owner != faction)
                Capture(faction, to);

            return ActionResult.Ok(message);
        }

        private ActionResult Attack(Faction attacker, Faction defender, Province from, Province to, List<Unit> attackers)
        {
            var defenders = _state.UnitsIn(to, defender, UnitType.Military);

            var result = _combat.ResolveBattle(new CombatContext(
                Attackers: ToCombatUnits(attackers, attacker),
                Defenders: ToCombatUnits(defenders, defender),
                DefenderHasFort: to.HasFortification && to.Owner == defender,
                DefenderReceivesFortBonus: defender.Ruleset.ReceivesFortificationBonus));
            CombatResolved?.Invoke(result);

            int attackersBefore = attackers.Count;
            int defendersBefore = defenders.Count;
            RemoveCasualties(attackers, result.SurvivingAttackers);
            RemoveCasualties(defenders, result.SurvivingDefenders);

            _state.Write($"Battle for {to}: {attacker} ({attackersBefore}) vs {defender} ({defendersBefore}). " +
                         $"3d4 {result.AttackerRawSum} vs {result.DefenderRawSum}, " +
                         $"hits {result.AttackerEffectiveHits}/{result.DefenderEffectiveHits}. " +
                         $"Losses {attacker} -{attackersBefore - attackers.Count}, {defender} -{defendersBefore - defenders.Count}");

            // A wiped-out garrison cannot hold the province, even on a casualty tie.
            bool attackerTakesProvince = attackers.Count > 0 && (defenders.Count == 0 || !result.AttackerMustRetreat);
            if (!attackerTakesProvince)
            {
                // Attackers never left: survivors stay in the province they attacked from.
                string held = $"{defender} holds {to}";
                _state.Write(held);
                return ActionResult.Ok(held);
            }

            if (defenders.Count > 0) Retreat(defender, to, defenders);

            for (int i = 0; i < attackers.Count; i++) attackers[i].Location = to;
            if (to.Owner != attacker) Capture(attacker, to);
            return ActionResult.Ok($"{attacker} takes {to}");
        }

        private void Retreat(Faction faction, Province from, List<Unit> units)
        {
            Province refuge = null;
            var neighbors = _state.Map.Neighbors(from);
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (neighbors[i].Owner == faction && _state.HostileOccupant(neighbors[i], faction) == null)
                {
                    refuge = neighbors[i];
                    break;
                }
            }

            if (refuge == null)
            {
                for (int i = 0; i < units.Count; i++) _state.RemoveUnit(units[i]);
                _state.Write($"{faction}: {units.Count} unit(s) had nowhere to retreat and were destroyed");
                return;
            }

            for (int i = 0; i < units.Count; i++) units[i].Location = refuge;
            _state.Write($"{faction}: {units.Count} unit(s) retreated {from} -> {refuge}");
        }

        private void Capture(Faction newOwner, Province province)
        {
            var previous = province.Owner;
            province.Owner = newOwner;

            // Undefended non-combat units (e.g. missionaries) of the old owner are lost with the province.
            var present = _state.UnitsIn(province);
            for (int i = 0; i < present.Count; i++)
            {
                if (present[i].Owner != newOwner && !present[i].IsCombatant) _state.RemoveUnit(present[i]);
            }

            _state.Write($"{newOwner}: captured {province} from {(previous != null ? previous.Name : "neutrals")}");
            ProvinceCaptured?.Invoke(province, previous, newOwner);
        }

        private static List<CombatUnit> ToCombatUnits(List<Unit> units, Faction owner)
        {
            var list = new List<CombatUnit>(units.Count);
            for (int i = 0; i < units.Count; i++)
            {
                list.Add(new CombatUnit(units[i].Id, owner.Archetype, MaxHp: 1, CurrentHp: 1));
            }

            return list;
        }

        /// <summary>Removes from the game every unit whose id is missing from the combat survivors.</summary>
        private void RemoveCasualties(List<Unit> units, IReadOnlyList<CombatUnit> survivors)
        {
            for (int i = units.Count - 1; i >= 0; i--)
            {
                bool survived = false;
                for (int s = 0; s < survivors.Count; s++)
                {
                    if (survivors[s].Id == units[i].Id)
                    {
                        survived = true;
                        break;
                    }
                }

                if (survived) continue;
                _state.RemoveUnit(units[i]);
                units.RemoveAt(i);
            }
        }
    }
}
