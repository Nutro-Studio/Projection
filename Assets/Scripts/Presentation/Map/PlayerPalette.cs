using UnityEngine;

namespace Presentation.Map
{
    public static class PlayerPalette
    {
        public static readonly Color Neutral = new(0.48f, 0.50f, 0.53f);

        private static readonly Color[] PlayerColors =
        {
            new(0.71f, 0.26f, 0.24f), // P1: Crimson
            new(0.79f, 0.64f, 0.15f), // P2: Ochre Gold
            new(0.30f, 0.55f, 0.29f), // P3: Forest Green
            new(0.24f, 0.49f, 0.69f), // P4: Steel Blue
            new(0.56f, 0.31f, 0.68f), // P5: Royal Violet
            new(0.82f, 0.44f, 0.18f), // P6: Burnt Orange
            new(0.18f, 0.64f, 0.62f), // P7: Teal
            new(0.76f, 0.34f, 0.56f), // P8: Magenta Rose
            new(0.42f, 0.58f, 0.22f) // P9: Olive Lime
        };

        public static Color ForPlayerId(int playerId)
        {
            if (playerId <= 0) return Neutral;
            return PlayerColors[(playerId - 1) % PlayerColors.Length];
        }
    }
}