using System.Collections.Generic;
using UnityEngine;

// Fog of war rules: a team sees an enemy unit only while one of its own living units can see it.
// Only your own side (champions, minions, towers, Nexus) gives vision, and your own side is always shown.
// Everything else, enemy buildings included, is hidden until one of your units gets a clear view; invisible champions are never seen.
// Jungle walls block the view. Standing close to a wall you can see a few metres past it (the walls are thin),
// but not deep behind it, so the middle of the jungle stays dark until you walk in.
public static class Vision
{
    // Tunable: how close to a wall you must be, and how far past it you can see.
    const float NearWall = 3.5f, PeekBeyond = 4f;

    // enemy units each team can see right now (index = Team.Blue / Team.Red), rebuilt ~10 times a second
    static readonly HashSet<Unit>[] seen = { new HashSet<Unit>(), new HashSet<Unit>() };
    // invisible enemy champions standing in range of that team's towers or Nexus (they detect stealth, even inner towers that cannot be attacked yet)
    static readonly HashSet<Unit>[] revealed = { new HashSet<Unit>(), new HashSet<Unit>() };
    static readonly List<Vector4> walls = new List<Vector4>(); // flat rectangles: x = minX, y = minZ, z = maxX, w = maxZ

    public static void SetWalls(IEnumerable<Bounds> bounds)
    {
        walls.Clear();
        foreach (var b in bounds) walls.Add(new Vector4(b.min.x, b.min.z, b.max.x, b.max.z));
    }

    public static float RadiusOf(Unit u)
    {
        switch (u.Kind)
        {
            case UnitKind.Champion: return 12f;
            case UnitKind.Minion: return 8f;
            case UnitKind.Tower: return 13f;
            case UnitKind.Nexus: return 11f;
            default: return 0f; // jungle monsters give nobody vision
        }
    }

    // Can a viewer standing at s with vision radius r see the point t?
    public static bool CanSee(Vector3 s, float r, Vector3 t)
    {
        float dx = t.x - s.x, dz = t.z - s.z;
        float dist = Mathf.Sqrt(dx * dx + dz * dz);
        if (dist > r) return false;
        if (walls.Count == 0) return true;
        float hit = FirstWall(s.x, s.z, t.x, t.z);
        if (hit < 0f) return true;                         // nothing in the way
        return hit <= NearWall && dist - hit <= PeekBeyond; // right at the wall: a short peek past it
    }

    // Distance from s to the first wall on the way to t, or -1 if the way is clear (2D slab test against each wall rectangle).
    static float FirstWall(float sx, float sz, float tx, float tz)
    {
        float dx = tx - sx, dz = tz - sz, best = float.MaxValue;
        foreach (var w in walls)
        {
            float t0 = 0f, t1 = 1f;
            if (!Slab(sx, dx, w.x, w.z, ref t0, ref t1) || !Slab(sz, dz, w.y, w.w, ref t0, ref t1)) continue;
            if (t0 < best) best = t0;
        }
        return best == float.MaxValue ? -1f : best * Mathf.Sqrt(dx * dx + dz * dz);
    }

    static bool Slab(float s, float d, float lo, float hi, ref float t0, ref float t1)
    {
        if (Mathf.Abs(d) < 1e-6f) return s >= lo && s <= hi;
        float a = (lo - s) / d, b = (hi - s) / d;
        if (a > b) { float tmp = a; a = b; b = tmp; }
        t0 = Mathf.Max(t0, a);
        t1 = Mathf.Min(t1, b);
        return t0 <= t1;
    }

    public static bool Sees(Team viewer, Unit u)
    {
        if (u.Team == viewer) return true;
        if (viewer == Team.Neutral) return false;
        if (u.Stealthed) return revealed[(int)viewer].Contains(u); // invisible: only if a tower or Nexus detects it
        return seen[(int)viewer].Contains(u);
    }

    // Normal units are targetable when picked; invisible ones only while revealed.
    public static bool Targetable(Team viewer, Unit u) { return !u.Stealthed || Sees(viewer, u); }

    // What the host may tell a team about: enemy buildings are always reported (a destroyed one must disappear for everyone,
    // and where they stand is no secret), but the client still draws them only while they are in vision.
    public static bool Reports(Team viewer, Unit u) { return u.IsStructure || Sees(viewer, u); }

    // For effects, which have a place but no unit.
    public static bool SeesPoint(Team viewer, Vector3 p)
    {
        foreach (var s in Unit.All)
            if (s.Alive && s.Team == viewer && CanSee(s.transform.position, RadiusOf(s), p)) return true;
        return false;
    }

    public static void Refresh()
    {
        for (int t = 0; t < 2; t++)
        {
            var viewer = (Team)t;
            var set = seen[t];
            set.Clear();
            revealed[t].Clear();
            foreach (var target in Unit.All)
            {
                if (!target.Alive || target.Team == viewer) continue;
                if (target.Stealthed)
                {
                    foreach (var s in Unit.All)
                        if (s.Alive && s.IsStructure && s.Team == viewer && Unit.FlatDist(s.transform.position, target.transform.position) <= RadiusOf(s))
                        {
                            revealed[t].Add(target);
                            break;
                        }
                    continue;
                }
                foreach (var s in Unit.All)
                    if (s.Alive && s.Team == viewer && CanSee(s.transform.position, RadiusOf(s), target.transform.position))
                    {
                        set.Add(target);
                        break;
                    }
            }
        }
    }
}
