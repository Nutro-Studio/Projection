using System;
using System.Collections.Generic;
using Core.Actions;
using Core.Board;
using Core.Combat;
using Core.Crisis;

namespace Core.Simulation
{
    public sealed class GameSession
    {
        public const int StartingGold = 10;
        public const int UnitCost = 3;
        public const int MissionaryCost = 3;
        public const int TraderCost = 2;
        public const int ChurchCost = 4;
        public const int ChurchTithe = 1;
        public const int RebellionCellCost = 2;
        public const int ConversionLimit = 2;
        public const int VictoryPointsPerProvince = 1;
        public const int TradingPostCost = 4;
        public const int StartingTradingPostCount = 2;
        public const int CaravanBaseGainPerProvince = 2;
        public const int CaravanTransitToll = 1;
        public const int CaravanMinTraversedForVp = 3;
        public const int HostileBuyoutBaseCost = 6;
        public const int HostileBuyoutUnitPremium = 2;
        public const int HostileBuyoutBuildingPremium = 2;
        public const int DefaultMercenaryPrice = 4;
        public const int FortCost = 5;
        public const int BuildFortVp = 1;
        public const int CaptureOrCrushRebellionVp = 2;
        public const int BuildChurchVp = 2;
        public const int DefendRebellionVp = 2;

        private readonly GameSessionState _state;
        private readonly ICrisisDirector _crisisDirector;
        private readonly MovementAndCombatHandler _movement;
        private readonly FactionActionHandler _factionActions;
        private readonly GuildCommerceHandler _guildCommerce;
        private readonly TurnAndCrisisHandler _turnAndCrisis;

        public event Action StateChanged
        {
            add => _state.StateChanged += value;
            remove => _state.StateChanged -= value;
        }

        public event Action<CombatReport> BattleResolved
        {
            add => _state.BattleResolved += value;
            remove => _state.BattleResolved -= value;
        }

        public event Action<ProvinceState> OnProvinceStateChanged
        {
            add => _state.ProvinceStateChanged += value;
            remove => _state.ProvinceStateChanged -= value;
        }

        public GameBalanceConfig Config => _state.Config;
        public IReadOnlyList<ProvinceState> Provinces => _state.Provinces;
        public PlayerProfile CurrentPlayer => _state.CurrentPlayer;
        public int TurnNumber => _state.TurnNumber;
        public bool IsGameOver => _state.IsGameOver;

        public GameSession(
            ICombatService combatService,
            ICrisisDirector crisisDirector,
            IReadOnlyDictionary<FactionType, IFactionRuleset> rulesets,
            IDiceRoller contestDice,
            IReadOnlyList<PlayerProfile> players,
            IReadOnlyList<ProvinceState> provinces,
            bool crisisEnabled,
            GameBalanceConfig config = null)
        {
            if (combatService == null) throw new ArgumentNullException(nameof(combatService));
            _crisisDirector = crisisDirector ?? throw new ArgumentNullException(nameof(crisisDirector));
            if (rulesets == null) throw new ArgumentNullException(nameof(rulesets));
            if (contestDice == null) throw new ArgumentNullException(nameof(contestDice));

            _state = new GameSessionState(players, provinces, crisisEnabled, config);
            _movement = new MovementAndCombatHandler(_state, combatService, rulesets);
            _factionActions = new FactionActionHandler(_state, contestDice);
            _guildCommerce = new GuildCommerceHandler(_state, _movement);
            _turnAndCrisis = new TurnAndCrisisHandler(_state, crisisDirector, rulesets, _guildCommerce);
        }

        // Queries
        public PlayerProfile GetPlayer(int playerId) => _state.GetPlayer(playerId);
        public int CountOwnedTradingPosts(int playerId) => _guildCommerce.CountOwnedTradingPosts(playerId);

        public int CountPlayerUnitsOfType(int playerId, UnitType type) =>
            _guildCommerce.CountPlayerUnitsOfType(playerId, type);

        public bool IsEmbargoedBy(int targetOwnerId, int guildPlayerId) =>
            _state.ActiveEmbargoes.Contains((targetOwnerId, guildPlayerId));

        public int GetProvinceStrength(int provinceId, int playerId) =>
            GetPlayer(playerId) == null
                ? 0
                : BoardHegemonyEvaluator.CalculateStrength(_state.Provinces[provinceId], playerId);

        public List<UnitInstance> GetUnitsForPlayer(int provinceId, int playerId, UnitType? typeFilter = null) =>
            _movement.GetUnitsForPlayer(provinceId, playerId, typeFilter);

        public int GetRebellionCellCost(int provinceId) =>
            _state.Provinces[provinceId].HasChurch
                ? _state.Config.RebellionCellCost * 2
                : _state.Config.RebellionCellCost;

        public int GetHostileBuyoutCost(int provinceId) => _guildCommerce.GetHostileBuyoutCost(provinceId);
        public int CountRebelProvinces(int playerId) => _turnAndCrisis.CountRebelProvinces(playerId);
        public int CountPresenceProvinces(int playerId) => _turnAndCrisis.CountPresenceProvinces(playerId);

