using System.Threading;
using HarmonyLib;

namespace RimModUtilities.Pawns
{
    internal static class PatchBootstrap
    {
        private static int initialized;

        internal static void Initialize()
        {
            if (Interlocked.Exchange(ref initialized, 1) != 0)
                return;

            new Harmony("pineapplelemonade67.rimmodutilities").PatchAll();
        }
    }
}
