using UnityEngine;

// Fires a homing bolt at an enemy in range. Minions come first. A champion is only shot when no minion is in range,
// and it then stays on that champion until it leaves range (or dies), after which it goes back to minions.
// Used by every tower (inner ones too, even while they cannot be attacked) and by the Nexus, which hits harder.
// Towers and the Nexus also see invisible champions in range (see Vision.Refresh), so they can shoot them.
public class Tower : MonoBehaviour
{
    float range = 10f, damage = 110f, interval = 1.1f;
    Unit u, locked;
    float next;

    public void Configure(float newRange, float newDamage, float newInterval)
    {
        range = newRange;
        damage = newDamage;
        interval = newInterval;
    }

    void Start() { u = GetComponent<Unit>(); }

    bool InRange(Unit t) => t != null && t.Alive && u.DistTo(t.transform.position) - t.Radius <= range;

    void Update()
    {
        if (u == null || !u.Alive || GameManager.I == null || GameManager.I.State != MatchState.Playing) return;
        if (Time.time < next) return;
        if (!InRange(locked)) locked = null;
        var t = locked != null ? locked : Unit.BestEnemy(u, range, 1000f, 0f); // huge champion penalty = minions win while any are near
        if (t == null) return;
        if (t.Kind == UnitKind.Champion) locked = t;
        next = Time.time + interval;
        Vfx.TowerShot(u, t, damage); // damage lands when the bolt arrives
    }
}
