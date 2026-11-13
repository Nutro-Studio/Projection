using Core.Combat;
using Core.Crisis;
using Presentation.Prototype;

namespace Presentation.Hud
{
    public static class HudText
    {
        public static string Gold(int gold) => $"Gold: {gold}G";

        public static string VictoryPoints(int victoryPoints) =>
            $"VP: {victoryPoints}/{CrisisDirector.VictoryPointsThreshold}";

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

        public static string Owner(PlayerProfile owner) => owner == null ? "Neutral" : Player(owner);

        public static string Units(int total, int movable) => $"Units: {total} (can move: {movable})";

        public static string Fort(bool hasFort) => hasFort ? "Fort: yes" : "Fort: no";

        public static string BuyButton() => $"Buy unit ({PrototypeGameSession.UnitCost}G)";

        public static string Command(CommandResult result) => result switch
        {
            CommandResult.Ok => string.Empty,
            CommandResult.GameOver => "The game is over.",
            CommandResult.NotYourProvince => "This province is not yours.",
            CommandResult.NoUnits => "There are no units here.",
            CommandResult.NotEnoughGold => $"Not enough gold: a unit costs {PrototypeGameSession.UnitCost}G.",
            CommandResult.NotAdjacent => "You can only move to an adjacent province.",
            CommandResult.UnitsAlreadyMoved => "These units have already moved this turn.",
            CommandResult.NoUnitsSelected => "Select at least one unit to move.",
            CommandResult.InvalidUnitSelection => "The selected units are not all in this province.",
            CommandResult.NoActionPoints => "No action points left. End the turn.",
            CommandResult.WrongFaction => "Your faction cannot do that.",
            CommandResult.NoMissionary => "This needs a Missionary in the province.",
            CommandResult.AlreadyBuilt => "This province already has it.",
            CommandResult.NoMilitaryUnits => "This needs a military unit in the province.",
            CommandResult.MissionariesCannotConquer => "Missionaries cannot enter land you do not hold.",
            CommandResult.NoTargetUnits => "There are no enemy units to target.",
            CommandResult.NoRebellionCell => "You have no undercover cell here.",
            CommandResult.NothingToPurge => "There is nothing here to purge.",
            _ => result.ToString()
        };

        public static string Hint(bool hasSelection, bool isTargetingMove)
        {
            if (isTargetingMove) return "Click a highlighted province to move. Esc cancels.";

            return hasSelection
                ? "Buy a unit or move the army. Space ends the turn."
                : "Click a province. Space ends the turn.";
        }

        public static string DiceTotal(int sum) => $"= {sum}";

        public static string BattleTitle(BattleReport report) => $"Battle for {report.ProvinceName}";

        public static string BattleSummary(BattleReport report)
        {
            var result = report.Result;
            string outcome = report.ProvinceCaptured
                ? $"Attacker captures {report.ProvinceName}."
                : $"Defender holds {report.ProvinceName}.";

            return $"Attacker takes the higher roll ({result.AttackerAssignedRoll}): {result.AttackerEffectiveHits} hit(s) after the cap, " +
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
