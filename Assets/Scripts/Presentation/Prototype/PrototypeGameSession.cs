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
        public const int ChurchCost = 4;
        public const int ChurchTithe = 1;
        public const int RebellionCellCost = 2;
        public const int ConversionLimit = 2;
        public const int VictoryPointsPerProvince = 1;

        private readonly IGameRules _rules;
        private readonly bool _crisisEnabled;
        private readonly PlayerProfile[] _players;
        private readonly ProvinceState[] _provinces;
        private readonly HashSet<int> _movedUnitIds = new();

        private int _currentPlayerIndex;
        private int _nextUnitId;

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

        /// <summary>
        /// Calculates total Root-style strength points for a player in a province:
        /// each Military unit = 1, Fort (if owned by player) = 1, Church (if owned by player) = 1.
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
                if (unit.Faction == player.Faction && unit.Kind == UnitKind.Military)
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

        public List<CombatUnit> GetUnitsForPlayer(int provinceId, int playerId, UnitKind? kindFilter = null)
        {
            var province = _provinces[provinceId];
            var player = GetPlayer(playerId);
            var result = new List<CombatUnit>();
            if (player == null) return result;

            for (int i = 0; i < province.Units.Count; i++)
            {
                var unit = province.Units[i];
                if (unit.Faction != player.Faction) continue;
                if (kindFilter.HasValue && unit.Kind != kindFilter.Value) continue;
                result.Add(unit);
            }

            return result;
        }

        public int CountMovableUnits(int provinceId)
        {
            var units = _provinces[provinceId].Units;
            var currentFaction = CurrentPlayer.Faction;
            int movable = 0;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].Faction == currentFaction && !_movedUnitIds.Contains(units[i].Id))
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
        /// Zealots cannot recruit standing armies; they field only converted or inherited military units.
        /// </summary>
        public CommandResult BuyUnit(int provinceId)
        {
            var check = ValidateOwnProvinceAction(provinceId);
            if (check != CommandResult.Ok) return check;
            if (CurrentPlayer.Faction == FactionType.Zealots) return CommandResult.WrongFaction;

            return Recruit(provinceId, UnitCost, UnitKind.Military);
        }

        public CommandResult BuyMissionary(int provinceId)
        {
            var check = ValidateOwnProvinceAction(provinceId);
            if (check != CommandResult.Ok) return check;
            if (CurrentPlayer.Faction != FactionType.Zealots) return CommandResult.WrongFaction;

            return Recruit(provinceId, MissionaryCost, UnitKind.Missionary);
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
        /// A Missionary spends the action to place a Religious Presence token in its province.
        /// </summary>
        public CommandResult Consecrate(int provinceId)
        {
            var check = ValidateOwnProvinceAction(provinceId);
            if (check != CommandResult.Ok) return check;

            var province = _provinces[provinceId];
            if (CurrentPlayer.Faction != FactionType.Zealots) return CommandResult.WrongFaction;
            if (IndexOfKind(GetUnitsForPlayer(provinceId, CurrentPlayer.PlayerId), UnitKind.Missionary) < 0)
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
        /// Expends a Missionary for a contested roll against an adjacent enemy garrison. On a win up to
        /// <see cref="ConversionLimit"/> enemy units keep their stats, join the Zealots and move to the
        /// Missionary's province.
        /// </summary>
        public CommandResult DivineConversion(int missionaryProvinceId, int targetProvinceId)
        {
            var check = ValidateOwnProvinceAction(missionaryProvinceId);
            if (check != CommandResult.Ok) return check;

            var origin = _provinces[missionaryProvinceId];
            var target = _provinces[targetProvinceId];
            var player = CurrentPlayer;
            if (player.Faction != FactionType.Zealots) return CommandResult.WrongFaction;

            int missionaryIndex = IndexOfPlayerUnitKind(origin.Units, player.Faction, UnitKind.Missionary);
            if (missionaryIndex < 0) return CommandResult.NoMissionary;
            if (!origin.IsAdjacentTo(targetProvinceId)) return CommandResult.NotAdjacent;

            var enemyUnits = new List<CombatUnit>();
            var nonEnemyUnits = new List<CombatUnit>();
            for (int i = 0; i < target.Units.Count; i++)
            {
                if (target.Units[i].Faction != player.Faction)
                    enemyUnits.Add(target.Units[i]);
                else
                    nonEnemyUnits.Add(target.Units[i]);
            }

            if (enemyUnits.Count == 0) return CommandResult.NoTargetUnits;

            var garrison = new List<CombatUnit>(origin.Units);
            garrison.RemoveAt(missionaryIndex);

            int zealotRoll = _rules.RollContest();
            int targetRoll = _rules.RollContest();
            if (zealotRoll > targetRoll)
            {
                int converted = Math.Min(ConversionLimit, enemyUnits.Count);
                for (int i = 0; i < enemyUnits.Count; i++)
                {
                    var unit = enemyUnits[i];
                    if (i < converted)
                    {
                        var convert = unit with { Faction = FactionType.Zealots };
                        garrison.Add(convert);
                        _movedUnitIds.Add(convert.Id);
                    }
                    else
                    {
                        nonEnemyUnits.Add(unit);
                    }
                }

                SetProvince(target.WithUnits(nonEnemyUnits));
            }

            SetProvince(origin.WithUnits(garrison));
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
        /// A landed army occupying the province dismantles an enemy Church (unless the province is fortified, which
        /// keeps it a Religious Bastion) and cleanses an enemy Religious Presence token.
        /// </summary>
        public CommandResult CleanseProvince(int provinceId)
        {
            var player = CurrentPlayer;
            bool isLanded = player.Faction == FactionType.Militants || player.Faction == FactionType.Zealots;
            var check = ValidatePresence(provinceId, isLanded);
            if (check != CommandResult.Ok) return check;

            var province = _provinces[provinceId];
            bool dismantleChurch = province.HasChurch && province.ChurchOwnerId != player.PlayerId && !province.HasFort;
            bool cleanseToken = province.PresenceToken != null && province.PresenceToken.OwnerId != player.PlayerId;
            if (!dismantleChurch && !cleanseToken) return CommandResult.NothingToPurge;

            if (dismantleChurch) province = province.WithChurch(ProvinceState.NoChurch);
            if (cleanseToken) province = province.WithPresence(null);

            SetProvince(province);
            SpendActionPoint();
            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        public List<int> GetMovableUnitIds(int provinceId)
        {
            var units = _provinces[provinceId].Units;
            var currentFaction = CurrentPlayer.Faction;
            var ids = new List<int>(units.Count);
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].Faction == currentFaction && !_movedUnitIds.Contains(units[i].Id))
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

            var selection = SplitSelectedUnits(from, CurrentPlayer.Faction, command.SelectedUnitIds, out var movers,
                out var stayers);
            if (selection != CommandResult.Ok) return selection;

            int playerId = CurrentPlayer.PlayerId;
            bool hasHostilePresence = HasEnemyMilitary(to.Units, CurrentPlayer.Faction);
            if (to.OwnerId != playerId && hasHostilePresence && IndexOfKind(movers, UnitKind.Missionary) >= 0)
            {
                return CommandResult.MissionariesCannotConquer;
            }

            SetProvince(from.WithUnits(stayers));
            SetProvince(to.WithUnits(Concat(to.Units, movers)));
            MarkMoved(movers);
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
            var attackers = GetUnitsForPlayer(provinceId, attacker.PlayerId, UnitKind.Military);
            if (attackers.Count == 0) return CommandResult.NoMilitaryUnits;

            var defenders = GetUnitsForPlayer(provinceId, defenderPlayerId, UnitKind.Military);
            if (defenders.Count == 0) return CommandResult.NoTargetUnits;

            bool defenderHasFort = province.HasFort && province.OwnerId == defenderPlayerId;
            var result = _rules.ResolveSkirmish(attackers, defenders, defender.Faction, defenderHasFort);

            // Preserve all unaffected units (bystanders, non-combatant missionaries) in the province.
            var bystanders = new List<CombatUnit>(province.Units.Count);
            for (int i = 0; i < province.Units.Count; i++)
            {
                var unit = province.Units[i];
                bool isCombatantAttacker = unit.Faction == attacker.Faction && unit.Kind == UnitKind.Military;
                bool isCombatantDefender = unit.Faction == defender.Faction && unit.Kind == UnitKind.Military;
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
            int victoryPoints = player.VictoryPoints + controlled * VictoryPointsPerProvince;
            var beforeRules = CopyProfile(player, player.Faction, gold, victoryPoints,
                CollectUnits(player.PlayerId));

            int provincesForRules = _crisisEnabled
                ? controlled
                : Math.Max(controlled, CrisisDirector.CrisisProvinceThreshold);
            var updated = _rules.EvaluatePlayerTurnState(beforeRules, provincesForRules);
            _players[_currentPlayerIndex] = updated;

            if (updated.Faction != player.Faction)
            {
                ConvertPlayerUnits(player.Faction, updated.Faction);
            }

            IsGameOver = updated.VictoryPoints >= CrisisDirector.VictoryPointsThreshold;
            if (!IsGameOver)
            {
                _movedUnitIds.Clear();
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

        /// <summary>
        /// Dynamically evaluates Province Hegemony via strict majority of total strength points:
        /// each Military unit = 1, Fort (owned by current controller) = 1, Church (owned by builder) = 1.
        /// </summary>
        private ProvinceState EvaluateHegemony(ProvinceState province)
        {
            int bestPlayerId = ProvinceState.NeutralOwner;
            int bestStrength = 0;
            bool isTie = false;

            for (int p = 0; p < _players.Length; p++)
            {
                int playerId = _players[p].PlayerId;
                var faction = _players[p].Faction;

                int strength = 0;
                for (int u = 0; u < province.Units.Count; u++)
                {
                    var unit = province.Units[u];
                    if (unit.Faction == faction && unit.Kind == UnitKind.Military)
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

            int newOwnerId = (bestStrength == 0 || isTie) ? ProvinceState.NeutralOwner : bestPlayerId;
            return newOwnerId == province.OwnerId
                ? province
                : province.WithOccupation(newOwnerId, province.Units);
        }

        /// <summary>
        /// Retreating units fall back to the first adjacent province held by that player (or without hostile military).
        /// With nowhere to go they are lost.
        /// </summary>
        private void RetreatUnits(ProvinceState originProvince, int playerId, IReadOnlyList<CombatUnit> survivors)
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

        /// <summary>
        /// Selected units belonging to <paramref name="movingFaction"/> move; every other unit stays in the origin.
        /// Fails without side effects when a selected unit has already moved, is not in the province, belongs to
        /// another player, or is listed twice.
        /// </summary>
        private CommandResult SplitSelectedUnits(
            ProvinceState province,
            FactionType movingFaction,
            IReadOnlyList<int> selectedUnitIds,
            out List<CombatUnit> movers,
            out List<CombatUnit> stayers)
        {
            int count = province.Units.Count;
            movers = new List<CombatUnit>(selectedUnitIds.Count);
            stayers = new List<CombatUnit>(count);
            for (int i = 0; i < count; i++)
            {
                var unit = province.Units[i];
                if (unit.Faction != movingFaction || !Contains(selectedUnitIds, unit.Id))
                {
                    stayers.Add(unit);
                    continue;
                }

                if (_movedUnitIds.Contains(unit.Id)) return CommandResult.UnitsAlreadyMoved;

                movers.Add(unit);
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

        private static bool HasEnemyMilitary(IReadOnlyList<CombatUnit> units, FactionType friendlyFaction)
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].Faction != friendlyFaction && units[i].Kind == UnitKind.Military)
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

        /// <summary>
        /// The player's faction must be allowed the action and have at least one military unit present in the province.
        /// </summary>
        private CommandResult ValidatePresence(int provinceId, bool factionAllowed)
        {
            if (IsGameOver) return CommandResult.GameOver;
            if (CurrentPlayer.ActionPoints <= 0) return CommandResult.NoActionPoints;
            if (!factionAllowed) return CommandResult.WrongFaction;
            if (GetUnitsForPlayer(provinceId, CurrentPlayer.PlayerId, UnitKind.Military).Count == 0)
            {
                return CommandResult.NoMilitaryUnits;
            }

            return CommandResult.Ok;
        }

        private CommandResult Recruit(int provinceId, int cost, UnitKind kind)
        {
            var province = _provinces[provinceId];
            var player = CurrentPlayer;
            if (player.Gold < cost) return CommandResult.NotEnoughGold;

            var units = new List<CombatUnit>(province.Units)
            {
                new(_nextUnitId++, player.Faction, 1, 1, kind)
            };

            SetProvince(province.WithUnits(units));
            AddGold(_currentPlayerIndex, -cost);
            SpendActionPoint();
            StateChanged?.Invoke();
            return CommandResult.Ok;
        }

        /// <summary>
        /// Phase 1 upkeep: each non-Zealot unit standing in a Zealot Church province pays the Church owner one Gold.
        /// Units the treasury cannot cover are disbanded.
        /// </summary>
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

                var kept = new List<CombatUnit>(province.Units.Count);
                for (int u = 0; u < province.Units.Count; u++)
                {
                    var unit = province.Units[u];
                    if (unit.Faction != payer.Faction)
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

        private static int IndexOfKind(IReadOnlyList<CombatUnit> units, UnitKind kind)
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].Kind == kind)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int IndexOfPlayerUnitKind(IReadOnlyList<CombatUnit> units, FactionType faction, UnitKind kind)
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].Faction == faction && units[i].Kind == kind)
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

        private void MarkMoved(IReadOnlyList<CombatUnit> units)
        {
            for (int i = 0; i < units.Count; i++)
            {
                _movedUnitIds.Add(units[i].Id);
            }
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

        private List<CombatUnit> CollectUnits(int playerId)
        {
            var player = GetPlayer(playerId);
            var units = new List<CombatUnit>();
            if (player == null) return units;

            for (int i = 0; i < _provinces.Length; i++)
            {
                var provinceUnits = _provinces[i].Units;
                for (int u = 0; u < provinceUnits.Count; u++)
                {
                    if (provinceUnits[u].Faction == player.Faction)
                    {
                        units.Add(provinceUnits[u]);
                    }
                }
            }

            return units;
        }

        private void ConvertPlayerUnits(FactionType oldFaction, FactionType newFaction)
        {
            for (int i = 0; i < _provinces.Length; i++)
            {
                var province = _provinces[i];
                bool changed = false;
                var converted = new List<CombatUnit>(province.Units.Count);
                for (int u = 0; u < province.Units.Count; u++)
                {
                    var unit = province.Units[u];
                    if (unit.Faction == oldFaction)
                    {
                        converted.Add(unit with { Faction = newFaction });
                        changed = true;
                    }
                    else
                    {
                        converted.Add(unit);
                    }
                }

                if (changed)
                {
                    SetProvince(province.WithUnits(converted));
                }
            }
        }

        private void SetProvince(ProvinceState province)
        {
            var evaluated = EvaluateHegemony(province);
            _provinces[evaluated.Id] = evaluated;
            OnProvinceStateChanged?.Invoke(evaluated);
        }

        private static List<CombatUnit> Concat(IReadOnlyList<CombatUnit> first, IReadOnlyList<CombatUnit> second)
        {
            var units = new List<CombatUnit>(first.Count + second.Count);
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
            IReadOnlyList<CombatUnit> units) =>
            new(profile.PlayerId, faction, gold, victoryPoints, units, profile.ActionPoints);
    }
}