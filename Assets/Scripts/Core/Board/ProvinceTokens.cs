namespace Core.Board
{
    /// <summary>
    /// Guerrilla cell. It stays undercover until the owner incites an open rebellion. A province holds at most one.
    /// </summary>
    public sealed class RebellionCell
    {
        public int OwnerId { get; }
        public bool IsOpenRebellion { get; }

        public RebellionCell(int ownerId, bool isOpenRebellion)
        {
            OwnerId = ownerId;
            IsOpenRebellion = isOpenRebellion;
        }
    }

    /// <summary>
    /// Zealot consecration marker. A province holds at most one.
    /// </summary>
    public sealed class ReligiousPresenceToken
    {
        public int OwnerId { get; }

        public ReligiousPresenceToken(int ownerId) => OwnerId = ownerId;
    }
}