namespace Core.Map
{
    /// <summary>
    /// The 21-province continent: a 7x3 grid, 4-neighbour adjacency.
    /// Columns 0-2 (minus the centre of column 2) are Militants, column 3 plus the edges around it are neutral,
    /// columns 4-6 (minus the centre of column 4) are Zealots. 8 / 5 / 8 as in the rulebook.
    ///
    ///   col:   0      1      2      3      4      5      6
    ///   row0:  M      M      M      N      Z      Z      Z
    ///   row1:  M     [M]     N      N      N     [Z]     Z
    ///   row2:  M      M      M      N      Z      Z      Z
    /// </summary>
    public static class StandardMap
    {
        public const int Columns = 7;
        public const int Rows = 3;

        private static readonly string[,] Names =
        {
            // [row, col]
            { "Northmarch", "Ironhold", "Greywall", "Ashen Pass", "Lumen Vale", "Choirfield", "Saint's Rest" },
            { "Westreach", "Bastion", "Rustford", "Crossroads", "Old Mill", "Sanctum", "Pilgrim Coast" },
            { "Southmarch", "Forgehill", "Redfort", "Drylands", "Candlemoor", "Abbey Hills", "Reliquary" },
        };

        public static ProvinceGraph Create(Faction militants, Faction zealots)
        {
            var graph = new ProvinceGraph();

            for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Columns; x++)
            {
                bool capital = (x == 1 || x == 5) && y == 1;
                var province = graph.Add(new Province(IdOf(x, y), Names[y, x], x, y, capital));
                province.Owner = OwnerOf(x, y, militants, zealots);
            }

            for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Columns; x++)
            {
                if (x + 1 < Columns) graph.Connect(IdOf(x, y), IdOf(x + 1, y));
                if (y + 1 < Rows) graph.Connect(IdOf(x, y), IdOf(x, y + 1));
            }

            return graph;
        }

        public static int IdOf(int x, int y) => y * Columns + x;

        private static Faction OwnerOf(int x, int y, Faction militants, Faction zealots)
        {
            if (x <= 1 || (x == 2 && y != 1)) return militants;
            if (x >= 5 || (x == 4 && y != 1)) return zealots;
            return null;
        }
    }
}
