using Core.Combat;
using UnityEngine;

namespace Presentation.Map
{
    public static class FactionPalette
    {
        public static readonly Color Neutral = new(0.48f, 0.5f, 0.53f);

        private static readonly Color Militants = new(0.71f, 0.26f, 0.24f);
        private static readonly Color Zealots = new(0.79f, 0.64f, 0.15f);
        private static readonly Color Guilds = new(0.24f, 0.49f, 0.69f);
        private static readonly Color Guerrillas = new(0.3f, 0.55f, 0.29f);

        public static Color For(FactionType faction) => faction switch
        {
            FactionType.Militants => Militants,
            FactionType.Zealots => Zealots,
            FactionType.Guilds => Guilds,
            FactionType.Guerrillas => Guerrillas,
            _ => Neutral
        };
    }
}
