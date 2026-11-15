namespace TheLastHuman
{
    /// <summary>What carries over from one scene to the next.</summary>
    public static class GameProgress
    {
        /// <summary>
        /// Set when the player escapes through the last portal. The human world (the Workplace
        /// scene) then opens on the celebration instead of the first shift.
        /// </summary>
        public static bool HeroReturns;
    }
}
