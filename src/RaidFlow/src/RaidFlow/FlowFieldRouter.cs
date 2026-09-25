using Verse;

namespace RaidFlow
{
    public static class FlowFieldRouter
    {
        public static bool TryResolve(PathRequest request)
        {
            return request != null && FlowFieldCache.TryResolve(request);
        }
    }
}
