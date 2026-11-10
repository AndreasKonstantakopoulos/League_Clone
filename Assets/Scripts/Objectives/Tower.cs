using UnityEngine;

// Shoots the best enemy in range every 1.1 s, preferring minions over champions.
public class Tower : MonoBehaviour
{
    const float Range = 10f, Damage = 110f, Interval = 1.1f;
    Unit u;
    float next;

    void Start() { u = GetComponent<Unit>(); }

    void Update()
    {
        if (u == null || !u.Alive || GameManager.I == null || GameManager.I.State != MatchState.Playing) return;
        if (Time.time < next) return;
        var t = Unit.BestEnemy(u, Range, 6f, 0f);
        if (t == null) return;
        next = Time.time + Interval;
        t.TakeDamage(Damage, DamageType.Physical, u);
    }
}
