using UnityEngine;

namespace Presentation.Map
{
    public static class MapLayout
    {
        public static Vector2[] CreateContinentalHexPositions21(float hexWidth = 1.8f, float hexHeight = 1.56f) => new[]
        {
            new Vector2(-4.5f * hexWidth, 0.0f * hexHeight), // 0: Militant Capital
            new Vector2(-4.0f * hexWidth, 1.0f * hexHeight), // 1
            new Vector2(-3.5f * hexWidth, 0.0f * hexHeight), // 2
            new Vector2(-4.0f * hexWidth, -1.0f * hexHeight), // 3
            new Vector2(-3.0f * hexWidth, 1.0f * hexHeight), // 4
            new Vector2(-2.5f * hexWidth, 0.0f * hexHeight), // 5
            new Vector2(-3.0f * hexWidth, -1.0f * hexHeight), // 6
            new Vector2(-2.0f * hexWidth, 1.0f * hexHeight), // 7
            new Vector2(-1.0f * hexWidth, 1.5f * hexHeight), // 8: North Choke
            new Vector2(-1.5f * hexWidth, 0.0f * hexHeight), // 9: West-Center Frontier
            new Vector2(-0.5f * hexWidth, 0.5f * hexHeight), // 10: Central Crossroads
            new Vector2(-0.5f * hexWidth, -0.5f * hexHeight), // 11: South-Center Frontier
            new Vector2(-1.0f * hexWidth, -1.5f * hexHeight), // 12: South Choke
            new Vector2(4.5f * hexWidth, 0.0f * hexHeight), // 13: Zealot Capital
            new Vector2(0.5f * hexWidth, 0.0f * hexHeight), // 14: Zealot Border
            new Vector2(1.0f * hexWidth, 1.5f * hexHeight), // 15
            new Vector2(1.5f * hexWidth, 0.5f * hexHeight), // 16
            new Vector2(1.5f * hexWidth, -0.5f * hexHeight), // 17
            new Vector2(1.0f * hexWidth, -1.5f * hexHeight), // 18
            new Vector2(2.5f * hexWidth, 0.0f * hexHeight), // 19
            new Vector2(3.5f * hexWidth, -0.5f * hexHeight) // 20
        };
    }
}