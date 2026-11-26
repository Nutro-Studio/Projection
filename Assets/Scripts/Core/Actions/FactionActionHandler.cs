using System;
using System.Collections.Generic;
using Core.Board;
using Core.Combat;
using Core.Simulation;

namespace Core.Actions
{
    internal sealed class FactionActionHandler
    {
        private readonly GameSessionState _state;
        private readonly IDiceRoller _contestDice;

        public FactionActionHandler(GameSessionState state, IDiceRoller contestDice)
        {
            _state = state;
            _contestDice = contestDice;
        }

        public ActionResult BuyUnit(int provinceId)
        {
            var check = ValidateOwnProvinceAction(provinceId);
            if (check != ActionResult.Ok) return check;
            if (_state.CurrentPlayer.Faction == FactionType.Zealots ||
                _state.CurrentPlayer.Faction == FactionType.Guilds)
            {
                return ActionResult.WrongFaction;
            }

            return Recruit(provinceId, GameSession.UnitCost, UnitType.RegularMilitary);
        }

        public ActionResult BuyMissionary(int provinceId)
        {
            var check = ValidateOwnProvinceAction(provinceId);
            if (check != ActionResult.Ok) return check;
            if (_state.CurrentPlayer.Faction != FactionType.Zealots) return ActionResult.WrongFaction;

            return Recruit(provinceId, GameSession.MissionaryCost, UnitType.Missionary);
        }