        public int CountActiveRebellionProvinces(int playerId) =>
            _turnAndCrisis.CountActiveRebellionProvinces(playerId);

        public int CountMovableUnits(int provinceId)
        {
            var units = _state.Provinces[provinceId].Units;
            int currentPlayerId = CurrentPlayer.PlayerId;
            int movable = 0;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].OwnerPlayerId == currentPlayerId && !units[i].HasActed) movable++;
            }

            return movable;
        }

        public List<int> GetMovableUnitIds(int provinceId)
        {
            var units = _state.Provinces[provinceId].Units;
            int currentPlayerId = CurrentPlayer.PlayerId;
            var ids = new List<int>(units.Count);
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].OwnerPlayerId == currentPlayerId && !units[i].HasActed) ids.Add(units[i].Id);
            }

            return ids;
        }

        public List<int> GetCoexistingEnemyIds(int provinceId)
        {
            var units = _state.Provinces[provinceId].Units;
            int currentPlayerId = CurrentPlayer.PlayerId;
            var enemyIds = new List<int>();
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit.OwnerPlayerId != currentPlayerId
                    && unit.Type == UnitType.RegularMilitary
                    && !enemyIds.Contains(unit.OwnerPlayerId))
                {
                    enemyIds.Add(unit.OwnerPlayerId);
                }
            }

            return enemyIds;
        }

        // Movement & Combat
        public ActionResult ValidateMoveOrigin(int provinceId) => _movement.ValidateMoveOrigin(provinceId);
        public ActionResult MoveArmy(MoveUnitsCommand command) => _movement.MoveArmy(command);

        public ActionResult InitiateBattle(int provinceId, int defenderPlayerId) =>
            _movement.InitiateBattle(provinceId, defenderPlayerId);

        // Faction Actions
        public ActionResult BuyUnit(int provinceId) => _factionActions.BuyUnit(provinceId);
        public ActionResult BuyMissionary(int provinceId) => _factionActions.BuyMissionary(provinceId);
        public ActionResult BuildFort(int provinceId) => _factionActions.BuildFort(provinceId);
        public ActionResult BuildChurch(int provinceId) => _factionActions.BuildChurch(provinceId);
        public ActionResult Consecrate(int provinceId) => _factionActions.Consecrate(provinceId);

        public ActionResult DivineConversion(int provinceId, int missionaryCount = 1) =>
            _factionActions.DivineConversion(provinceId, missionaryCount);

        public ActionResult PlaceRebellionCell(int provinceId) => _factionActions.PlaceRebellionCell(provinceId);
        public ActionResult InciteRebellion(int provinceId) => _factionActions.InciteRebellion(provinceId);
        public ActionResult MartialLaw(int provinceId) => _factionActions.MartialLaw(provinceId);
        public ActionResult Inquisition(int provinceId) => _factionActions.Inquisition(provinceId);
        public ActionResult CleanseProvince(int provinceId) => _factionActions.CleanseProvince(provinceId);

        // Guild Commerce & Counters
        public ActionResult BuyTrader(int provinceId) => _guildCommerce.BuyTrader(provinceId);
        public ActionResult BuildTradingPost(int provinceId) => _guildCommerce.BuildTradingPost(provinceId);
        public ActionResult RunCaravan(IReadOnlyList<int> path) => _guildCommerce.RunCaravan(path);
        public ActionResult DeclareEmbargo(int guildPlayerId) => _guildCommerce.DeclareEmbargo(guildPlayerId);

        public ActionResult RaidCaravan(int provinceId, int guildPlayerId) =>
            _guildCommerce.RaidCaravan(provinceId, guildPlayerId);

        public ActionResult HostileBuyout(int provinceId) => _guildCommerce.HostileBuyout(provinceId);

        public ActionResult SellMercenaryContract(int provinceId, int buyerPlayerId, int? goldPrice = null) =>
            _guildCommerce.SellMercenaryContract(provinceId, buyerPlayerId,
                goldPrice ?? _state.Config.DefaultMercenaryPrice);

        // Turn & Crisis
        public bool CanCapitulate() => !IsGameOver && _crisisDirector.CanCapitulate(CurrentPlayer);

        public bool CanVoluntaryShift(FactionType targetFaction) =>
            !IsGameOver && CurrentPlayer.ActionPoints > 0 &&
            _crisisDirector.CanTriggerVoluntaryShift(CurrentPlayer, targetFaction,
                _state.CountProvinces(CurrentPlayer.PlayerId));

        public bool CanAscend() =>
            !IsGameOver && CurrentPlayer.ActionPoints > 0 &&
            _crisisDirector.CanTriggerAscension(CurrentPlayer, CountRebelProvinces(CurrentPlayer.PlayerId));

        public ActionResult EndTurn() => _turnAndCrisis.EndTurn();
        public ActionResult Capitulate() => _turnAndCrisis.Capitulate();
        public ActionResult VoluntaryShift(FactionType targetFaction) => _turnAndCrisis.VoluntaryShift(targetFaction);
        public ActionResult Ascend(FactionType targetLandedFaction) => _turnAndCrisis.Ascend(targetLandedFaction);
    }
}