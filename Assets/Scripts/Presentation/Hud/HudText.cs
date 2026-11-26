using System;
using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;
using Core.Board;
using Core.Simulation;

namespace Presentation.Hud
{
    public static class HudText
    {
        public static string ActionPoints(int current, int max) => $"AP: {current}/{max}";

        public static string Gold(int gold) => $"Gold: {gold}G";

        public static string VictoryPoints(int victoryPoints, GameBalanceConfig config = null) =>
            $"VP: {victoryPoints}/{(config ?? GameBalanceConfig.Default).VictoryPointsThreshold}";

        public static string Player(PlayerProfile player) => $"Player {player.PlayerId}: {Faction(player.Faction)}";

        public static string Turn(int turnNumber) => $"Turn {turnNumber}";

        public static string Faction(FactionType faction) => faction switch
        {
            FactionType.Militants => "Militants",
            FactionType.Zealots => "Zealots",
            FactionType.Guilds => "Guilds",
            FactionType.Guerrillas => "Guerrillas",
            _ => faction.ToString()
        };

        public static string UnitTypeName(UnitType type) => type switch
        {
            UnitType.RegularMilitary => "Military",
            UnitType.Missionary => "Missionary",
            UnitType.Trader => "Trader",
            _ => type.ToString()
        };

        public static string UnitFilter(UnitType? filter) =>
            filter.HasValue ? $"Type: {UnitTypeName(filter.Value)}" : "Type: All";

        public static string Owner(PlayerProfile owner) => owner == null ? "Neutral" : Player(owner);

        public static string Units(int total, int movable) => $"Units: {total} (can move: {movable})";

        public static string GarrisonBreakdown(ProvinceState province, Func<int, PlayerProfile> getPlayer)
        {
            if (province.Units.Count == 0) return string.Empty;

            var playerIds = new List<int>();
            for (int i = 0; i < province.Units.Count; i++)
            {
                int ownerId = province.Units[i].OwnerPlayerId;
                if (!playerIds.Contains(ownerId))
                {
                    playerIds.Add(ownerId);
                }
            }

            var lines = new List<string>(playerIds.Count);
            for (int p = 0; p < playerIds.Count; p++)
            {
                int ownerId = playerIds[p];
                int military = 0, militaryMovable = 0;
                int missionary = 0, missionaryMovable = 0;
                int trader = 0, traderMovable = 0;

                for (int u = 0; u < province.Units.Count; u++)
                {
                    var unit = province.Units[u];
                    if (unit.OwnerPlayerId != ownerId) continue;

                    switch (unit.Type)
                    {
                        case UnitType.RegularMilitary:
                            military++;
                            if (!unit.HasActed) militaryMovable++;
                            break;
                        case UnitType.Missionary:
                            missionary++;
                            if (!unit.HasActed) missionaryMovable++;
                            break;
                        case UnitType.Trader:
                            trader++;
                            if (!unit.HasActed) traderMovable++;
                            break;
                    }
                }

                var parts = new List<string>(3);
                if (military > 0) parts.Add($"{military} Military ({militaryMovable} movable)");
                if (missionary > 0) parts.Add($"{missionary} Missionary ({missionaryMovable} movable)");
                if (trader > 0) parts.Add($"{trader} Trader ({traderMovable} movable)");

                var owner = getPlayer(ownerId);
                string ownerLabel = owner != null ? $"P{owner.PlayerId} {Faction(owner.Faction)}" : $"P{ownerId}";
                lines.Add($"{ownerLabel}: {string.Join(", ", parts)}");
            }

            return string.Join("\n", lines);
        }