        public ActionResult BuildFort(int provinceId)
        {
            var check = ValidateOwnProvinceAction(provinceId);
            if (check != ActionResult.Ok) return check;

            var province = _state.Provinces[provinceId];
            var player = _state.CurrentPlayer;
            if (player.Faction != FactionType.Militants) return ActionResult.WrongFaction;
            if (province.HasFort) return ActionResult.AlreadyBuilt;
            if (player.Gold < GameSession.FortCost) return ActionResult.NotEnoughGold;

            _state.SetProvince(province.WithFort(true));
            _state.AddGold(_state.CurrentPlayerIndex, -GameSession.FortCost);
            _state.AddVictoryPoints(_state.CurrentPlayerIndex, GameSession.BuildFortVp);
            _state.SpendActionPoint();
            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        public ActionResult BuildChurch(int provinceId)
        {
            var check = ValidateOwnProvinceAction(provinceId);
            if (check != ActionResult.Ok) return check;

            var province = _state.Provinces[provinceId];
            var player = _state.CurrentPlayer;
            if (player.Faction != FactionType.Zealots) return ActionResult.WrongFaction;
            if (province.HasChurch) return ActionResult.AlreadyBuilt;
            if (player.Gold < GameSession.ChurchCost) return ActionResult.NotEnoughGold;

            _state.SetProvince(province.WithChurch(player.PlayerId));
            _state.AddGold(_state.CurrentPlayerIndex, -GameSession.ChurchCost);
            _state.AddVictoryPoints(_state.CurrentPlayerIndex, GameSession.BuildChurchVp);
            _state.SpendActionPoint();
            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        public ActionResult Consecrate(int provinceId)
        {
            var check = ValidateAction(FactionType.Zealots);
            if (check != ActionResult.Ok) return check;

            var province = _state.Provinces[provinceId];
            if (IndexOfPlayerUnitType(province.Units, _state.CurrentPlayer.PlayerId, UnitType.Missionary) < 0)
            {
                return ActionResult.NoMissionary;
            }

            if (province.PresenceToken != null) return ActionResult.AlreadyBuilt;

            _state.SetProvince(province.WithPresence(new ReligiousPresenceToken(_state.CurrentPlayer.PlayerId)));
            _state.SpendActionPoint();
            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        public ActionResult DivineConversion(int provinceId, int missionaryCount = 1)
        {
            var check = ValidateAction(FactionType.Zealots);
            if (check != ActionResult.Ok) return check;

            var province = _state.Provinces[provinceId];
            var player = _state.CurrentPlayer;

            int missionariesPresent = 0;
            int enemyMilitaryCount = 0;
            for (int i = 0; i < province.Units.Count; i++)
            {
                var unit = province.Units[i];
                if (unit.OwnerPlayerId == player.PlayerId && unit.Type == UnitType.Missionary)
                {
                    missionariesPresent++;
                }
                else if (unit.OwnerPlayerId != player.PlayerId && unit.Type == UnitType.RegularMilitary)
                {
                    enemyMilitaryCount++;
                }
            }

            if (missionariesPresent == 0) return ActionResult.NoMissionary;
            if (missionaryCount <= 0 || missionaryCount > missionariesPresent) return ActionResult.InvalidUnitSelection;
            if (enemyMilitaryCount == 0) return ActionResult.NoTargetUnits;

            var (z1, z2, z3) = _contestDice.Roll3D4();
            var (e1, e2, e3) = _contestDice.Roll3D4();
            int rollGain = Math.Max(0, (z1 + z2 + z3) - (e1 + e2 + e3));
            int maxGain = missionaryCount + 1;
            int toConvert = Math.Min(Math.Min(rollGain, maxGain), enemyMilitaryCount);

            var updatedUnits = new List<UnitInstance>(province.Units.Count - missionaryCount);
            int expendedSoFar = 0;
            int convertedSoFar = 0;

            for (int i = 0; i < province.Units.Count; i++)
            {
                var unit = province.Units[i];
                if (expendedSoFar < missionaryCount
                    && unit.OwnerPlayerId == player.PlayerId
                    && unit.Type == UnitType.Missionary)
                {
                    expendedSoFar++;
                    continue;
                }

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

            _state.SetProvince(province.WithUnits(updatedUnits));
            _state.SpendActionPoint();
            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        public ActionResult PlaceRebellionCell(int provinceId)
        {
            var check = ValidateAction(FactionType.Guerrillas);
            if (check != ActionResult.Ok) return check;

            var province = _state.Provinces[provinceId];
            int cost = province.HasChurch ? GameSession.RebellionCellCost * 2 : GameSession.RebellionCellCost;
            if (province.RebellionCell != null) return ActionResult.AlreadyBuilt;
            if (_state.CurrentPlayer.Gold < cost) return ActionResult.NotEnoughGold;

            _state.SetProvince(province.WithRebellionCell(new RebellionCell(_state.CurrentPlayer.PlayerId, false)));
            _state.AddGold(_state.CurrentPlayerIndex, -cost);
            _state.SpendActionPoint();
            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        public ActionResult InciteRebellion(int provinceId)
        {
            var check = ValidateAction(FactionType.Guerrillas);
            if (check != ActionResult.Ok) return check;

            var province = _state.Provinces[provinceId];
            var cell = province.RebellionCell;
            if (cell == null || cell.OwnerId != _state.CurrentPlayer.PlayerId) return ActionResult.NoRebellionCell;
            if (cell.IsOpenRebellion) return ActionResult.AlreadyBuilt;

            _state.SetProvince(province.WithRebellionCell(new RebellionCell(cell.OwnerId, true)));
            _state.SpendActionPoint();
            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        public ActionResult MartialLaw(int provinceId)
        {
            var check = ValidatePresence(provinceId, _state.CurrentPlayer.Faction == FactionType.Militants);
            if (check != ActionResult.Ok) return check;

            var province = _state.Provinces[provinceId];
            if (province.RebellionCell == null || !province.RebellionCell.IsOpenRebellion)
            {
                return ActionResult.NothingToPurge;
            }

            _state.SetProvince(province.WithRebellionCell(null));
            _state.AddVictoryPoints(_state.CurrentPlayerIndex, GameSession.CaptureOrCrushRebellionVp);
            _state.SpendActionPoint();
            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        public ActionResult Inquisition(int provinceId)
        {
            var check = ValidatePresence(provinceId, _state.CurrentPlayer.Faction == FactionType.Militants);
            if (check != ActionResult.Ok) return check;

            var province = _state.Provinces[provinceId];
            int playerId = _state.CurrentPlayer.PlayerId;

            bool hasPresence = province.PresenceToken != null;
            bool hasEnemyMissionary = false;
            for (int i = 0; i < province.Units.Count; i++)
            {
                var u = province.Units[i];
                if (u.OwnerPlayerId != playerId && u.Type == UnitType.Missionary)
                {
                    hasEnemyMissionary = true;
                    break;
                }
            }

            if (!hasPresence && !hasEnemyMissionary) return ActionResult.NothingToPurge;

            var updatedProvince = hasPresence ? province.WithPresence(null) : province;
            if (hasEnemyMissionary)
            {
                var remaining = new List<UnitInstance>(updatedProvince.Units.Count);
                for (int i = 0; i < updatedProvince.Units.Count; i++)
                {
                    var u = updatedProvince.Units[i];
                    if (u.OwnerPlayerId != playerId && u.Type == UnitType.Missionary) continue;
                    remaining.Add(u);
                }

                updatedProvince = updatedProvince.WithUnits(remaining);
            }

            _state.SetProvince(updatedProvince);
            _state.SpendActionPoint();
            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        public ActionResult CleanseProvince(int provinceId)
        {
            var player = _state.CurrentPlayer;
            bool isLanded = player.Faction == FactionType.Militants || player.Faction == FactionType.Zealots;
            var check = ValidatePresence(provinceId, isLanded);
            if (check != ActionResult.Ok) return check;

            var province = _state.Provinces[provinceId];
            bool dismantleChurch = province.HasChurch && province.ChurchOwnerId != player.PlayerId && !province.HasFort;
            bool dismantleTradingPost = province.HasTradingPost && province.TradingPostOwnerId != player.PlayerId &&
                                        !province.HasFort;
            bool cleanseToken = province.PresenceToken != null && province.PresenceToken.OwnerId != player.PlayerId;
            if (!dismantleChurch && !dismantleTradingPost && !cleanseToken) return ActionResult.NothingToPurge;

            if (dismantleChurch) province = province.WithChurch(ProvinceState.NoChurch);
            if (dismantleTradingPost) province = province.WithTradingPost(ProvinceState.NoTradingPost);
            if (cleanseToken) province = province.WithPresence(null);

            _state.SetProvince(province);
            _state.SpendActionPoint();
            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        private ActionResult ValidateOwnProvinceAction(int provinceId)
        {
            if (_state.IsGameOver) return ActionResult.GameOver;
            if (_state.CurrentPlayer.ActionPoints <= 0) return ActionResult.NoActionPoints;
            if (_state.Provinces[provinceId].OwnerId != _state.CurrentPlayer.PlayerId)
                return ActionResult.NotYourProvince;
            return ActionResult.Ok;
        }

        private ActionResult ValidateAction(FactionType requiredFaction)
        {
            if (_state.IsGameOver) return ActionResult.GameOver;
            if (_state.CurrentPlayer.ActionPoints <= 0) return ActionResult.NoActionPoints;
            if (_state.CurrentPlayer.Faction != requiredFaction) return ActionResult.WrongFaction;
            return ActionResult.Ok;
        }

        private ActionResult ValidatePresence(int provinceId, bool factionAllowed)
        {
            if (_state.IsGameOver) return ActionResult.GameOver;
            if (_state.CurrentPlayer.ActionPoints <= 0) return ActionResult.NoActionPoints;
            if (!factionAllowed) return ActionResult.WrongFaction;
            if (IndexOfPlayerUnitType(_state.Provinces[provinceId].Units, _state.CurrentPlayer.PlayerId,
                    UnitType.RegularMilitary) < 0)
            {
                return ActionResult.NoMilitaryUnits;
            }

            return ActionResult.Ok;
        }

        private ActionResult Recruit(int provinceId, int cost, UnitType type)
        {
            var province = _state.Provinces[provinceId];
            var player = _state.CurrentPlayer;
            if (player.Gold < cost) return ActionResult.NotEnoughGold;

            var units = new List<UnitInstance>(province.Units)
            {
                new(_state.NextUnitId++, player.PlayerId, player.Faction, type, 1, 1)
            };

            _state.SetProvince(province.WithUnits(units));
            _state.AddGold(_state.CurrentPlayerIndex, -cost);
            _state.SpendActionPoint();
            _state.NotifyStateChanged();
            return ActionResult.Ok;
        }

        private static int IndexOfPlayerUnitType(IReadOnlyList<UnitInstance> units, int playerId, UnitType type)
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].OwnerPlayerId == playerId && units[i].Type == type) return i;
            }

            return -1;
        }
    }
}