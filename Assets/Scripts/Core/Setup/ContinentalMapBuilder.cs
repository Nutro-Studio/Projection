using System.Collections.Generic;
using Core.Board;
using Core.Combat;
using Core.Crisis;
using Core.Simulation;

namespace Core.Setup
{
    /// <summary>
    /// Builds the starting players and 21-province continental map graph.
    /// </summary>
    public static class ContinentalMapBuilder
    {
        public const int MilitantsPlayerId = 1;
        public const int ZealotsPlayerId = 2;
        public const int GuerrillasPlayerId = 3;
        public const int GuildsPlayerId = 4;

        public const int ContinentalProvinceCount = 21;
        public const int MilitantCapitalId = 0;
        public const int ZealotCapitalId = 13;
        public const int GuerrillaCellProvinceId = 14;

        public static PlayerProfile[] CreatePlayers(GameBalanceConfig config = null)
        {
            var cfg = config ?? GameBalanceConfig.Default;
            return new[]
            {
                CreatePlayer(MilitantsPlayerId, FactionType.Militants, cfg.StartingGold),
                CreatePlayer(ZealotsPlayerId, FactionType.Zealots, cfg.StartingGold),
                CreatePlayer(GuerrillasPlayerId, FactionType.Guerrillas, cfg.StartingGold),
                CreatePlayer(GuildsPlayerId, FactionType.Guilds, cfg.StartingGold)
            };
        }

