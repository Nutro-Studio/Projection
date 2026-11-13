using System.Collections.Generic;

namespace Core.Combat
{
    public enum FactionType
    {
        Militants,
        Zealots,
        Guilds,
        Guerrillas
    }

    public enum UnitType
    {
        RegularMilitary,
        Missionary,
        Trader
    }

    public record UnitInstance(
        int Id,
        int OwnerPlayerId,
        FactionType OriginalFaction,
        UnitType Type,
        int CurrentHp,
        int MaxHp,
        bool HasActed = false
    );

    public record CombatContext(
        IReadOnlyList<UnitInstance> Attackers,
        IReadOnlyList<UnitInstance> Defenders,
        bool DefenderHasFort,
        bool DefenderReceivesFortBonus
    );

    public record CombatResult(
        int AttackerRawSum,
        int DefenderRawSum,
        int AttackerAssignedRoll,
        int DefenderAssignedRoll,
        int AttackerEffectiveHits,
        int DefenderEffectiveHits,
        IReadOnlyList<UnitInstance> SurvivingAttackers,
        IReadOnlyList<UnitInstance> SurvivingDefenders,
        bool AttackerMustRetreat,
        bool DefenderMustRetreat,
        (int roll1, int roll2, int roll3) AttackerRolls,
        (int roll1, int roll2, int roll3) DefenderRolls
    );

    public interface IDiceRoller
    {
        int RollD4();
        (int roll1, int roll2, int roll3) Roll3D4();
    }

    public interface ICombatService
    {
        CombatResult ResolveBattle(CombatContext context);
    }
}