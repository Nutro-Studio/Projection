using System.Collections.Generic;
using Core.Combat;

namespace Core.Board
{
    public sealed class ProvinceState
    {
        public const int NeutralOwner = -1;
        public const int NoChurch = -1;
        public const int NoTradingPost = -1;

        public int Id { get; }
        public string Name { get; }
        public bool HasFort { get; }
        public IReadOnlyList<int> Neighbours { get; }
        public int OwnerId { get; }
        public IReadOnlyList<UnitInstance> Units { get; }
        public int ChurchOwnerId { get; }
        public int TradingPostOwnerId { get; }
        public ReligiousPresenceToken PresenceToken { get; }
        public RebellionCell RebellionCell { get; }

        public bool HasChurch => ChurchOwnerId != NoChurch;
        public bool HasTradingPost => TradingPostOwnerId != NoTradingPost;

        public ProvinceState(
            int id,
            string name,
            bool hasFort,
            IReadOnlyList<int> neighbours,
            int ownerId,
            IReadOnlyList<UnitInstance> units,
            int churchOwnerId = NoChurch,
            ReligiousPresenceToken presenceToken = null,
            RebellionCell rebellionCell = null,
            int tradingPostOwnerId = NoTradingPost)
        {
            Id = id;
            Name = name;
            HasFort = hasFort;
            Neighbours = neighbours;
            OwnerId = ownerId;
            Units = units;
            ChurchOwnerId = churchOwnerId;
            PresenceToken = presenceToken;
            RebellionCell = rebellionCell;
            TradingPostOwnerId = tradingPostOwnerId;
        }

        public ProvinceState WithOccupation(int ownerId, IReadOnlyList<UnitInstance> units) =>
            new(Id, Name, HasFort, Neighbours, ownerId, units, ChurchOwnerId, PresenceToken, RebellionCell,
                TradingPostOwnerId);

        public ProvinceState WithUnits(IReadOnlyList<UnitInstance> units) =>
            new(Id, Name, HasFort, Neighbours, OwnerId, units, ChurchOwnerId, PresenceToken, RebellionCell,
                TradingPostOwnerId);

        public ProvinceState WithChurch(int churchOwnerId) =>
            new(Id, Name, HasFort, Neighbours, OwnerId, Units, churchOwnerId, PresenceToken, RebellionCell,
                TradingPostOwnerId);

        public ProvinceState WithTradingPost(int tradingPostOwnerId) =>
            new(Id, Name, HasFort, Neighbours, OwnerId, Units, ChurchOwnerId, PresenceToken, RebellionCell,
                tradingPostOwnerId);

        public ProvinceState WithPresence(ReligiousPresenceToken presenceToken) =>
            new(Id, Name, HasFort, Neighbours, OwnerId, Units, ChurchOwnerId, presenceToken, RebellionCell,
                TradingPostOwnerId);

        public ProvinceState WithRebellionCell(RebellionCell rebellionCell) =>
            new(Id, Name, HasFort, Neighbours, OwnerId, Units, ChurchOwnerId, PresenceToken, rebellionCell,
                TradingPostOwnerId);

        public ProvinceState WithFort(bool hasFort) =>
            new(Id, Name, hasFort, Neighbours, OwnerId, Units, ChurchOwnerId, PresenceToken, RebellionCell,
                TradingPostOwnerId);

        public bool IsAdjacentTo(int provinceId)
        {
            for (int i = 0; i < Neighbours.Count; i++)
            {
                if (Neighbours[i] == provinceId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}