        public static string ProvinceMarkers(ProvinceState province, Func<int, PlayerProfile> getPlayer)
        {
            var markers = new List<string>(3);

            if (province.HasChurch)
            {
                var owner = getPlayer(province.ChurchOwnerId);
                string who = owner != null ? $"P{owner.PlayerId}" : $"P{province.ChurchOwnerId}";
                markers.Add($"Church ({who})");
            }

            if (province.PresenceToken != null)
            {
                var owner = getPlayer(province.PresenceToken.OwnerId);
                string who = owner != null ? $"P{owner.PlayerId}" : $"P{province.PresenceToken.OwnerId}";
                markers.Add($"Religious Presence ({who})");
            }

            if (province.RebellionCell != null)
            {
                var owner = getPlayer(province.RebellionCell.OwnerId);
                string who = owner != null ? $"P{owner.PlayerId}" : $"P{province.RebellionCell.OwnerId}";
                string state = province.RebellionCell.IsOpenRebellion ? "OPEN REBELLION" : "Undercover Cell";
                markers.Add($"{state} ({who})");
            }

            if (province.HasTradingPost)
            {
                var owner = getPlayer(province.TradingPostOwnerId);
                string who = owner != null ? $"P{owner.PlayerId}" : $"P{province.TradingPostOwnerId}";
                markers.Add($"Trading Post ({who})");
            }

            return markers.Count == 0 ? string.Empty : $"Markers: {string.Join(" | ", markers)}";
        }

        public static string Fort(bool hasFort) => hasFort ? "Fort: yes" : "Fort: no";

        public static string MoveButton(int unitsToMove) =>
            unitsToMove > 0 ? $"Move {unitsToMove} unit(s)" : "Move army";

        public static string UnitsToMove(int unitsToMove, int movable) => $"{unitsToMove}/{movable}";

        public static string BuyButton(GameBalanceConfig config = null) =>
            $"Buy Military ({(config ?? GameBalanceConfig.Default).UnitCost}G)";

        public static string BuyMissionaryButton(GameBalanceConfig config = null) =>
            $"Buy Missionary ({(config ?? GameBalanceConfig.Default).MissionaryCost}G)";

        public static string BuyTraderButton(GameBalanceConfig config = null) =>
            $"Buy Trader ({(config ?? GameBalanceConfig.Default).TraderCost}G)";

        public static string BuildFortButton(GameBalanceConfig config = null) =>
            $"Build Fort ({(config ?? GameBalanceConfig.Default).FortCost}G)";

        public static string BuildChurchButton(GameBalanceConfig config = null) =>
            $"Build Church ({(config ?? GameBalanceConfig.Default).ChurchCost}G)";

        public static string BuildTradingPostButton(GameBalanceConfig config = null) =>
            $"Trading Post ({(config ?? GameBalanceConfig.Default).TradingPostCost}G)";

        public static string SellMercenaryButton(PlayerProfile buyer, GameBalanceConfig config = null) =>
            $"Sell Mercenary to P{buyer.PlayerId} ({(config ?? GameBalanceConfig.Default).DefaultMercenaryPrice}G)";

        public static string ConsecrateButton() => "Consecrate";

        public static string DivineConversionButton() => "Divine Conversion";

        public static string PlaceCellButton(int cost) => $"Place Cell ({cost}G)";

        public static string InciteRebellionButton() => "Incite Rebellion";

        public static string MartialLawButton() => "Martial Law";

        public static string InquisitionButton() => "Inquisition";

        public static string CleanseButton() => "Cleanse Province";
        public static string CaravanNetworkButton() => "Caravan Network";
        public static string ConfirmCaravanButton(int nodes) => $"Run Caravan ({nodes} nodes)";
        public static string HostileBuyoutButton(int cost) => $"Hostile Buyout ({cost}G)";
        public static string EmbargoButton(PlayerProfile guild) => $"Embargo P{guild.PlayerId}";
        public static string RaidCaravanButton(PlayerProfile guild) => $"Raid P{guild.PlayerId} Caravan";

        public static string AttackButton(PlayerProfile enemy) =>
            $"Attack P{enemy.PlayerId} ({Faction(enemy.Faction)})";

        public static string ShiftButton(FactionType target, bool isAscension) =>
            isAscension ? $"Ascend -> {Faction(target)}" : $"Shift -> {Faction(target)}";

        public static string CapitulateButton() => "Capitulate -> Guerrillas";

