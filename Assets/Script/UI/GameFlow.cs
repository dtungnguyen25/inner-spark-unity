namespace Pcb
{
    /// <summary>Carries the level to start on from the Main Menu scene into the gameplay scene.</summary>
    public static class GameFlow
    {
        static int requestedLevel;
        static bool hasRequest;

        /// <summary>True once Main Menu / Stage Select has asked for a specific level and it hasn't been consumed yet.</summary>
        public static bool HasPendingRequest => hasRequest;

        /// <summary>Called by Main Menu / Stage Select before loading the gameplay scene.</summary>
        public static void RequestLevel(int index)
        {
            requestedLevel = index;
            hasRequest = true;
        }

        /// <summary>Consumes the pending request, if any; otherwise returns 'fallback' (so testing the gameplay scene directly still uses LevelManager's own startLevel).</summary>
        public static int TakeRequestedLevel(int fallback)
        {
            if (!hasRequest) return fallback;
            hasRequest = false;
            return requestedLevel;
        }
    }
}
