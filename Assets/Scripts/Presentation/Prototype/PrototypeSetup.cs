using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;
using UnityEngine;

namespace Presentation.Prototype
{
    /// <summary>
    /// Starting layout of one province. Names are intentionally omitted.
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
        public bool HasPresenceToken { get; }
        public int RebellionCellOwnerId { get; }
        public int RebellionUnits { get; }
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
            int tradingPostOwnerId = ProvinceState.NoTradingPost,
            bool hasPresenceToken = false,
            int rebellionUnits = 0)
        {
            Id = id;
            Name = name ?? string.Empty;
            HasFort = hasFort;
            Neighbours = neighbours;
            OwnerId = ownerId;
            RegularUnits = regularUnits;
            Missionaries = missionaries;
            HasChurch = hasChurch;
            HasPresenceToken = hasPresenceToken;
            RebellionCellOwnerId = rebellionCellOwnerId;
            RebellionUnits = rebellionUnits;
            Traders = traders;
            TradingPostOwnerId = tradingPostOwnerId;
        }
    }

    /// <summary>
    /// Builds the starting players and 21-province continental map graph.
    /// </summary>
    public static class PrototypeSetup
    {
        public const int MilitantsPlayerId = 1;
        public const int ZealotsPlayerId = 2;
        public const int GuerrillasPlayerId = 3;
        public const int GuildsPlayerId = 4;

        public const int ContinentalProvinceCount = 21;
        public const int MilitantCapitalId = 0;
        public const int ZealotCapitalId = 13;
        public const int GuerrillaCellProvinceId = 14;

        public static PlayerProfile[] CreatePlayers() => new[]
        {
            CreatePlayer(MilitantsPlayerId, FactionType.Militants),
            CreatePlayer(ZealotsPlayerId, FactionType.Zealots),
            CreatePlayer(GuerrillasPlayerId, FactionType.Guerrillas),
            CreatePlayer(GuildsPlayerId, FactionType.Guilds)
        };

        /// <summary>
        /// 21-province continental graph (no province names):
        /// - 0..7: Militants (8 provinces). Capital 0: 1 Fort, 3 Warriors, 1 Guild Trading Post, 1 Guild Trader.
        /// - 8..12: Neutral Frontier (5 choke-point provinces).
        /// - 13..20: Zealots (8 provinces). Capital 13: 1 Church, 1 Missionary, 1 Religious Presence, 1 Guild Trading Post.
        ///   Province 14: 1 Guerrilla undercover cell + 1 Guerrilla rebel unit.
        /// </summary>
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

        /// <summary>
        /// World-space 2D hex coordinates matching the adjacency topology of <see cref="CreateContinentalMap21"/>.
        /// </summary>
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
            new Vector2(-1.0f * hexWidth, 1.5f * hexHeight), // 8: North Choke (Neutral)
            new Vector2(-1.5f * hexWidth, 0.0f * hexHeight), // 9: West-Center Frontier (Neutral)
            new Vector2(-0.5f * hexWidth, 0.5f * hexHeight), // 10: Central Crossroads (Neutral)
            new Vector2(-0.5f * hexWidth, -0.5f * hexHeight), // 11: South-Center Frontier (Neutral)
            new Vector2(-1.0f * hexWidth, -1.5f * hexHeight), // 12: South Choke (Neutral)
            new Vector2(4.5f * hexWidth, 0.0f * hexHeight), // 13: Zealot Capital
            new Vector2(0.5f * hexWidth, 0.0f * hexHeight), // 14: Zealot Border (Guerrilla Cell + Rebel)
            new Vector2(1.0f * hexWidth, 1.5f * hexHeight), // 15
            new Vector2(1.5f * hexWidth, 0.5f * hexHeight), // 16
            new Vector2(1.5f * hexWidth, -0.5f * hexHeight), // 17
            new Vector2(1.0f * hexWidth, -1.5f * hexHeight), // 18
            new Vector2(2.5f * hexWidth, 0.0f * hexHeight), // 19
            new Vector2(3.5f * hexWidth, -0.5f * hexHeight) // 20
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