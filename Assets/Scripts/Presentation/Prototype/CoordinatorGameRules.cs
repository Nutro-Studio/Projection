using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;

namespace Presentation.Prototype
{
    public sealed class CoordinatorGameRules : IGameRules
    {
        private readonly CombatCoordinator _coordinator;
        private readonly IDiceRoller _contestDice = new SystemRandomDiceRoller();

        public CoordinatorGameRules(CombatCoordinator coordinator) => _coordinator = coordinator;

        public CombatResult ResolveSkirmish(
            IReadOnlyList<UnitInstance> attackers,
            IReadOnlyList<UnitInstance> defenders,
            FactionType defenderFaction,
            bool hasFortification) =>
            _coordinator.ResolveSkirmish(attackers, defenders, defenderFaction, hasFortification);

        public PlayerProfile EvaluatePlayerTurnState(PlayerProfile player, int controlledProvinces) =>
            _coordinator.EvaluatePlayerTurnState(player, controlledProvinces);

        public int CalculateRevenue(FactionType faction, int controlledProvinces) =>
            _coordinator.GetRuleset(faction).CalculateUpkeepRevenue(controlledProvinces);

        public bool CanTriggerVoluntaryShift(PlayerProfile player, FactionType targetFaction, int controlledProvinces) =>
            _coordinator.CanTriggerVoluntaryShift(player, targetFaction, controlledProvinces);

        public CrisisShiftResult ExecuteVoluntaryShift(PlayerProfile player, FactionType targetFaction) =>
            _coordinator.ExecuteVoluntaryShift(player, targetFaction);

        public bool CanTriggerAscension(PlayerProfile player, int controlledProvinces) =>
            _coordinator.CanTriggerAscension(player, controlledProvinces);

        public CrisisShiftResult ExecuteAscension(PlayerProfile player, FactionType targetLandedFaction) =>
            _coordinator.ExecuteAscension(player, targetLandedFaction);

        public int RollContest()
        {
            var (roll1, roll2, roll3) = _contestDice.Roll3D4();
            return roll1 + roll2 + roll3;
        }
    }
}