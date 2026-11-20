using System;
using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;

namespace Presentation.Prototype
{
    /// <summary>
    /// Stand-in for the economy and movement systems so the HUD can run the prototype scenario.
    /// Economy and map services should replace this class once they land in Core.
    /// </summary>
    public sealed class PrototypeGameSession
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

        private readonly IGameRules _rules;
        private readonly bool _crisisEnabled;
        private readonly PlayerProfile[] _players;
        private readonly ProvinceState[] _provinces;
        private readonly HashSet<(int EmbargoerId, int GuildId)> _activeEmbargoes = new();

        private int _currentPlayerIndex;
        private int _nextUnitId;
        private int _qualifyingCaravanRoutesThisTurn;

        public event Action StateChanged;
        public event Action<BattleReport> BattleResolved;
        public event Action<ProvinceState> OnProvinceStateChanged;

        public IReadOnlyList<ProvinceState> Provinces => _provinces;
        public PlayerProfile CurrentPlayer => _players[_currentPlayerIndex];
        public int TurnNumber { get; private set; } = 1;
        public bool IsGameOver { get; private set; }

        /// <param name="crisisEnabled">
        /// The three-province test map can never hold the crisis threshold of three, so that scene turns it off.
        /// </param>
        public PrototypeGameSession(
            IGameRules rules,
            IReadOnlyList<PlayerProfile> players,
            IReadOnlyList<ProvinceState> provinces,
            bool crisisEnabled)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _crisisEnabled = crisisEnabled;

            _players = new PlayerProfile[players.Count];
            for (int i = 0; i < players.Count; i++)
            {
                _players[i] = players[i];
            }

