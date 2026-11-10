using System.Collections.Generic;
using UnityEngine;

// Neutral monster. Stays at its spawn until a champion comes close or something hits it,
// chases within its leash, and walks home and heals if the target escapes.
public class Monster : MonoBehaviour
{
    MonsterDef d;
    Unit u, target;
    Vector3 home;
    float next, nextSlam;

    public void Init(MonsterDef def, Vector3 homePos)
    {
        d = def;
        home = homePos;
        u = GetComponent<Unit>();
    }

    bool InLeash(Unit t) => t != null && t.Alive && Unit.FlatDist(home, t.transform.position) <= d.Leash;

    void Update()
    {
        if (GameManager.I == null || GameManager.I.State != MatchState.Playing || !u.Alive || u.Stunned) return;

        if (!InLeash(target)) target = null; // dead, gone, or dragged too far from home
        if (target == null)
        {
            if (Time.time - u.LastDamaged < 4f && InLeash(u.LastAttacker)) target = u.LastAttacker;
            else
            {
                var c = Unit.NearestTo(transform.position, d.Aggro, x => x.Kind == UnitKind.Champion);
                if (InLeash(c)) target = c;
            }
        }

        if (target == null) // back to the spawn point, healing fast
        {
            u.GoTo(home);
            u.Heal(u.Total.Health * 0.25f * Time.deltaTime);
            return;
        }

        float gap = u.DistTo(target.transform.position) - target.Radius;
        if (d.SlamRadius > 0f && gap <= d.SlamRadius && Time.time >= nextSlam) Slam();
        if (gap > d.Range) { u.GoTo(target.transform.position); return; }

        u.Halt();
        var look = target.transform.position;
        look.y = transform.position.y;
        transform.LookAt(look);
        if (Time.time < next) return;
        next = Time.time + d.Interval;
        target.TakeDamage(u.Total.AD, DamageType.Physical, u);
        Vfx.Auto(u, Vfx.TeamColor(Team.Neutral), d.Range < 3f, target);
    }

    // Big monsters periodically stomp everything around them.
    void Slam()
    {
        nextSlam = Time.time + 5f;
        var hit = new List<Unit>();
        Unit.InRadius(transform.position, d.SlamRadius, hit);
        foreach (var t in hit)
        {
            if (t.Team == Team.Neutral) continue;
            t.TakeDamage(d.SlamDamage, DamageType.Magic, u);
            t.Slow(0.3f, 1.5f);
        }
        Vfx.MonsterSlam(transform.position, d.SlamRadius, d.Color, 0.7f);
    }
}
