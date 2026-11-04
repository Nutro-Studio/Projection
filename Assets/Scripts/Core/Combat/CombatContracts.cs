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

    public record CombatUnit(
        int Id,
        FactionType Faction,
        int MaxHp,
        int CurrentHp
    );

    public record CombatContext(
        IReadOnlyList<CombatUnit> Attackers,
        IReadOnlyList<CombatUnit> Defenders,
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
        IReadOnlyList<CombatUnit> SurvivingAttackers,
        IReadOnlyList<CombatUnit> SurvivingDefenders,
        bool AttackerMustRetreat,
        bool DefenderMustRetreat
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