using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;
using Presentation.Prototype;

namespace Tests.Editor
{
    /// <summary>
    /// Plays back fixed attacker / defender rolls, alternating between the two.
    /// </summary>
    internal sealed class ScriptedDiceRoller : IDiceRoller
    {
        public (int, int, int) AttackerRoll = (2, 2, 2);
        public (int, int, int) DefenderRoll = (1, 1, 1);
        private bool _defenderNext;

        public int RollD4() => 1;

        public (int roll1, int roll2, int roll3) Roll3D4()
        {
            var roll = _defenderNext ? DefenderRoll : AttackerRoll;
            _defenderNext = !_defenderNext;
            return roll;
        }
    }

    /// <summary>
    /// IGameRules backed by the real Core combat service, rulesets and CrisisDirector.
    /// </summary>
    internal sealed class CoreRules : IGameRules
    {
        private readonly ICombatService _combat;
        private readonly IDiceRoller _dice;
        private readonly CrisisDirector _director;
        private readonly Dictionary<FactionType, IFactionRuleset> _rulesets;

        public CoreRules(IDiceRoller dice)
        {
            _rulesets = new Dictionary<FactionType, IFactionRuleset>
            {
                { FactionType.Militants, new MilitantRuleset() },
                { FactionType.Zealots, new ZealotRuleset() },
                { FactionType.Guerrillas, new GuerrillaRuleset() },
                { FactionType.Guilds, new GuildRuleset() }
            };
            _dice = dice;
            _combat = new RootCombatService(dice);
            _director = new CrisisDirector(_rulesets);
        }

        public CombatResult ResolveSkirmish(
            IReadOnlyList<UnitInstance> attackers,
            IReadOnlyList<UnitInstance> defenders,
            FactionType defenderFaction,
            bool hasFortification) =>
            _combat.ResolveBattle(new CombatContext(attackers, defenders, hasFortification,
                _rulesets[defenderFaction].ReceivesFortificationBonus));

        public PlayerProfile EvaluatePlayerTurnState(PlayerProfile player, int controlledProvinces)
        {
            if (_director.CheckVictory(player)) return player;

            return _director.CanTriggerCrisis(player, controlledProvinces)
                ? _director.ExecuteForcedCrisis(player).UpdatedProfile
                : player;
        }

        public int CalculateRevenue(FactionType faction, int controlledProvinces) =>
            _rulesets[faction].CalculateUpkeepRevenue(controlledProvinces);

        public int RollContest()
        {
            var (roll1, roll2, roll3) = _dice.Roll3D4();
            return roll1 + roll2 + roll3;
        }

        public bool CanTriggerVoluntaryShift(PlayerProfile player, FactionType targetFaction,
            int controlledProvinces) =>
            _director.CanTriggerVoluntaryShift(player, targetFaction, controlledProvinces);

        public CrisisShiftResult ExecuteVoluntaryShift(PlayerProfile player, FactionType targetFaction) =>
            _director.ExecuteVoluntaryShift(player, targetFaction);

        public bool CanTriggerAscension(PlayerProfile player, int controlledProvinces) =>
            _director.CanTriggerAscension(player, controlledProvinces);

        public CrisisShiftResult ExecuteAscension(PlayerProfile player, FactionType targetLandedFaction) =>
            _director.ExecuteAscension(player, targetLandedFaction);
    }
}
