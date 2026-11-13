using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;

namespace Presentation.Prototype
{
    public enum CommandResult
    {
        Ok,
        GameOver,
        NotYourProvince,
        NoUnits,
        NotEnoughGold,
        NotAdjacent,
        UnitsAlreadyMoved,
        NoUnitsSelected,
        InvalidUnitSelection,
        NoActionPoints,
        WrongFaction,
        NoMissionary,
        AlreadyBuilt,
        NoMilitaryUnits,
        MissionariesCannotConquer,
        NoTargetUnits,
        NoRebellionCell,
        NothingToPurge,
        InvalidBattleTarget
    }

    /// <summary>
    /// Guerrilla cell. It stays undercover until the owner incites an open rebellion. A province holds at most one.
    /// </summary>
    public sealed class RebellionCell
    {
        public int OwnerId { get; }
        public bool IsOpenRebellion { get; }

        public RebellionCell(int ownerId, bool isOpenRebellion)
        {
            OwnerId = ownerId;
            IsOpenRebellion = isOpenRebellion;
        }
    }

    /// <summary>
    /// Zealot consecration marker. A province holds at most one.
    /// </summary>
    public sealed class ReligiousPresenceToken
    {
        public int OwnerId { get; }

        public ReligiousPresenceToken(int ownerId) => OwnerId = ownerId;
    }

    public sealed class MoveArmyCommand
    {
        public int OriginProvinceId { get; }
        public int TargetProvinceId { get; }
        public IReadOnlyList<int> SelectedUnitIds { get; }

        public MoveArmyCommand(int originProvinceId, int targetProvinceId, IReadOnlyList<int> selectedUnitIds)
        {
            OriginProvinceId = originProvinceId;
            TargetProvinceId = targetProvinceId;
            SelectedUnitIds = selectedUnitIds;
        }
    }

    /// <summary>
    /// Rules owned by the Core systems. The session only reaches them through this seam.
    /// </summary>
    public interface IGameRules
    {
        CombatResult ResolveSkirmish(
            IReadOnlyList<UnitInstance> attackers,
            IReadOnlyList<UnitInstance> defenders,
            FactionType defenderFaction,
            bool hasFortification);

        PlayerProfile EvaluatePlayerTurnState(PlayerProfile player, int controlledProvinces);
        int CalculateRevenue(FactionType faction, int controlledProvinces);

        /// <summary>One side of a contested roll such as Divine Conversion: the sum of 3d4.</summary>
        int RollContest();
    }

    public sealed class ProvinceState
    {
        public const int NeutralOwner = -1;
        public const int NoChurch = -1;

        public int Id { get; }
        public string Name { get; }
        public bool HasFort { get; }
        public IReadOnlyList<int> Neighbours { get; }
        public int OwnerId { get; }
        public IReadOnlyList<UnitInstance> Units { get; }

        /// <summary>Player who built the Church here, or <see cref="NoChurch"/>.</summary>
        public int ChurchOwnerId { get; }

        public ReligiousPresenceToken PresenceToken { get; }
        public RebellionCell RebellionCell { get; }

        public bool HasChurch => ChurchOwnerId != NoChurch;

        public ProvinceState(
            int id,
            string name,
            bool hasFort,
            IReadOnlyList<int> neighbours,
            int ownerId,
            IReadOnlyList<UnitInstance> units,
            int churchOwnerId = NoChurch,
            ReligiousPresenceToken presenceToken = null,
            RebellionCell rebellionCell = null)
        {
            Id = id;
            Name = name;
            HasFort = hasFort;
            Neighbours = neighbours;
            OwnerId = ownerId;
            Units = units;
            ChurchOwnerId = churchOwnerId;
            PresenceToken = presenceToken;
            RebellionCell = rebellionCell;
        }

        public ProvinceState WithOccupation(int ownerId, IReadOnlyList<UnitInstance> units) =>
            new(Id, Name, HasFort, Neighbours, ownerId, units, ChurchOwnerId, PresenceToken, RebellionCell);

        public ProvinceState WithUnits(IReadOnlyList<UnitInstance> units) =>
            new(Id, Name, HasFort, Neighbours, OwnerId, units, ChurchOwnerId, PresenceToken, RebellionCell);

        public ProvinceState WithChurch(int churchOwnerId) =>
            new(Id, Name, HasFort, Neighbours, OwnerId, Units, churchOwnerId, PresenceToken, RebellionCell);

        public ProvinceState WithPresence(ReligiousPresenceToken presenceToken) =>
            new(Id, Name, HasFort, Neighbours, OwnerId, Units, ChurchOwnerId, presenceToken, RebellionCell);

        public ProvinceState WithRebellionCell(RebellionCell rebellionCell) =>
            new(Id, Name, HasFort, Neighbours, OwnerId, Units, ChurchOwnerId, PresenceToken, rebellionCell);

        public bool IsAdjacentTo(int provinceId)
        {
            for (int i = 0; i < Neighbours.Count; i++)
            {
                if (Neighbours[i] == provinceId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class BattleReport
    {
        public string ProvinceName { get; }
        public int AttackerId { get; }
        public int DefenderId { get; }
        public int AttackerCount { get; }
        public int DefenderCount { get; }
        public bool ProvinceCaptured { get; }
        public CombatResult Result { get; }

        public int AttackerCasualties => AttackerCount - Result.SurvivingAttackers.Count;
        public int DefenderCasualties => DefenderCount - Result.SurvivingDefenders.Count;

        public BattleReport(
            string provinceName,
            int attackerId,
            int defenderId,
            int attackerCount,
            int defenderCount,
            bool provinceCaptured,
            CombatResult result)
        {
            ProvinceName = provinceName;
            AttackerId = attackerId;
            DefenderId = defenderId;
            AttackerCount = attackerCount;
            DefenderCount = defenderCount;
            ProvinceCaptured = provinceCaptured;
            Result = result;
        }
    }
}