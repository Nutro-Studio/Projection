namespace Presentation.Hud
{
    public enum HudActionKind
    {
        BuyUnit,
        BuyMissionary,
        BuildChurch,
        Consecrate,
        DivineConversion,
        PlaceRebellionCell,
        InciteRebellion,
        MartialLaw,
        Inquisition,
        CleanseProvince,
        Attack
    }

    /// <summary>
    /// A command the selected province offers the current player. <see cref="TargetPlayerId"/> is only used by
    /// <see cref="HudActionKind.Attack"/>.
    /// </summary>
    public readonly struct HudAction
    {
        public HudActionKind Kind { get; }
        public int TargetPlayerId { get; }

        public HudAction(HudActionKind kind, int targetPlayerId = -1)
        {
            Kind = kind;
            TargetPlayerId = targetPlayerId;
        }
    }
}
