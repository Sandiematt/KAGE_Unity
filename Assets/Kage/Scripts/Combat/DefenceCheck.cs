namespace Kage
{
    /// <summary>
    /// One incoming hit passed through the defence checks (ADR-0004). A check that takes the hit sets
    /// <see cref="resolved"/> and the <see cref="result"/> to return; the rest of the order is skipped.
    /// </summary>
    public struct DefenceCheck
    {
        public HitInfo info;      // the incoming hit, as resolved so far
        public bool resolved;     // set by a check that took the hit
        public HitResult result;  // the result to return when resolved
    }
}
