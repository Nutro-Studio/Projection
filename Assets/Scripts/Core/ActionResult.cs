namespace Projection.Core
{
    /// <summary>Outcome of a player action: either success or a human-readable reason it was rejected.</summary>
    public readonly struct ActionResult
    {
        public bool Success { get; }
        public string Message { get; }

        private ActionResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static ActionResult Ok(string message = "") => new ActionResult(true, message);
        public static ActionResult Fail(string reason) => new ActionResult(false, reason);

        public override string ToString() => (Success ? "OK: " : "FAIL: ") + Message;
    }
}