        public static string ProvinceTitle(ProvinceState province) =>
            string.IsNullOrWhiteSpace(province.Name) ? $"Province #{province.Id}" : province.Name;

        public static string Command(ActionResult result) => result switch
        {
            ActionResult.Ok => string.Empty,
            ActionResult.GameOver => "The game is over.",
            ActionResult.NotYourProvince => "This province is not yours.",
            ActionResult.NoUnits => "There are no units here.",
            ActionResult.NotEnoughGold => "Not enough gold.",
            ActionResult.NotAdjacent => "You can only target an adjacent province.",
            ActionResult.UnitsAlreadyMoved => "These units have already moved this turn.",
            ActionResult.NoUnitsSelected => "Select at least one unit to move.",
            ActionResult.InvalidUnitSelection => "The selected units are not all in this province.",
            ActionResult.NoActionPoints => "No action points left. End the turn.",
            ActionResult.WrongFaction => "Your faction cannot do that.",
            ActionResult.NoMissionary => "This needs a Missionary in the province.",
            ActionResult.AlreadyBuilt => "This province already has it.",
            ActionResult.NoMilitaryUnits => "This needs a military unit in the province.",
            ActionResult.NoTargetUnits => "There are no enemy units to target.",
            ActionResult.NoRebellionCell => "You have no undercover cell here.",
            ActionResult.NothingToPurge => "There is nothing here to purge.",
            ActionResult.InvalidBattleTarget => "Choose a valid enemy force in this province.",
            ActionResult.InvalidRulesetShift => "Ruleset shift requirements are not met.",
            ActionResult.NoTrader => "This needs a ready Trader in the province.",
            ActionResult.ProvinceFortified => "Fortified provinces cannot be bought out.",
            ActionResult.InvalidCaravanPath => "Select a contiguous path of at least 2 provinces.",
            ActionResult.TraderCapReached => "Build more Trading Posts to support additional Traders.",
            ActionResult.TargetEmbargoed => "Target faction has declared an Embargo against you.",
            ActionResult.NotEmbargoed => "Declare an Embargo before raiding Guild caravans.",
            ActionResult.CannotBuyoutWithExistingTradingPost =>
                "Cannot buy out a province that already has a Trading Post.",
            _ => result.ToString()
        };

        public static string Hint(bool hasSelection, bool isTargetingMove)
        {
            if (isTargetingMove) return "Click a highlighted province to move. Esc cancels.";

            return hasSelection
                ? "Choose an action or move units. Space ends the turn."
                : "Click a province. Space ends the turn.";
        }

        public static string DiceTotal(int sum) => $"= {sum}";

        public static string BattleTitle(CombatReport report) => $"Battle for {report.ProvinceName}";

        public static string BattleSummary(CombatReport report)
        {
            var result = report.Result;
            string outcome = report.ProvinceCaptured
                ? $"Attacker captures {report.ProvinceName}."
                : $"Defender holds {report.ProvinceName}.";

            return
                $"Attacker takes the higher roll ({result.AttackerAssignedRoll}): {result.AttackerEffectiveHits} hit(s) after the cap, " +
                $"{report.DefenderCasualties} defender(s) lost.\n" +
                $"Defender takes the lower roll ({result.DefenderAssignedRoll}): {result.DefenderEffectiveHits} hit(s) after the cap, " +
                $"{report.AttackerCasualties} attacker(s) lost.\n" +
                outcome;
        }

        public static string RulesetShift(CrisisShiftResult shift)
        {
            string from = Faction(shift.PreviousFaction);
            string to = Faction(shift.NewFaction);
            if (shift.IsCrisis) return $"CRISIS! {from} collapse and become {to}. Treasury lost, -20% VP.";
            if (shift.IsAscension) return $"ASCENSION! {from} rise as {to}. +10% VP.";

            return $"Ruleset shift: {from} become {to}. -20% VP.";
        }

        public static string Victory(PlayerProfile winner) =>
            $"{Player(winner)} wins with {winner.VictoryPoints} VP!";
    }
}