        public static ProvinceSeed[] CreateContinentalMap21() => new[]
        {
            // --- Militant West (0..7) ---
            new ProvinceSeed(0, string.Empty, hasFort: true, neighbours: new[] { 1, 2, 3 },
                ownerId: MilitantsPlayerId, regularUnits: 3, traders: 1, tradingPostOwnerId: GuildsPlayerId),
            new ProvinceSeed(1, string.Empty, hasFort: false, neighbours: new[] { 0, 2, 4 },
                ownerId: MilitantsPlayerId, regularUnits: 0),
            new ProvinceSeed(2, string.Empty, hasFort: false, neighbours: new[] { 0, 1, 3, 4, 5 },
                ownerId: MilitantsPlayerId, regularUnits: 0),
            new ProvinceSeed(3, string.Empty, hasFort: false, neighbours: new[] { 0, 2, 5, 6 },
                ownerId: MilitantsPlayerId, regularUnits: 0),
            new ProvinceSeed(4, string.Empty, hasFort: false, neighbours: new[] { 1, 2, 5, 7, 8 },
                ownerId: MilitantsPlayerId, regularUnits: 0),
            new ProvinceSeed(5, string.Empty, hasFort: false, neighbours: new[] { 2, 3, 4, 6, 7, 9 },
                ownerId: MilitantsPlayerId, regularUnits: 0),
            new ProvinceSeed(6, string.Empty, hasFort: false, neighbours: new[] { 3, 5, 11, 12 },
                ownerId: MilitantsPlayerId, regularUnits: 0),
            new ProvinceSeed(7, string.Empty, hasFort: false, neighbours: new[] { 4, 5, 8, 9, 10 },
                ownerId: MilitantsPlayerId, regularUnits: 0),

            // --- Neutral Frontier Choke Points (8..12) ---
            new ProvinceSeed(8, string.Empty, hasFort: false, neighbours: new[] { 4, 7, 10, 15 },
                ownerId: ProvinceState.NeutralOwner, regularUnits: 0),
            new ProvinceSeed(9, string.Empty, hasFort: false, neighbours: new[] { 5, 7, 10, 11 },
                ownerId: ProvinceState.NeutralOwner, regularUnits: 0),
            new ProvinceSeed(10, string.Empty, hasFort: false, neighbours: new[] { 7, 8, 9, 14, 15, 16 },
                ownerId: ProvinceState.NeutralOwner, regularUnits: 0),
            new ProvinceSeed(11, string.Empty, hasFort: false, neighbours: new[] { 6, 9, 12, 14, 17 },
                ownerId: ProvinceState.NeutralOwner, regularUnits: 0),
            new ProvinceSeed(12, string.Empty, hasFort: false, neighbours: new[] { 6, 11, 17, 18 },
                ownerId: ProvinceState.NeutralOwner, regularUnits: 0),

            // --- Zealot East (13..20) ---
            new ProvinceSeed(13, string.Empty, hasFort: false, neighbours: new[] { 16, 19, 20 },
                ownerId: ZealotsPlayerId, regularUnits: 0, missionaries: 1, hasChurch: true,
                traders: 0, tradingPostOwnerId: GuildsPlayerId, hasPresenceToken: true),
            new ProvinceSeed(14, string.Empty, hasFort: false, neighbours: new[] { 10, 11, 16, 17, 19 },
                ownerId: ZealotsPlayerId, regularUnits: 0,
                rebellionCellOwnerId: GuerrillasPlayerId, rebellionUnits: 1),
            new ProvinceSeed(15, string.Empty, hasFort: false, neighbours: new[] { 8, 10, 16, 19 },
                ownerId: ZealotsPlayerId, regularUnits: 0),
            new ProvinceSeed(16, string.Empty, hasFort: false, neighbours: new[] { 10, 13, 14, 15, 19 },
                ownerId: ZealotsPlayerId, regularUnits: 0),
            new ProvinceSeed(17, string.Empty, hasFort: false, neighbours: new[] { 11, 12, 14, 18, 19, 20 },
                ownerId: ZealotsPlayerId, regularUnits: 0),
            new ProvinceSeed(18, string.Empty, hasFort: false, neighbours: new[] { 12, 17, 20 },
                ownerId: ZealotsPlayerId, regularUnits: 0),
            new ProvinceSeed(19, string.Empty, hasFort: false, neighbours: new[] { 13, 14, 15, 16, 17, 20 },
                ownerId: ZealotsPlayerId, regularUnits: 0),
            new ProvinceSeed(20, string.Empty, hasFort: false, neighbours: new[] { 13, 17, 18, 19 },
                ownerId: ZealotsPlayerId, regularUnits: 0)
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

                var units = new List<UnitInstance>(
                    seed.RegularUnits + seed.Missionaries + seed.Traders + seed.RebellionUnits);

                for (int u = 0; u < seed.RegularUnits; u++)
                {
                    units.Add(new UnitInstance(nextUnitId++, seed.OwnerId, faction, UnitType.RegularMilitary, 1, 1));
                }

                for (int u = 0; u < seed.Missionaries; u++)
                {
                    units.Add(new UnitInstance(nextUnitId++, seed.OwnerId, faction, UnitType.Missionary, 1, 1));
                }

                for (int u = 0; u < seed.Traders; u++)
                {
                    units.Add(new UnitInstance(nextUnitId++, GuildsPlayerId, FactionType.Guilds, UnitType.Trader, 1,
                        1));
                }

                for (int u = 0; u < seed.RebellionUnits; u++)
                {
                    int rebelOwner = seed.RebellionCellOwnerId != ProvinceSeed.NoRebellionCell
                        ? seed.RebellionCellOwnerId
                        : GuerrillasPlayerId;
                    units.Add(new UnitInstance(nextUnitId++, rebelOwner, FactionType.Guerrillas,
                        UnitType.RegularMilitary, 1, 1));
                }

                var presence = seed.HasPresenceToken && seed.OwnerId != ProvinceState.NeutralOwner
                    ? new ReligiousPresenceToken(seed.OwnerId)
                    : null;

                var cell = seed.RebellionCellOwnerId == ProvinceSeed.NoRebellionCell
                    ? null
                    : new RebellionCell(seed.RebellionCellOwnerId, false);

                provinces.Add(new ProvinceState(seed.Id, seed.Name, seed.HasFort, seed.Neighbours, seed.OwnerId, units,
                    seed.HasChurch ? seed.OwnerId : ProvinceState.NoChurch, presence, cell, seed.TradingPostOwnerId));
            }

            return provinces;
        }

        private static PlayerProfile CreatePlayer(int playerId, FactionType faction, int startingGold) =>
            new(playerId, faction, startingGold, 0, new List<UnitInstance>());

        private static FactionType FactionOf(IReadOnlyList<PlayerProfile> players, int playerId)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].PlayerId == playerId) return players[i].Faction;
            }

            return FactionType.Guerrillas;
        }
    }
}