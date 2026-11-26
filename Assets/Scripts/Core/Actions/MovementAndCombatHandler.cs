using System;
using System.Collections.Generic;
using Core.Board;
using Core.Combat;
using Core.Crisis;
using Core.Simulation;

namespace Core.Actions
{
    internal sealed class MovementAndCombatHandler
    {
        private readonly GameSessionState _state;
        private readonly ICombatService _combatService;
        private readonly IReadOnlyDictionary<FactionType, IFactionRuleset> _rulesets;

        public MovementAndCombatHandler(
            GameSessionState state,
            ICombatService combatService,
            IReadOnlyDictionary<FactionType, IFactionRuleset> rulesets)
        {
            _state = state;
            _combatService = combatService;
            _rulesets = rulesets;
        }

        public ActionResult ValidateMoveOrigin(int provinceId)
        {
            if (_state.IsGameOver) return ActionResult.GameOver;
            if (_state.CurrentPlayer.ActionPoints <= 0) return ActionResult.NoActionPoints;

            var units = _state.Provinces[provinceId].Units;
            int playerId = _state.CurrentPlayer.PlayerId;
            bool hasOwn = false;
            bool hasMovable = false;

            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].OwnerPlayerId != playerId) continue;
                hasOwn = true;
                if (!units[i].HasActed) hasMovable = true;
            }

            if (!hasOwn) return ActionResult.NoUnits;
            if (!hasMovable) return ActionResult.UnitsAlreadyMoved;
            return ActionResult.Ok;
        }

        public ActionResult MoveArmy(MoveUnitsCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            int fromId = command.OriginProvinceId;
            int toId = command.TargetProvinceId;
            var originCheck = ValidateMoveOrigin(fromId);
            if (originCheck != ActionResult.Ok) return originCheck;
            if (command.SelectedUnitIds == null || command.SelectedUnitIds.Count == 0)
            {
                return ActionResult.NoUnitsSelected;
            }

            var from = _state.Provinces[fromId];
            var to = _state.Provinces[toId];
            if (fromId == toId || !from.IsAdjacentTo(toId)) return ActionResult.NotAdjacent;

            var selection = SplitSelectedUnits(
                from, _state.CurrentPlayer.PlayerId, command.SelectedUnitIds, out var movers, out var stayers);
            if (selection != ActionResult.Ok) return selection;

            _state.SetProvince(from.WithUnits(stayers));
            _state.SetProvince(to.WithUnits(Concat(to.Units, movers)));
            _state.SpendActionPoint();
            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        public ActionResult InitiateBattle(int provinceId, int defenderPlayerId)
        {
            if (_state.IsGameOver) return ActionResult.GameOver;
            if (_state.CurrentPlayer.ActionPoints <= 0) return ActionResult.NoActionPoints;

            var attacker = _state.CurrentPlayer;
            if (attacker.PlayerId == defenderPlayerId) return ActionResult.InvalidBattleTarget;

            var defender = _state.GetPlayer(defenderPlayerId);
            if (defender == null) return ActionResult.InvalidBattleTarget;

            var province = _state.Provinces[provinceId];
            int previousOwnerId = province.OwnerId;

            var attackers = GetUnitsForPlayer(provinceId, attacker.PlayerId, UnitType.RegularMilitary);
            if (attackers.Count == 0) return ActionResult.NoMilitaryUnits;

            var defenders = GetUnitsForPlayer(provinceId, defenderPlayerId, UnitType.RegularMilitary);
            if (defenders.Count == 0) return ActionResult.NoTargetUnits;

            bool defenderHasFort = province.HasFort && province.OwnerId == defenderPlayerId;
            bool receivesFortBonus = _rulesets.TryGetValue(defender.Faction, out var ruleset)
                                     && ruleset.ReceivesFortificationBonus;

            var result = _combatService.ResolveBattle(
                new CombatContext(attackers, defenders, defenderHasFort, receivesFortBonus));

            var bystanders = new List<UnitInstance>(province.Units.Count);
            for (int i = 0; i < province.Units.Count; i++)
            {
                var unit = province.Units[i];
                bool isAttacker = unit.OwnerPlayerId == attacker.PlayerId && unit.Type == UnitType.RegularMilitary;
                bool isDefender = unit.OwnerPlayerId == defenderPlayerId && unit.Type == UnitType.RegularMilitary;
                if (!isAttacker && !isDefender) bystanders.Add(unit);
            }

            bool captured = result.DefenderMustRetreat && result.SurvivingAttackers.Count > 0;
            var updatedUnits = Concat(bystanders, result.SurvivingAttackers);

            if (result.DefenderMustRetreat)
            {
                RetreatUnits(province, defenderPlayerId, result.SurvivingDefenders);
            }
            else
            {
                updatedUnits = Concat(updatedUnits, result.SurvivingDefenders);
            }

            _state.SetProvince(province.WithUnits(updatedUnits));
            _state.SpendActionPoint();

            var postBattleProvince = _state.Provinces[provinceId];
            bool provinceCaptured = captured ||
                                    (previousOwnerId != attacker.PlayerId &&
                                     postBattleProvince.OwnerId == attacker.PlayerId);

            if (provinceCaptured && attacker.Faction == FactionType.Militants)
            {
                _state.AddVictoryPoints(_state.CurrentPlayerIndex, _state.Config.CaptureOrCrushRebellionVp);
            }

            if (!provinceCaptured
                && result.AttackerMustRetreat
                && defender.Faction == FactionType.Guerrillas
                && postBattleProvince.RebellionCell != null
                && postBattleProvince.RebellionCell.OwnerId == defenderPlayerId)
            {
                int defenderIndex = _state.IndexOfPlayer(defenderPlayerId);
                if (defenderIndex >= 0)
                {
                    _state.AddVictoryPoints(defenderIndex, _state.Config.DefendRebellionVp);
                }
            }

            _state.NotifyBattleResolved(new CombatReport(
                province.Name, attacker.PlayerId, defenderPlayerId,
                attackers.Count, defenders.Count, provinceCaptured, result));

            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        public void RetreatUnits(ProvinceState originProvince, int playerId, IReadOnlyList<UnitInstance> survivors)
        {
            if (survivors.Count == 0) return;

            for (int i = 0; i < originProvince.Neighbours.Count; i++)
            {
                var neighbour = _state.Provinces[originProvince.Neighbours[i]];
                if (neighbour.OwnerId == playerId)
                {
                    _state.SetProvince(neighbour.WithUnits(Concat(neighbour.Units, survivors)));
                    return;
                }
            }
        }

        public List<UnitInstance> GetUnitsForPlayer(int provinceId, int playerId, UnitType? typeFilter = null)
        {
            var units = _state.Provinces[provinceId].Units;
            var result = new List<UnitInstance>();
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit.OwnerPlayerId != playerId) continue;
                if (typeFilter.HasValue && unit.Type != typeFilter.Value) continue;
                result.Add(unit);
            }

            return result;
        }

        private static ActionResult SplitSelectedUnits(
            ProvinceState province,
            int movingPlayerId,
            IReadOnlyList<int> selectedUnitIds,
            out List<UnitInstance> movers,
            out List<UnitInstance> stayers)
        {
            movers = new List<UnitInstance>(selectedUnitIds.Count);
            stayers = new List<UnitInstance>(province.Units.Count);

            for (int i = 0; i < province.Units.Count; i++)
            {
                var unit = province.Units[i];
                if (unit.OwnerPlayerId != movingPlayerId || !Contains(selectedUnitIds, unit.Id))
                {
                    stayers.Add(unit);
                    continue;
                }

                if (unit.HasActed) return ActionResult.UnitsAlreadyMoved;
                movers.Add(unit with { HasActed = true });
            }

            return movers.Count == selectedUnitIds.Count ? ActionResult.Ok : ActionResult.InvalidUnitSelection;
        }

        private static bool Contains(IReadOnlyList<int> ids, int id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id) return true;
            }

            return false;
        }

        private static List<UnitInstance> Concat(IReadOnlyList<UnitInstance> first, IReadOnlyList<UnitInstance> second)
        {
            var units = new List<UnitInstance>(first.Count + second.Count);
            for (int i = 0; i < first.Count; i++) units.Add(first[i]);
            for (int i = 0; i < second.Count; i++) units.Add(second[i]);
            return units;
        }
    }
}