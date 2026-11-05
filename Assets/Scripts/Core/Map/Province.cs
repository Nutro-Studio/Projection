namespace Projection.Core.Map
{
    public class Province
    {
        public int Id { get; }
        public string Name { get; }

        /// <summary>Layout cell on the map grid (column, row). Used only for drawing.</summary>
        public int GridX { get; }
        public int GridY { get; }

        public bool IsCapital { get; }

        /// <summary>null = neutral.</summary>
        public Faction Owner { get; set; }

        public bool HasFortification { get; set; }

        /// <summary>Church belongs to whoever built it and keeps collecting tithes for them.</summary>
        public Faction ChurchOwner { get; set; }
        public bool HasChurch => ChurchOwner != null;

        public Faction TradingPostOwner { get; set; }
        public bool HasTradingPost => TradingPostOwner != null;

        public Province(int id, string name, int gridX, int gridY, bool isCapital = false)
        {
            Id = id;
            Name = name;
            GridX = gridX;
            GridY = gridY;
            IsCapital = isCapital;
        }

        public override string ToString() => Name;
    }
}
