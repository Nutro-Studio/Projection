using System;
using System.Collections.Generic;
using Core.Crisis;
using UnityEngine;

namespace Core.Combat
{
    public class CombatCoordinator : MonoBehaviour
    {
        public static CombatCoordinator Instance { get; private set; }

        private ICombatService _combatService;
        private ICrisisDirector _crisisDirector;
        private IReadOnlyDictionary<FactionType, IFactionRuleset> _rulesets;

        // Events for UI
        public event Action<CombatResult> OnCombatResolved;
        public event Action<CrisisShiftResult> OnRulesetShifted;
        public event Action<PlayerProfile> OnGameWon;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            InitializeServices();
        }

        private void InitializeServices()
        {
            _rulesets = new Dictionary<FactionType, IFactionRuleset>
            {
                { FactionType.Militants, new MilitantRuleset() },
                { FactionType.Zealots, new ZealotRuleset() },
                { FactionType.Guerrillas, new GuerrillaRuleset() },
                { FactionType.Guilds, new GuildRuleset() }
            };

            _combatService = new RootCombatService(new SystemRandomDiceRoller());
            _crisisDirector = new CrisisDirector(_rulesets);

            _crisisDirector.OnRulesetShifted += shift => OnRulesetShifted?.Invoke(shift);
            _crisisDirector.OnVictoryAchieved += winner => OnGameWon?.Invoke(winner);
        }

        /// <summary>
        /// Entry point for movement system when armies collide.
        /// </summary>
        public CombatResult ResolveSkirmish(
            IReadOnlyList<UnitInstance> attackers,
            IReadOnlyList<UnitInstance> defenders,
            FactionType defenderFaction,
            bool hasFortification)
        {
            bool receivesFortBonus = _rulesets.TryGetValue(defenderFaction, out var ruleset)
                                     && ruleset.ReceivesFortificationBonus;

            var context = new CombatContext(
                Attackers: attackers,
                Defenders: defenders,
                DefenderHasFort: hasFortification,
                DefenderReceivesFortBonus: receivesFortBonus
            );

            var result = _combatService.ResolveBattle(context);
            OnCombatResolved?.Invoke(result);
            return result;
        }

        /// <summary>
        /// Entry point for turn end / province change validation.
        /// </summary>
        public PlayerProfile EvaluatePlayerTurnState(PlayerProfile player, int controlledProvinces)
        {
            if (_crisisDirector.CheckVictory(player))
            {
                return player;
            }

            if (_crisisDirector.CanTriggerCrisis(player, controlledProvinces))
            {
                var crisisResult = _crisisDirector.ExecuteForcedCrisis(player);
                return crisisResult.UpdatedProfile;
            }

            return player;
        }

        public IFactionRuleset GetRuleset(FactionType faction) => _rulesets[faction];
    }
}