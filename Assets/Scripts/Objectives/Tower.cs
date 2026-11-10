using UnityEngine;

// Fires a homing bolt every 1.1 s. Minions come first. A champion is only shot when no minion is in range,
// and the tower then stays on that champion until it leaves range (or dies), after which it goes back to minions.
public class Tower : MonoBehaviour
{
    const float Range = 10f, Damage = 110f, Interval = 1.1f;
    Unit u, locked;
    float next;

    void Start() { u = GetComponent<Unit>(); }

    bool InRange(Unit t) => t != null && t.Alive && u.DistTo(t.transform.position) - t.Radius <= Range;

    void Update()
    {
        if (u == null || !u.Alive || GameManager.I == null || GameManager.I.State != MatchState.Playing) return;
        if (Time.time < next) return;
        if (!InRange(locked)) locked = null;
        var t = locked != null ? locked : Unit.BestEnemy(u, Range, 1000f, 0f); // huge champion penalty = minions win while any are near
        if (t == null) return;
        if (t.Kind == UnitKind.Champion) locked = t;
        next = Time.time + Interval;
        Vfx.TowerShot(u, t, Damage); // damage lands when the bolt arrives
    }
}