            _provinces = new ProvinceState[provinces.Count];
            for (int i = 0; i < provinces.Count; i++)
            {
                _provinces[i] = EvaluateHegemony(provinces[i]);
                for (int u = 0; u < provinces[i].Units.Count; u++)
                {
                    _nextUnitId = Math.Max(_nextUnitId, provinces[i].Units[u].Id + 1);
                }
            }
        }

        public PlayerProfile GetPlayer(int playerId)
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i].PlayerId == playerId)
                {
                    return _players[i];
                }
            }

            return null;
        }

        public int CountOwnedTradingPosts(int playerId)
        {
            int count = 0;
            for (int i = 0; i < _provinces.Length; i++)
            {
                if (_provinces[i].TradingPostOwnerId == playerId)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountPlayerUnitsOfType(int playerId, UnitType type)
        {
            int count = 0;
            for (int i = 0; i < _provinces.Length; i++)
            {
                var units = _provinces[i].Units;
                for (int u = 0; u < units.Count; u++)
                {
                    if (units[u].OwnerPlayerId == playerId && units[u].Type == type)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public bool IsEmbargoedBy(int targetOwnerId, int guildPlayerId) =>
            _activeEmbargoes.Contains((targetOwnerId, guildPlayerId));

        /// <summary>
        /// Calculates total Root-style strength points for a player in a province:
        /// each RegularMilitary unit = 1, Fort (if owned by player) = 1, Church (if owned by player) = 1.
        /// </summary>
        public int GetProvinceStrength(int provinceId, int playerId)
        {
            var province = _provinces[provinceId];
            var player = GetPlayer(playerId);
            if (player == null) return 0;

            int strength = 0;
            for (int i = 0; i < province.Units.Count; i++)
            {
                var unit = province.Units[i];
                if (unit.OwnerPlayerId == playerId && unit.Type == UnitType.RegularMilitary)
                {
                    strength++;
                }
            }

            if (province.HasFort && province.OwnerId == playerId)
            {
                strength++;
            }

            if (province.HasChurch && province.ChurchOwnerId == playerId)
            {
                strength++;
            }

            return strength;
        }

        public List<UnitInstance> GetUnitsForPlayer(int provinceId, int playerId, UnitType? typeFilter = null)
        {
            var province = _provinces[provinceId];
            var result = new List<UnitInstance>();

            for (int i = 0; i < province.Units.Count; i++)
            {
                var unit = province.Units[i];
                if (unit.OwnerPlayerId != playerId) continue;
                if (typeFilter.HasValue && unit.Type != typeFilter.Value) continue;
                result.Add(unit);
            }

            return result;
        }

        public int CountMovableUnits(int provinceId)
        {
            var units = _provinces[provinceId].Units;
            int currentPlayerId = CurrentPlayer.PlayerId;
            int movable = 0;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].OwnerPlayerId == currentPlayerId && !units[i].HasActed)
                {
                    movable++;
                }
            }

            return movable;
        }

        public CommandResult ValidateMoveOrigin(int provinceId)
        {
            if (IsGameOver) return CommandResult.GameOver;
            if (CurrentPlayer.ActionPoints <= 0) return CommandResult.NoActionPoints;

            var ownUnits = GetUnitsForPlayer(provinceId, CurrentPlayer.PlayerId);
            if (ownUnits.Count == 0) return CommandResult.NoUnits;
            if (CountMovableUnits(provinceId) == 0) return CommandResult.UnitsAlreadyMoved;

            return CommandResult.Ok;
        }

        /// <summary>
        /// Zealots and Guilds cannot recruit standing armies; they field only converted, mercenary, or inherited units.
        /// </summary>
        public CommandResult BuyUnit(int provinceId)
        {
            var check = ValidateOwnProvinceAction(provinceId);
            if (check != CommandResult.Ok) return check;
            if (CurrentPlayer.Faction == FactionType.Zealots || CurrentPlayer.Faction == FactionType.Guilds)
            {
                return CommandResult.WrongFaction;
            }

            return Recruit(provinceId, UnitCost, UnitType.RegularMilitary);
        }

        public CommandResult BuyMissionary(int provinceId)
        {
            var check = ValidateOwnProvinceAction(provinceId);
            if (check != CommandResult.Ok) return check;
            if (CurrentPlayer.Faction != FactionType.Zealots) return CommandResult.WrongFaction;

            return Recruit(provinceId, MissionaryCost, UnitType.Missionary);
        }

        public CommandResult BuyTrader(int provinceId)
        {
            if (IsGameOver) return CommandResult.GameOver;
            if (CurrentPlayer.ActionPoints <= 0) return CommandResult.NoActionPoints;
            if (CurrentPlayer.Faction != FactionType.Guilds) return CommandResult.WrongFaction;

            var province = _provinces[provinceId];
            bool canRecruitHere = province.OwnerId == CurrentPlayer.PlayerId
                                  || province.TradingPostOwnerId == CurrentPlayer.PlayerId;
            if (!canRecruitHere) return CommandResult.NotYourProvince;

            if (CountPlayerUnitsOfType(CurrentPlayer.PlayerId, UnitType.Trader) >=
                CountOwnedTradingPosts(CurrentPlayer.PlayerId))
            {
                return CommandResult.TraderCapReached;
            }

            return Recruit(provinceId, TraderCost, UnitType.Trader);
        }

        public CommandResult BuildChurch(int provinceId)
        {
            var check = ValidateOwnProvinceAction(provinceId);
            if (check != CommandResult.Ok) return check;

            var province = _provinces[provinceId];
            var player = CurrentPlayer;
            if (player.Faction != FactionType.Zealots) return CommandResult.WrongFaction;
            if (province.HasChurch) return CommandResult.AlreadyBuilt;
            if (player.Gold < ChurchCost) return CommandResult.NotEnoughGold;

            SetProvince(province.WithChurch(player.PlayerId));
            AddGold(_currentPlayerIndex, -ChurchCost);
            SpendActionPoint();
            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        /// <summary>
        /// A Missionary spends the action to place a Religious Presence token in the province it occupies.
        /// </summary>
        public CommandResult Consecrate(int provinceId)
        {
            var check = ValidateAction(FactionType.Zealots);
            if (check != CommandResult.Ok) return check;

            var province = _provinces[provinceId];
            if (IndexOfPlayerUnitType(province.Units, CurrentPlayer.PlayerId, UnitType.Missionary) < 0)
            {
                return CommandResult.NoMissionary;
            }

            if (province.PresenceToken != null) return CommandResult.AlreadyBuilt;

            SetProvince(province.WithPresence(new ReligiousPresenceToken(CurrentPlayer.PlayerId)));
            SpendActionPoint();
            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        /// <summary>
        /// Expends 1 Missionary in <paramref name="provinceId"/> for a contested 3d4 roll against co-existing
        /// enemy RegularMilitary units. On a win, converts up to (missionariesPresent + 1) enemy military units
        /// capped by (zealotRoll - enemyRoll) with zero casualties.
        /// </summary>
        public CommandResult DivineConversion(int provinceId)
        {
            var check = ValidateAction(FactionType.Zealots);
            if (check != CommandResult.Ok) return check;

            var province = _provinces[provinceId];
            var player = CurrentPlayer;

            int missionaryIndex = IndexOfPlayerUnitType(province.Units, player.PlayerId, UnitType.Missionary);
            if (missionaryIndex < 0) return CommandResult.NoMissionary;

            int missionariesPresent = GetUnitsForPlayer(provinceId, player.PlayerId, UnitType.Missionary).Count;
            int enemyMilitaryCount = 0;
            for (int i = 0; i < province.Units.Count; i++)
            {
                var unit = province.Units[i];
                if (unit.OwnerPlayerId != player.PlayerId && unit.Type == UnitType.RegularMilitary)
                {
                    enemyMilitaryCount++;
                }
            }

            if (enemyMilitaryCount == 0) return CommandResult.NoTargetUnits;

            int zealotRoll = _rules.RollContest();
            int enemyRoll = _rules.RollContest();
            int rollGain = Math.Max(0, zealotRoll - enemyRoll);
            int maxGain = missionariesPresent + 1;
            int toConvert = Math.Min(Math.Min(rollGain, maxGain), enemyMilitaryCount);

            var updatedUnits = new List<UnitInstance>(province.Units.Count - 1);
            int convertedSoFar = 0;

            for (int i = 0; i < province.Units.Count; i++)
            {
                if (i == missionaryIndex) continue; // Expend 1 Missionary

                var unit = province.Units[i];
                if (convertedSoFar < toConvert
                    && unit.OwnerPlayerId != player.PlayerId
                    && unit.Type == UnitType.RegularMilitary)
                {
                    updatedUnits.Add(unit with { OwnerPlayerId = player.PlayerId, HasActed = true });
                    convertedSoFar++;
                }
                else
                {
                    updatedUnits.Add(unit);
                }
            }

            SetProvince(province.WithUnits(updatedUnits));
            SpendActionPoint();
            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        /// <summary>
        /// Sanctuary: a Church doubles the Gold a Guerrilla pays to raise a Rebellion cell in the province.
        /// </summary>
        public int GetRebellionCellCost(int provinceId) =>
            _provinces[provinceId].HasChurch ? RebellionCellCost * 2 : RebellionCellCost;

        /// <summary>
        /// Guerrillas are non-landed, so a cell can go into any province that does not hold one yet.
        /// </summary>
        public CommandResult PlaceRebellionCell(int provinceId)
        {
            var check = ValidateAction(FactionType.Guerrillas);
            if (check != CommandResult.Ok) return check;

            var province = _provinces[provinceId];
            int cost = GetRebellionCellCost(provinceId);
            if (province.RebellionCell != null) return CommandResult.AlreadyBuilt;
            if (CurrentPlayer.Gold < cost) return CommandResult.NotEnoughGold;

            SetProvince(province.WithRebellionCell(new RebellionCell(CurrentPlayer.PlayerId, false)));
            AddGold(_currentPlayerIndex, -cost);
            SpendActionPoint();
            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        /// <summary>
        /// Turns the player's undercover cell in the province into an open rebellion.
        /// </summary>
        public CommandResult InciteRebellion(int provinceId)
        {
            var check = ValidateAction(FactionType.Guerrillas);
            if (check != CommandResult.Ok) return check;

            var province = _provinces[provinceId];
            var cell = province.RebellionCell;
            if (cell == null || cell.OwnerId != CurrentPlayer.PlayerId) return CommandResult.NoRebellionCell;
            if (cell.IsOpenRebellion) return CommandResult.AlreadyBuilt;

            SetProvince(province.WithRebellionCell(new RebellionCell(cell.OwnerId, true)));
            SpendActionPoint();
            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        /// <summary>
        /// Militant counter to Guerrillas: removes an open Rebellion from a province where Militants hold presence.
        /// Undercover cells cannot be targeted until they rise.
        /// </summary>
        public CommandResult MartialLaw(int provinceId)
        {
            var check = ValidatePresence(provinceId, CurrentPlayer.Faction == FactionType.Militants);
            if (check != CommandResult.Ok) return check;

            var province = _provinces[provinceId];
            if (province.RebellionCell == null || !province.RebellionCell.IsOpenRebellion)
            {
                return CommandResult.NothingToPurge;
            }

            SetProvince(province.WithRebellionCell(null));
            SpendActionPoint();
            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        /// <summary>
        /// Militant counter to Zealots: removes the Religious Presence token from a province where Militants hold presence.
        /// </summary>
        public CommandResult Inquisition(int provinceId)
        {
            var check = ValidatePresence(provinceId, CurrentPlayer.Faction == FactionType.Militants);
            if (check != CommandResult.Ok) return check;

            var province = _provinces[provinceId];
            if (province.PresenceToken == null) return CommandResult.NothingToPurge;

            SetProvince(province.WithPresence(null));
            SpendActionPoint();
            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        /// <summary>
        /// A landed army occupying the province dismantles an enemy Church or Trading Post (unless the province is
        /// fortified, which keeps it a Bastion) and cleanses an enemy Religious Presence token.
        /// </summary>
        public CommandResult CleanseProvince(int provinceId)
        {
            var player = CurrentPlayer;
            bool isLanded = player.Faction == FactionType.Militants || player.Faction == FactionType.Zealots;
            var check = ValidatePresence(provinceId, isLanded);
            if (check != CommandResult.Ok) return check;

            var province = _provinces[provinceId];
            bool dismantleChurch = province.HasChurch && province.ChurchOwnerId != player.PlayerId && !province.HasFort;
            bool dismantleTradingPost = province.HasTradingPost && province.TradingPostOwnerId != player.PlayerId &&
                                        !province.HasFort;
            bool cleanseToken = province.PresenceToken != null && province.PresenceToken.OwnerId != player.PlayerId;
            if (!dismantleChurch && !dismantleTradingPost && !cleanseToken) return CommandResult.NothingToPurge;

            if (dismantleChurch) province = province.WithChurch(ProvinceState.NoChurch);
            if (dismantleTradingPost) province = province.WithTradingPost(ProvinceState.NoTradingPost);
            if (cleanseToken) province = province.WithPresence(null);

            SetProvince(province);
            SpendActionPoint();
            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        public List<int> GetMovableUnitIds(int provinceId)
        {
            var units = _provinces[provinceId].Units;
            int currentPlayerId = CurrentPlayer.PlayerId;
            var ids = new List<int>(units.Count);
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].OwnerPlayerId == currentPlayerId && !units[i].HasActed)
                {
                    ids.Add(units[i].Id);
                }
            }

            return ids;
        }

        /// <summary>
        /// Decoupled Move: shifts selected units across adjacent nodes without triggering automatic combat.
        /// Opposing forces can co-exist in the target province; hegemony is recalculated dynamically.
        /// </summary>
        public CommandResult MoveArmy(MoveArmyCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            int fromId = command.OriginProvinceId;
            int toId = command.TargetProvinceId;
            var originCheck = ValidateMoveOrigin(fromId);
            if (originCheck != CommandResult.Ok) return originCheck;
            if (command.SelectedUnitIds == null || command.SelectedUnitIds.Count == 0)
            {
                return CommandResult.NoUnitsSelected;
            }

            var from = _provinces[fromId];
            var to = _provinces[toId];
            if (fromId == toId || !from.IsAdjacentTo(toId)) return CommandResult.NotAdjacent;

            int playerId = CurrentPlayer.PlayerId;
            var selection =
                SplitSelectedUnits(from, playerId, command.SelectedUnitIds, out var movers, out var stayers);
            if (selection != CommandResult.Ok) return selection;

            SetProvince(from.WithUnits(stayers));
            SetProvince(to.WithUnits(Concat(to.Units, movers)));
            SpendActionPoint();
            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        /// <summary>
        /// Dedicated Battle Action: expends 1 AP to attack a specific co-existing enemy player in a province.
        /// </summary>
        public CommandResult InitiateBattle(int provinceId, int defenderPlayerId)
        {
            if (IsGameOver) return CommandResult.GameOver;
            if (CurrentPlayer.ActionPoints <= 0) return CommandResult.NoActionPoints;

            var attacker = CurrentPlayer;
            if (attacker.PlayerId == defenderPlayerId) return CommandResult.InvalidBattleTarget;

            var defender = GetPlayer(defenderPlayerId);
            if (defender == null) return CommandResult.InvalidBattleTarget;

            var province = _provinces[provinceId];
            var attackers = GetUnitsForPlayer(provinceId, attacker.PlayerId, UnitType.RegularMilitary);
            if (attackers.Count == 0) return CommandResult.NoMilitaryUnits;

            var defenders = GetUnitsForPlayer(provinceId, defenderPlayerId, UnitType.RegularMilitary);
            if (defenders.Count == 0) return CommandResult.NoTargetUnits;

            bool defenderHasFort = province.HasFort && province.OwnerId == defenderPlayerId;
            var result = _rules.ResolveSkirmish(attackers, defenders, defender.Faction, defenderHasFort);

            var bystanders = new List<UnitInstance>(province.Units.Count);
            for (int i = 0; i < province.Units.Count; i++)
            {
                var unit = province.Units[i];
                bool isCombatantAttacker =
                    unit.OwnerPlayerId == attacker.PlayerId && unit.Type == UnitType.RegularMilitary;
                bool isCombatantDefender =
                    unit.OwnerPlayerId == defenderPlayerId && unit.Type == UnitType.RegularMilitary;
                if (!isCombatantAttacker && !isCombatantDefender)
                {
                    bystanders.Add(unit);
                }
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

            SetProvince(province.WithUnits(updatedUnits));
            SpendActionPoint();

            bool attackerHoldsHegemony = _provinces[provinceId].OwnerId == attacker.PlayerId;
            BattleResolved?.Invoke(new BattleReport(
                province.Name,
                attacker.PlayerId,
                defenderPlayerId,
                attackers.Count,
                defenders.Count,
                captured || attackerHoldsHegemony,
                result));

            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        public CommandResult EndTurn()
        {
            if (IsGameOver) return CommandResult.GameOver;

            var player = CurrentPlayer;
            int controlled = CountProvinces(player.PlayerId);
            int gold = player.Gold + _rules.CalculateRevenue(player.Faction, controlled);
            int vpGain = player.Faction == FactionType.Guilds
                ? CalculateGuildVictoryPoints(player.PlayerId, gold)
                : controlled * VictoryPointsPerProvince;
            int victoryPoints = player.VictoryPoints + vpGain;

            var beforeRules = CopyProfile(player, player.Faction, gold, victoryPoints,
                CollectUnits(player.PlayerId));

            int provincesForRules = _crisisEnabled
                ? controlled
                : Math.Max(controlled, CrisisDirector.CrisisProvinceThreshold);
            var updated = _rules.EvaluatePlayerTurnState(beforeRules, provincesForRules);
            _players[_currentPlayerIndex] = updated;
            _qualifyingCaravanRoutesThisTurn = 0;

            IsGameOver = updated.VictoryPoints >= CrisisDirector.VictoryPointsThreshold;
            if (!IsGameOver)
            {
                ResetUnitActionFlags();
                _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Length;
                if (_currentPlayerIndex == 0)
                {
                    TurnNumber++;
                }

                _players[_currentPlayerIndex] =
                    CurrentPlayer with { ActionPoints = PlayerProfile.BaseActionPoints };
                CollectChurchTithes();
            }

            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        private int CalculateGuildVictoryPoints(int playerId, int bankedGold)
        {
            int tradingPosts = CountOwnedTradingPosts(playerId);
            int goldVp = Math.Max(0, bankedGold) / 10;
            int routeVp = _qualifyingCaravanRoutesThisTurn * 2;
            return tradingPosts + goldVp + routeVp;
        }

        /// <summary>
        /// Dynamically evaluates Province Hegemony via strict majority of total strength points:
        /// each RegularMilitary unit = 1, Fort (owned by current controller) = 1, Church (owned by builder) = 1.
        /// </summary>
        private ProvinceState EvaluateHegemony(ProvinceState province)
        {
            int bestPlayerId = ProvinceState.NeutralOwner;
            int bestStrength = 0;
            bool isTie = false;

            for (int p = 0; p < _players.Length; p++)
            {
                int playerId = _players[p].PlayerId;

                int strength = 0;
                for (int u = 0; u < province.Units.Count; u++)
                {
                    var unit = province.Units[u];
                    if (unit.OwnerPlayerId == playerId && unit.Type == UnitType.RegularMilitary)
                    {
                        strength++;
                    }
                }

                if (province.HasFort && province.OwnerId == playerId)
                {
                    strength++;
                }

                if (province.HasChurch && province.ChurchOwnerId == playerId)
                {
                    strength++;
                }

                if (strength > bestStrength)
                {
                    bestStrength = strength;
                    bestPlayerId = playerId;
                    isTie = false;
                }
                else if (strength > 0 && strength == bestStrength)
                {
                    isTie = true;
                }
            }

            if (bestStrength == 0)
            {
                return province;
            }

            int newOwnerId = isTie ? ProvinceState.NeutralOwner : bestPlayerId;
            return newOwnerId == province.OwnerId
                ? province
                : province.WithOccupation(newOwnerId, province.Units);
        }

        /// <summary>
        /// Retreating units fall back to the first adjacent province held by that player.
        /// With nowhere to go they are lost.
        /// </summary>
        private void RetreatUnits(ProvinceState originProvince, int playerId, IReadOnlyList<UnitInstance> survivors)
        {
            if (survivors.Count == 0) return;

            for (int i = 0; i < originProvince.Neighbours.Count; i++)
            {
                var neighbour = _provinces[originProvince.Neighbours[i]];
                if (neighbour.OwnerId == playerId)
                {
                    SetProvince(neighbour.WithUnits(Concat(neighbour.Units, survivors)));
                    return;
                }
            }
        }

        private static CommandResult SplitSelectedUnits(
            ProvinceState province,
            int movingPlayerId,
            IReadOnlyList<int> selectedUnitIds,
            out List<UnitInstance> movers,
            out List<UnitInstance> stayers)
        {
            int count = province.Units.Count;
            movers = new List<UnitInstance>(selectedUnitIds.Count);
            stayers = new List<UnitInstance>(count);
            for (int i = 0; i < count; i++)
            {
                var unit = province.Units[i];
                if (unit.OwnerPlayerId != movingPlayerId || !Contains(selectedUnitIds, unit.Id))
                {
                    stayers.Add(unit);
                    continue;
                }

                if (unit.HasActed) return CommandResult.UnitsAlreadyMoved;

                movers.Add(unit with { HasActed = true });
            }

            return movers.Count == selectedUnitIds.Count ? CommandResult.Ok : CommandResult.InvalidUnitSelection;
        }

        private static bool Contains(IReadOnlyList<int> ids, int id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private CommandResult ValidateOwnProvinceAction(int provinceId)
        {
            if (IsGameOver) return CommandResult.GameOver;
            if (CurrentPlayer.ActionPoints <= 0) return CommandResult.NoActionPoints;
            if (_provinces[provinceId].OwnerId != CurrentPlayer.PlayerId) return CommandResult.NotYourProvince;

            return CommandResult.Ok;
        }

        private CommandResult ValidateAction(FactionType requiredFaction)
        {
            if (IsGameOver) return CommandResult.GameOver;
            if (CurrentPlayer.ActionPoints <= 0) return CommandResult.NoActionPoints;
            if (CurrentPlayer.Faction != requiredFaction) return CommandResult.WrongFaction;

            return CommandResult.Ok;
        }

        private CommandResult ValidatePresence(int provinceId, bool factionAllowed)
        {
            if (IsGameOver) return CommandResult.GameOver;
            if (CurrentPlayer.ActionPoints <= 0) return CommandResult.NoActionPoints;
            if (!factionAllowed) return CommandResult.WrongFaction;
            if (GetUnitsForPlayer(provinceId, CurrentPlayer.PlayerId, UnitType.RegularMilitary).Count == 0)
            {
                return CommandResult.NoMilitaryUnits;
            }

            return CommandResult.Ok;
        }

        private CommandResult Recruit(int provinceId, int cost, UnitType type)
        {
            var province = _provinces[provinceId];
            var player = CurrentPlayer;
            if (player.Gold < cost) return CommandResult.NotEnoughGold;

            var units = new List<UnitInstance>(province.Units)
            {
                new(_nextUnitId++, player.PlayerId, player.Faction, type, 1, 1)
            };

            SetProvince(province.WithUnits(units));
            AddGold(_currentPlayerIndex, -cost);
            SpendActionPoint();
            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        private void CollectChurchTithes()
        {
            var payer = CurrentPlayer;
            if (payer.Faction == FactionType.Zealots || payer.Faction == FactionType.Guerrillas) return;

            for (int i = 0; i < _provinces.Length; i++)
            {
                var province = _provinces[i];
                if (!province.HasChurch) continue;

                int churchOwnerIndex = IndexOfPlayer(province.ChurchOwnerId);
                if (churchOwnerIndex < 0 || _players[churchOwnerIndex].Faction != FactionType.Zealots) continue;

                var kept = new List<UnitInstance>(province.Units.Count);
                for (int u = 0; u < province.Units.Count; u++)
                {
                    var unit = province.Units[u];
                    if (unit.OwnerPlayerId != payer.PlayerId)
                    {
                        kept.Add(unit);
                        continue;
                    }

                    if (CurrentPlayer.Gold < ChurchTithe) continue;

                    AddGold(_currentPlayerIndex, -ChurchTithe);
                    AddGold(churchOwnerIndex, ChurchTithe);
                    kept.Add(unit);
                }

                if (kept.Count != province.Units.Count)
                {
                    SetProvince(province.WithUnits(kept));
                }
            }
        }

        private void ResetUnitActionFlags()
        {
            for (int i = 0; i < _provinces.Length; i++)
            {
                var province = _provinces[i];
                bool changed = false;
                var resetUnits = new List<UnitInstance>(province.Units.Count);
                for (int u = 0; u < province.Units.Count; u++)
                {
                    var unit = province.Units[u];
                    if (unit.HasActed)
                    {
                        resetUnits.Add(unit with { HasActed = false });
                        changed = true;
                    }
                    else
                    {
                        resetUnits.Add(unit);
                    }
                }

                if (changed)
                {
                    _provinces[i] = province.WithUnits(resetUnits);
                }
            }
        }

        private int IndexOfPlayer(int playerId)
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i].PlayerId == playerId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int IndexOfPlayerUnitType(IReadOnlyList<UnitInstance> units, int playerId, UnitType type)
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].OwnerPlayerId == playerId && units[i].Type == type)
                {
                    return i;
                }
            }

            return -1;
        }

        private void AddGold(int playerIndex, int amount)
        {
            var player = _players[playerIndex];
            _players[playerIndex] = player with { Gold = player.Gold + amount };
        }

        private void SpendActionPoint()
        {
            var player = CurrentPlayer;
            _players[_currentPlayerIndex] = player with { ActionPoints = player.ActionPoints - 1 };
        }

        private int CountProvinces(int playerId)
        {
            int count = 0;
            for (int i = 0; i < _provinces.Length; i++)
            {
                if (_provinces[i].OwnerId == playerId)
                {
                    count++;
                }
            }

            return count;
        }

        private List<UnitInstance> CollectUnits(int playerId)
        {
            var units = new List<UnitInstance>();

            for (int i = 0; i < _provinces.Length; i++)
            {
                var provinceUnits = _provinces[i].Units;
                for (int u = 0; u < provinceUnits.Count; u++)
                {
                    if (provinceUnits[u].OwnerPlayerId == playerId)
                    {
                        units.Add(provinceUnits[u]);
                    }
                }
            }

            return units;
        }

        private void SetProvince(ProvinceState province)
        {
            var evaluated = EvaluateHegemony(province);
            _provinces[evaluated.Id] = evaluated;
            OnProvinceStateChanged?.Invoke(evaluated);
        }

        private static List<UnitInstance> Concat(IReadOnlyList<UnitInstance> first, IReadOnlyList<UnitInstance> second)
        {
            var units = new List<UnitInstance>(first.Count + second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                units.Add(first[i]);
            }

            for (int i = 0; i < second.Count; i++)
            {
                units.Add(second[i]);
            }

            return units;
        }

        private static PlayerProfile CopyProfile(
            PlayerProfile profile,
            FactionType faction,
            int gold,
            int victoryPoints,
            IReadOnlyList<UnitInstance> units) =>
            new(profile.PlayerId, faction, gold, victoryPoints, units, profile.ActionPoints);

        public List<int> GetCoexistingEnemyIds(int provinceId)
        {
            var units = _provinces[provinceId].Units;
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

        public bool CanVoluntaryShift(FactionType targetFaction)
        {
            if (IsGameOver || CurrentPlayer.ActionPoints <= 0) return false;
            return _rules.CanTriggerVoluntaryShift(CurrentPlayer, targetFaction,
                CountProvinces(CurrentPlayer.PlayerId));
        }

        public bool CanAscend()
        {
            if (IsGameOver || CurrentPlayer.ActionPoints <= 0) return false;
            return _rules.CanTriggerAscension(CurrentPlayer, CountProvinces(CurrentPlayer.PlayerId));
        }

        public CommandResult VoluntaryShift(FactionType targetFaction)
        {
            if (IsGameOver) return CommandResult.GameOver;
            if (CurrentPlayer.ActionPoints <= 0) return CommandResult.NoActionPoints;

            var player = CurrentPlayer;
            int controlled = CountProvinces(player.PlayerId);
            if (!_rules.CanTriggerVoluntaryShift(player, targetFaction, controlled))
            {
                return CommandResult.InvalidRulesetShift;
            }

            var withUnits = player with { Units = CollectUnits(player.PlayerId) };
            var shift = _rules.ExecuteVoluntaryShift(withUnits, targetFaction);
            ApplyShiftResult(shift);
            return CommandResult.Ok;
        }

        public CommandResult Ascend(FactionType targetLandedFaction)
        {
            if (IsGameOver) return CommandResult.GameOver;
            if (CurrentPlayer.ActionPoints <= 0) return CommandResult.NoActionPoints;

            var player = CurrentPlayer;
            int controlled = CountProvinces(player.PlayerId);
            if (!_rules.CanTriggerAscension(player, controlled))
            {
                return CommandResult.InvalidRulesetShift;
            }

            var withUnits = player with { Units = CollectUnits(player.PlayerId) };
            var shift = _rules.ExecuteAscension(withUnits, targetLandedFaction);
            ApplyShiftResult(shift);
            return CommandResult.Ok;
        }

        private void ApplyShiftResult(CrisisShiftResult shift)
        {
            _players[_currentPlayerIndex] = shift.UpdatedProfile with
            {
                ActionPoints = CurrentPlayer.ActionPoints - 1
            };

            if (shift.PreviousFaction == FactionType.Militants && shift.IsVoluntary)
            {
                var survivingIds = new HashSet<int>();
                for (int i = 0; i < shift.UpdatedProfile.Units.Count; i++)
                {
                    survivingIds.Add(shift.UpdatedProfile.Units[i].Id);
                }

                for (int p = 0; p < _provinces.Length; p++)
                {
                    var province = _provinces[p];
                    var kept = new List<UnitInstance>(province.Units.Count);
                    for (int u = 0; u < province.Units.Count; u++)
                    {
                        var unit = province.Units[u];
                        if (unit.OwnerPlayerId != shift.UpdatedProfile.PlayerId || survivingIds.Contains(unit.Id))
                        {
                            kept.Add(unit);
                        }
                    }

                    if (kept.Count != province.Units.Count)
                    {
                        SetProvince(province.WithUnits(kept));
                    }
                }
            }

            if (_players[_currentPlayerIndex].VictoryPoints >= CrisisDirector.VictoryPointsThreshold)
            {
                IsGameOver = true;
            }

            StateChanged?.Invoke();
        }

        /// <summary>
        /// Guilds establish a Trading Post in any unbuilt province where they have a Trader present.
        /// Grants +1 Trader immediately per Rulebook Section 3.
        /// </summary>
        public CommandResult BuildTradingPost(int provinceId)
        {
            var check = ValidateAction(FactionType.Guilds);
            if (check != CommandResult.Ok) return check;

            var province = _provinces[provinceId];
            var player = CurrentPlayer;
            if (IndexOfPlayerUnitType(province.Units, player.PlayerId, UnitType.Trader) < 0)
            {
                return CommandResult.NoTrader;
            }

            if (province.HasTradingPost) return CommandResult.AlreadyBuilt;
            if (player.Gold < TradingPostCost) return CommandResult.NotEnoughGold;

            var updatedUnits = new List<UnitInstance>(province.Units)
            {
                new(_nextUnitId++, player.PlayerId, FactionType.Guilds, UnitType.Trader, 1, 1)
            };

            SetProvince(province.WithTradingPost(player.PlayerId).WithUnits(updatedUnits));
            AddGold(_currentPlayerIndex, -TradingPostCost);
            SpendActionPoint();
            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        /// <summary>
        /// Caravan Network (Rulebook Section 5.3): moves a ready Guild Trader between two Trading Posts
        /// along an adjacent path. Pays 1G transit fee to each traversed province owner and gains
        /// 2G + (extraTradingPosts / 2) per province traversed. Routes traversing >= 3 provinces score +2 VP.
        /// </summary>
        public CommandResult RunCaravan(IReadOnlyList<int> path)
        {
            var check = ValidateAction(FactionType.Guilds);
            if (check != CommandResult.Ok) return check;

            if (path == null || path.Count < 2) return CommandResult.InvalidCaravanPath;

            int playerId = CurrentPlayer.PlayerId;
            int originId = path[0];
            int destId = path[path.Count - 1];
            if (originId < 0 || originId >= _provinces.Length || destId < 0 || destId >= _provinces.Length)
            {
                return CommandResult.InvalidCaravanPath;
            }

            if (!_provinces[originId].HasTradingPost || !_provinces[destId].HasTradingPost)
            {
                return CommandResult.InvalidCaravanPath;
            }

            var origin = _provinces[originId];
            int traderIndex = -1;
            for (int i = 0; i < origin.Units.Count; i++)
            {
                var u = origin.Units[i];
                if (u.OwnerPlayerId == playerId && u.Type == UnitType.Trader && !u.HasActed)
                {
                    traderIndex = i;
                    break;
                }
            }

            if (traderIndex < 0) return CommandResult.NoTrader;

            var visited = new HashSet<int> { originId };
            int totalToll = 0;
            int traversedCount = path.Count - 1;

            for (int step = 1; step < path.Count; step++)
            {
                int prevId = path[step - 1];
                int nextId = path[step];
                if (nextId < 0 || nextId >= _provinces.Length) return CommandResult.InvalidCaravanPath;
                if (!visited.Add(nextId)) return CommandResult.InvalidCaravanPath;
                if (!_provinces[prevId].IsAdjacentTo(nextId)) return CommandResult.NotAdjacent;

                int ownerId = _provinces[nextId].OwnerId;
                if (ownerId != ProvinceState.NeutralOwner && ownerId != playerId)
                {
                    totalToll += CaravanTransitToll;
                }
            }

            if (CurrentPlayer.Gold < totalToll) return CommandResult.NotEnoughGold;

            for (int step = 1; step < path.Count; step++)
            {
                int ownerId = _provinces[path[step]].OwnerId;
                if (ownerId != ProvinceState.NeutralOwner && ownerId != playerId)
                {
                    int ownerIdx = IndexOfPlayer(ownerId);
                    if (ownerIdx >= 0)
                    {
                        AddGold(ownerIdx, CaravanTransitToll);
                    }
                }
            }

            int extraPosts = Math.Max(0, CountOwnedTradingPosts(playerId) - StartingTradingPostCount);
            int gainPerProvince = CaravanBaseGainPerProvince + (extraPosts / 2);
            int netGoldDelta = (traversedCount * gainPerProvince) - totalToll;
            AddGold(_currentPlayerIndex, netGoldDelta);

            var trader = origin.Units[traderIndex] with { HasActed = true };
            var originUnits = new List<UnitInstance>(origin.Units.Count - 1);
            for (int i = 0; i < origin.Units.Count; i++)
            {
                if (i != traderIndex) originUnits.Add(origin.Units[i]);
            }

            var dest = _provinces[destId];
            var destUnits = new List<UnitInstance>(dest.Units) { trader };

            SetProvince(origin.WithUnits(originUnits));
            SetProvince(dest.WithUnits(destUnits));

            if (traversedCount >= CaravanMinTraversedForVp)
            {
                _qualifyingCaravanRoutesThisTurn++;
            }

            SpendActionPoint();
            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        /// <summary>
        /// Computes the market value to buy out an unfortified foreign province.
        /// </summary>
        public int GetHostileBuyoutCost(int provinceId)
        {
            var province = _provinces[provinceId];
            int cost = HostileBuyoutBaseCost;
            for (int i = 0; i < province.Units.Count; i++)
            {
                var u = province.Units[i];
                if (u.OwnerPlayerId == province.OwnerId && u.Type == UnitType.RegularMilitary)
                {
                    cost += HostileBuyoutUnitPremium;
                }
            }

            if (province.HasChurch) cost += HostileBuyoutBuildingPremium;
            if (province.HasTradingPost) cost += HostileBuyoutBuildingPremium;
            return cost;
        }

        /// <summary>
        /// Target player declares an Embargo against a Guild player, vetoing Hostile Buyouts and making
        /// Guild Traders fair game to raid.
        /// </summary>
        public CommandResult DeclareEmbargo(int guildPlayerId)
        {
            if (IsGameOver) return CommandResult.GameOver;
            if (CurrentPlayer.ActionPoints <= 0) return CommandResult.NoActionPoints;
            var guild = GetPlayer(guildPlayerId);
            if (guild == null || guild.Faction != FactionType.Guilds || guildPlayerId == CurrentPlayer.PlayerId)
            {
                return CommandResult.InvalidBattleTarget;
            }

            if (!_activeEmbargoes.Add((CurrentPlayer.PlayerId, guildPlayerId)))
            {
                return CommandResult.AlreadyBuilt;
            }

            SpendActionPoint();
            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        /// <summary>
        /// Military unit of an embargoing player raids and eliminates a co-existing Guild Trader, looting 2G.
        /// </summary>
        public CommandResult RaidCaravan(int provinceId, int guildPlayerId)
        {
            if (IsGameOver) return CommandResult.GameOver;
            if (CurrentPlayer.ActionPoints <= 0) return CommandResult.NoActionPoints;
            if (!IsEmbargoedBy(CurrentPlayer.PlayerId, guildPlayerId)) return CommandResult.NotEmbargoed;
            if (GetUnitsForPlayer(provinceId, CurrentPlayer.PlayerId, UnitType.RegularMilitary).Count == 0)
            {
                return CommandResult.NoMilitaryUnits;
            }

            var province = _provinces[provinceId];
            int traderIdx = IndexOfPlayerUnitType(province.Units, guildPlayerId, UnitType.Trader);
            if (traderIdx < 0) return CommandResult.NoTrader;

            var updatedUnits = new List<UnitInstance>(province.Units.Count - 1);
            for (int i = 0; i < province.Units.Count; i++)
            {
                if (i != traderIdx) updatedUnits.Add(province.Units[i]);
            }

            SetProvince(province.WithUnits(updatedUnits));
            AddGold(_currentPlayerIndex, TraderCost);
            SpendActionPoint();
            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        /// <summary>
        /// Hostile Buyout: Guilds pay market value to purchase an unfortified province owned by another player
        /// where they have a Trader present, instantly placing a Trading Post (+1 Trader) and retreating enemy troops.
        /// </summary>
        public CommandResult HostileBuyout(int provinceId)
        {
            var check = ValidateAction(FactionType.Guilds);
            if (check != CommandResult.Ok) return check;

            var province = _provinces[provinceId];
            var player = CurrentPlayer;

            if (province.OwnerId == ProvinceState.NeutralOwner || province.OwnerId == player.PlayerId)
            {
                return CommandResult.InvalidBattleTarget;
            }

            if (IsEmbargoedBy(province.OwnerId, player.PlayerId))
            {
                return CommandResult.TargetEmbargoed;
            }

            if (province.HasFort) return CommandResult.ProvinceFortified;

            if (IndexOfPlayerUnitType(province.Units, player.PlayerId, UnitType.Trader) < 0)
            {
                return CommandResult.NoTrader;
            }

            int cost = GetHostileBuyoutCost(provinceId);
            if (player.Gold < cost) return CommandResult.NotEnoughGold;

            int previousOwnerId = province.OwnerId;
            int previousOwnerIdx = IndexOfPlayer(previousOwnerId);

            var remainingUnits = new List<UnitInstance>(province.Units.Count);
            var expelledMilitary = new List<UnitInstance>();
            for (int i = 0; i < province.Units.Count; i++)
            {
                var u = province.Units[i];
                if (u.OwnerPlayerId == previousOwnerId && u.Type == UnitType.RegularMilitary)
                {
                    expelledMilitary.Add(u);
                }
                else
                {
                    remainingUnits.Add(u);
                }
            }

            if (!province.HasTradingPost)
            {
                remainingUnits.Add(new UnitInstance(_nextUnitId++, player.PlayerId, FactionType.Guilds, UnitType.Trader,
                    1,
                    1));
            }

            AddGold(_currentPlayerIndex, -cost);
            if (previousOwnerIdx >= 0)
            {
                AddGold(previousOwnerIdx, cost);
            }

            var updatedProvince = province
                .WithOccupation(player.PlayerId, remainingUnits)
                .WithTradingPost(province.HasTradingPost ? province.TradingPostOwnerId : player.PlayerId);
            _provinces[provinceId] = updatedProvince;
            OnProvinceStateChanged?.Invoke(updatedProvince);

            if (expelledMilitary.Count > 0)
            {
                RetreatUnits(updatedProvince, previousOwnerId, expelledMilitary);
            }

            SpendActionPoint();
            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        /// <summary>
        /// War Financing (Rulebook Section 5.3): Guilds sell a Mercenary unit in <paramref name="provinceId"/>
        /// to <paramref name="buyerPlayerId"/> for <paramref name="goldPrice"/> Gold.
        /// </summary>
        public CommandResult SellMercenaryContract(int provinceId, int buyerPlayerId,
            int goldPrice = DefaultMercenaryPrice)
        {
            var check = ValidateAction(FactionType.Guilds);
            if (check != CommandResult.Ok) return check;

            if (goldPrice < 0 || buyerPlayerId == CurrentPlayer.PlayerId) return CommandResult.InvalidBattleTarget;
            int buyerIdx = IndexOfPlayer(buyerPlayerId);
            if (buyerIdx < 0) return CommandResult.InvalidBattleTarget;
            if (_players[buyerIdx].Gold < goldPrice) return CommandResult.NotEnoughGold;

            var province = _provinces[provinceId];
            if (IndexOfPlayerUnitType(province.Units, CurrentPlayer.PlayerId, UnitType.Trader) < 0
                && province.TradingPostOwnerId != CurrentPlayer.PlayerId)
            {
                return CommandResult.NoTrader;
            }

            var units = new List<UnitInstance>(province.Units)
            {
                new(_nextUnitId++, buyerPlayerId, FactionType.Guilds, UnitType.RegularMilitary, 1, 1)
            };

            AddGold(buyerIdx, -goldPrice);
            AddGold(_currentPlayerIndex, goldPrice);
            SetProvince(province.WithUnits(units));
            SpendActionPoint();
            StateChanged?.Invoke();
            return CommandResult.Ok;
        }
    }
}