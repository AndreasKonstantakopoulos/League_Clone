using System.Collections.Generic;
using UnityEngine;

// Fog of war rules: a team sees an enemy unit only while one of its own living units is close enough.
// Own units and all buildings are always shown; invisible champions are never seen by the other team.
// shortcut: vision is a plain circle, so walls do not block it. Add line of sight if that matters later.
public static class Vision
{
    // enemy units each team can see right now (index = Team.Blue / Team.Red), rebuilt ~10 times a second
    static readonly HashSet<Unit>[] seen = { new HashSet<Unit>(), new HashSet<Unit>() };

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

    public static bool Sees(Team viewer, Unit u)
    {
        if (u.Team == viewer || u.IsStructure) return true;
        if (u.Stealthed || viewer == Team.Neutral) return false;
        return seen[(int)viewer].Contains(u);
    }

    // For effects, which have a place but no unit.
    public static bool SeesPoint(Team viewer, Vector3 p)
    {
        foreach (var s in Unit.All)
            if (s.Alive && s.Team == viewer && Unit.FlatDist(s.transform.position, p) <= RadiusOf(s)) return true;
        return false;
    }

    public static void Refresh()
    {
        for (int t = 0; t < 2; t++)
        {
            var viewer = (Team)t;
            var set = seen[t];
            set.Clear();
            foreach (var target in Unit.All)
            {
                if (!target.Alive || target.IsStructure || target.Team == viewer) continue;
                foreach (var s in Unit.All)
                    if (s.Alive && s.Team == viewer && Unit.FlatDist(s.transform.position, target.transform.position) <= RadiusOf(s))
                    {
                        set.Add(target);
                        break;
                    }
            }
        }
    }
}
