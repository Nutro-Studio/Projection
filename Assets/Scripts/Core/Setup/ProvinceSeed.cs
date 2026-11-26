using System.Collections.Generic;
using Core.Board;

namespace Core.Setup
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
}