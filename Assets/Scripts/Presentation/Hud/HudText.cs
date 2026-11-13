using System.Text;
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

        public static string ActionPoints(int actionPoints) => $"AP: {actionPoints}/{PlayerProfile.BaseActionPoints}";

        public static string Faction(FactionType faction) => faction switch
        {
            FactionType.Militants => "Militants",
            FactionType.Zealots => "Zealots",
            FactionType.Guilds => "Guilds",
            FactionType.Guerrillas => "Guerrillas",
            _ => faction.ToString()
        };

        public static string Owner(PlayerProfile owner) => owner == null ? "Neutral" : Player(owner);

        public static string UnitTypeName(UnitType type) => type switch
        {
            UnitType.RegularMilitary => "Soldiers",
            UnitType.Missionary => "Missionaries",
            UnitType.Trader => "Traders",
            _ => type.ToString()
        };

        /// <summary>
        /// One line per player present in the province, e.g. "Player 1 Militants: 2 Soldiers, 1 Missionaries".
        /// </summary>
        public static string Garrison(ProvinceState province, PrototypeGameSession session)
        {
            if (province.Units.Count == 0) return "No units";

            var text = new StringBuilder();
            for (int i = 0; i < province.Units.Count; i++)
            {
                int playerId = province.Units[i].OwnerPlayerId;
                if (IndexOfOwner(province, playerId) != i) continue;

                if (text.Length > 0) text.Append('\n');
                var owner = session.GetPlayer(playerId);
                text.Append(owner == null ? "Neutral" : $"P{owner.PlayerId} {Faction(owner.Faction)}").Append(": ");
                AppendUnitCounts(text, province, playerId);
            }

            return text.ToString();
        }

        public static string Structures(ProvinceState province, PrototypeGameSession session)
        {
            var text = new StringBuilder();
            if (province.HasFort) text.Append("Fort");
            if (province.HasChurch) Append(text, $"Church ({OwnerName(session, province.ChurchOwnerId)})");
            if (province.PresenceToken != null)
            {
                Append(text, $"Presence ({OwnerName(session, province.PresenceToken.OwnerId)})");
            }

            if (province.RebellionCell != null)
            {
                string state = province.RebellionCell.IsOpenRebellion ? "Open rebellion" : "Undercover cell";
                Append(text, $"{state} ({OwnerName(session, province.RebellionCell.OwnerId)})");
            }

            return text.Length == 0 ? "No structures" : text.ToString();
        }

        public static string MoveButton(int unitsToMove) =>
            unitsToMove > 0 ? $"Move {unitsToMove} unit(s)" : "Move army";

        public static string UnitsToMove(int unitsToMove, int movable) => $"{unitsToMove}/{movable}";

        public static string Action(HudAction action, ProvinceState province, PrototypeGameSession session) =>
            action.Kind switch
            {
                HudActionKind.BuyUnit => $"Buy unit ({PrototypeGameSession.UnitCost}G)",
                HudActionKind.BuyMissionary => $"Missionary ({PrototypeGameSession.MissionaryCost}G)",
                HudActionKind.BuildChurch => $"Build church ({PrototypeGameSession.ChurchCost}G)",
                HudActionKind.Consecrate => "Consecrate",
                HudActionKind.DivineConversion => "Divine conversion",
                HudActionKind.PlaceRebellionCell => $"Place cell ({session.GetRebellionCellCost(province.Id)}G)",
                HudActionKind.InciteRebellion => "Incite rebellion",
                HudActionKind.MartialLaw => "Martial law",
                HudActionKind.Inquisition => "Inquisition",
                HudActionKind.CleanseProvince => "Cleanse province",
                HudActionKind.Attack => $"Attack {OwnerName(session, action.TargetPlayerId)}",
                _ => action.Kind.ToString()
            };

        public static string Shift(FactionType target, bool isAscension, bool isPending)
        {
            string modifier = isAscension ? "+10% VP" : "-20% VP";
            return isPending
                ? $"Confirm: become {Faction(target)} ({modifier})"
                : $"{(isAscension ? "Ascend as" : "Become")} {Faction(target)}";
        }

        public static string Command(CommandResult result) => result switch
        {
            CommandResult.Ok => string.Empty,
            CommandResult.GameOver => "The game is over.",
            CommandResult.NotYourProvince => "This province is not yours.",
            CommandResult.NoUnits => "There are no units here.",
            CommandResult.NotEnoughGold => "Not enough gold.",
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
            CommandResult.InvalidBattleTarget => "Choose a valid enemy force in this province.",
            CommandResult.ShiftNotAllowed => "That ruleset shift is not available.",
            _ => result.ToString()
        };

        public static string Hint(HudInteraction interaction)
        {
            if (interaction.IsTargetingMove) return "Click a highlighted province to move. Esc cancels.";
            if (interaction.IsTargetingConversion)
            {
                return "Click a highlighted neighbour to convert its units. Esc cancels.";
            }

            if (interaction.PendingShift.HasValue) return "Click the shift again to confirm. Esc cancels.";

            return interaction.SelectedProvinceId != HudInteraction.NoSelection
                ? "Pick an action or move units. Space ends the turn."
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

        private static string OwnerName(PrototypeGameSession session, int playerId)
        {
            var owner = session.GetPlayer(playerId);
            return owner == null ? "Neutral" : $"P{owner.PlayerId} {Faction(owner.Faction)}";
        }

        private static void Append(StringBuilder text, string part)
        {
            if (text.Length > 0) text.Append(" · ");
            text.Append(part);
        }

        private static int IndexOfOwner(ProvinceState province, int playerId)
        {
            for (int i = 0; i < province.Units.Count; i++)
            {
                if (province.Units[i].OwnerPlayerId == playerId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void AppendUnitCounts(StringBuilder text, ProvinceState province, int playerId)
        {
            bool first = true;
            var types = (UnitType[])System.Enum.GetValues(typeof(UnitType));
            for (int t = 0; t < types.Length; t++)
            {
                int count = 0;
                for (int i = 0; i < province.Units.Count; i++)
                {
                    if (province.Units[i].OwnerPlayerId == playerId && province.Units[i].Type == types[t])
                    {
                        count++;
                    }
                }

                if (count == 0) continue;

                if (!first) text.Append(", ");
                text.Append(count).Append(' ').Append(UnitTypeName(types[t]));
                first = false;
            }
        }
    }
}
