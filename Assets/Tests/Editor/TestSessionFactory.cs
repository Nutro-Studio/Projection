using System.Collections.Generic;
using Core.Board;
using Core.Combat;
using Core.Crisis;
using Core.Simulation;

namespace Tests.Editor
{
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

    internal static class TestSessionFactory
    {
        public static Dictionary<FactionType, IFactionRuleset> CreateRulesets() => new()
        {
            { FactionType.Militants, new MilitantRuleset() },
            { FactionType.Zealots, new ZealotRuleset() },
            { FactionType.Guerrillas, new GuerrillaRuleset() },
            { FactionType.Guilds, new GuildRuleset() }
        };

        public static GameSession Create(
            IReadOnlyList<PlayerProfile> players,
            IReadOnlyList<ProvinceState> provinces,
            bool crisisEnabled = false,
            IDiceRoller dice = null)
        {
            var roller = dice ?? new SystemRandomDiceRoller();
            var rulesets = CreateRulesets();
            var combat = new RootCombatService(roller);
            var director = new CrisisDirector(rulesets);
            return new GameSession(combat, director, rulesets, roller, players, provinces, crisisEnabled);
        }
    }
}