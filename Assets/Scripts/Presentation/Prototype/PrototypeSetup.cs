using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;

namespace Presentation.Prototype
{
    /// <summary>
    /// Starting layout of one province, as authored on its view in the scene.
    /// </summary>
    public sealed class ProvinceSeed
    {
        public const int NoRebellionCell = -1;

        public int Id { get; }
        public string Name { get; }
        public bool HasFort { get; }
        public IReadOnlyList<int> Neighbours { get; }
        public int OwnerId { get; }
        public int RegularUnits { get; }
        public int Missionaries { get; }
        public bool HasChurch { get; }
        public int RebellionCellOwnerId { get; }
        public int Traders { get; }
        public int TradingPostOwnerId { get; }

        public ProvinceSeed(
            int id,
            string name,
            bool hasFort,
            IReadOnlyList<int> neighbours,
            int ownerId,
            int regularUnits,
            int missionaries = 0,
            bool hasChurch = false,
            int rebellionCellOwnerId = NoRebellionCell,
            int traders = 0,
            int tradingPostOwnerId = ProvinceState.NoTradingPost)
        {
            Id = id;
            Name = name;
            HasFort = hasFort;
            Neighbours = neighbours;
            OwnerId = ownerId;
            RegularUnits = regularUnits;
            Missionaries = missionaries;
            HasChurch = hasChurch;
            RebellionCellOwnerId = rebellionCellOwnerId;
            Traders = traders;
            TradingPostOwnerId = tradingPostOwnerId;
        }
    }

    /// <summary>
    /// Builds the starting players and provinces of the prototype match.
    /// </summary>
    public static class PrototypeSetup
    {
        public const int MilitantsPlayerId = 1;
        public const int ZealotsPlayerId = 2;
        public const int GuerrillasPlayerId = 3;
        public const int GuildsPlayerId = 4;

        public static PlayerProfile[] CreatePlayers() => new[]
        {
            CreatePlayer(MilitantsPlayerId, FactionType.Militants),
            CreatePlayer(ZealotsPlayerId, FactionType.Zealots),
            CreatePlayer(GuerrillasPlayerId, FactionType.Guerrillas),
            CreatePlayer(GuildsPlayerId, FactionType.Guilds)
        };

        public static List<ProvinceState> BuildProvinces(
            IReadOnlyList<ProvinceSeed> seeds,
            IReadOnlyList<PlayerProfile> players)
        {
            var provinces = new List<ProvinceState>(seeds.Count);
            int nextUnitId = 0;
            for (int i = 0; i < seeds.Count; i++)
            {
                var seed = seeds[i];
                var faction = FactionOf(players, seed.OwnerId);
                int startingTraders = seed.Traders == 0 && seed.TradingPostOwnerId == GuildsPlayerId
                    ? 1
                    : seed.Traders;

                var units = new List<UnitInstance>(seed.RegularUnits + seed.Missionaries + startingTraders);
                for (int u = 0; u < seed.RegularUnits; u++)
                {
                    units.Add(new UnitInstance(nextUnitId++, seed.OwnerId, faction, UnitType.RegularMilitary, 1, 1));
                }

                for (int u = 0; u < seed.Missionaries; u++)
                {
                    units.Add(new UnitInstance(nextUnitId++, seed.OwnerId, faction, UnitType.Missionary, 1, 1));
                }

                for (int u = 0; u < startingTraders; u++)
                {
                    units.Add(new UnitInstance(nextUnitId++, GuildsPlayerId, FactionType.Guilds, UnitType.Trader, 1,
                        1));
                }

                var cell = seed.RebellionCellOwnerId == ProvinceSeed.NoRebellionCell
                    ? null
                    : new RebellionCell(seed.RebellionCellOwnerId, false);

                provinces.Add(new ProvinceState(seed.Id, seed.Name, seed.HasFort, seed.Neighbours, seed.OwnerId, units,
                    seed.HasChurch ? seed.OwnerId : ProvinceState.NoChurch, null, cell, seed.TradingPostOwnerId));
            }

            return provinces;
        }

        private static PlayerProfile CreatePlayer(int playerId, FactionType faction) =>
            new(playerId, faction, PrototypeGameSession.StartingGold, 0, new List<UnitInstance>());

        private static FactionType FactionOf(IReadOnlyList<PlayerProfile> players, int playerId)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].PlayerId == playerId)
                {
                    return players[i].Faction;
                }
            }

            return FactionType.Guerrillas;
        }
    }
}