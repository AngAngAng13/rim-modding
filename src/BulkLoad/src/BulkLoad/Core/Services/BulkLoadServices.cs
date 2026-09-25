namespace BulkLoad
{
    internal static class BulkLoadServices
    {
        public static ILoadingCoordinator Coordinator { get; set; }
        public static ILoadingPlanner Planner { get; set; }
        public static IPickupPolicy Pickups { get; set; }
        public static ILoadingOfferGate OfferGate { get; set; }

        private static bool initialized;

        public static void EnsureInitialized()
        {
            if (initialized)
                return;

            var ledger = new ClaimLedger();
            var registry = new ActiveJobRegistry();
            var turns = new TurnTaker();
            Coordinator = new LoadingCoordinator(ledger, registry, turns);
            Planner = new LoadingPlanner(ledger);
            Pickups = new PickupPolicy();
            OfferGate = new LoadingOfferGate(Coordinator, Planner);
            initialized = true;
        }
    }
}
