using System.Collections.Generic;

public readonly struct RoomRoute
{
    public readonly RoomConnection Entry, Exit;
    public RoomRoute(RoomConnection entry, RoomConnection exit) { Entry = entry; Exit = exit; }
}

// Checks only connection compatibility. Geometry and safe layouts remain authoring responsibilities.
public static class RoomRoutePlanner
{
    public static bool CanComplete(RoomConnection entry, int count, IReadOnlyList<RoomRoute> routes)
    {
        return CanComplete(entry, count, routes, new Dictionary<long, bool>());
    }
    public static bool CanReach(RoomConnection entry, int intermediateCount, RoomConnection finalEntry, IReadOnlyList<RoomRoute> routes)
    {
        return CanReach(entry, intermediateCount, finalEntry, routes, new Dictionary<long, bool>());
    }
    // Return the shortest usable length at or above the requested length.
    // With two connection types, an attainable length repeats with period at most two.
    public static bool TryReachAtLeast(RoomConnection entry, int minimumCount, RoomConnection finalEntry, IReadOnlyList<RoomRoute> routes, out int count)
    {
        minimumCount = System.Math.Max(0, minimumCount);
        for (int extra = 0; extra <= 2; extra++)
        {
            int candidate = minimumCount + extra;
            if (CanReach(entry, candidate, finalEntry, routes)) { count = candidate; return true; }
        }
        count = 0;
        return false;
    }
    static bool CanReach(RoomConnection entry, int count, RoomConnection finalEntry, IReadOnlyList<RoomRoute> routes, Dictionary<long, bool> memo)
    {
        if (count <= 0) return entry == finalEntry;
        long key = ((long)count << 8) | (uint)entry;
        if (memo.TryGetValue(key, out bool known)) return known;
        if (routes != null)
            foreach (var route in routes)
                if (route.Entry == entry && CanReach(route.Exit, count - 1, finalEntry, routes, memo)) return memo[key] = true;
        return memo[key] = false;
    }
    static bool CanComplete(RoomConnection entry, int count, IReadOnlyList<RoomRoute> routes, Dictionary<long, bool> memo)
    {
        if (count <= 0) return true;
        long key = ((long)count << 8) | (uint)entry;
        if (memo.TryGetValue(key, out bool known)) return known;
        if (routes != null)
            foreach (var route in routes)
                if (route.Entry == entry && CanComplete(route.Exit, count - 1, routes, memo)) return memo[key] = true;
        return memo[key] = false;
    }
}
