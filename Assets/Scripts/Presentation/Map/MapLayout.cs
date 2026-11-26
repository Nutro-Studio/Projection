using UnityEngine;

namespace Presentation.Map
{
    public static class MapLayout
    {
        public static Vector2[] CreateContinentalHexPositions21(float colSpacing = 2.5f, float rowSpacing = 1.85f) =>
            new[]
            {
                // --- Militant West (0..7) ---
                new Vector2(-4.5f * colSpacing, 0.0f * rowSpacing), // 0: Militant Capital
                new Vector2(-3.5f * colSpacing, 1.2f * rowSpacing), // 1
                new Vector2(-3.5f * colSpacing, 0.0f * rowSpacing), // 2
                new Vector2(-3.5f * colSpacing, -1.2f * rowSpacing), // 3
                new Vector2(-2.5f * colSpacing, 1.4f * rowSpacing), // 4
                new Vector2(-2.5f * colSpacing, 0.0f * rowSpacing), // 5
                new Vector2(-2.5f * colSpacing, -1.4f * rowSpacing), // 6
                new Vector2(-1.6f * colSpacing, 0.7f * rowSpacing), // 7

                // --- Neutral Frontier Choke Points (8..12) ---
                new Vector2(-0.7f * colSpacing, 1.6f * rowSpacing), // 8: North Choke
                new Vector2(-1.5f * colSpacing, -0.7f * rowSpacing), // 9: West-Center Frontier
                new Vector2(-0.5f * colSpacing, 0.5f * rowSpacing), // 10: Central Crossroads
                new Vector2(-0.5f * colSpacing, -0.6f * rowSpacing), // 11: South-Center Frontier
                new Vector2(-0.7f * colSpacing, -1.6f * rowSpacing), // 12: South Choke

                // --- Zealot East (13..20) ---
                new Vector2(4.5f * colSpacing, 0.0f * rowSpacing), // 13: Zealot Capital
                new Vector2(0.5f * colSpacing, 0.0f * rowSpacing), // 14: Zealot Border
                new Vector2(0.8f * colSpacing, 1.5f * rowSpacing), // 15
                new Vector2(1.6f * colSpacing, 0.7f * rowSpacing), // 16
                new Vector2(1.6f * colSpacing, -0.7f * rowSpacing), // 17
                new Vector2(0.8f * colSpacing, -1.5f * rowSpacing), // 18
                new Vector2(2.6f * colSpacing, 0.0f * rowSpacing), // 19
                new Vector2(3.5f * colSpacing, -0.9f * rowSpacing) // 20
            };
    }
}