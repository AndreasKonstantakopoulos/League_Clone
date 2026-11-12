using System.Collections.Generic;
using UnityEngine;

// Walks its lane, fights the best enemy in aggro range, otherwise keeps marching to the enemy Nexus.
public class Minion : MonoBehaviour
{
    List<Vector3> path;
    Vector3 goal;
    Unit u;
    int wp;
    float next, range;

    public void Init(List<Vector3> lanePath, Vector3 enemyNexus, float attackRange)
    {
        path = lanePath;
        goal = enemyNexus;
        range = attackRange;
        u = GetComponent<Unit>();
    }

    void Update()
    {
        if (GameManager.I == null || GameManager.I.State != MatchState.Playing || !u.Alive || u.Stunned) return;

        var t = Unit.BestEnemy(u, 8f, 4f, 6f); // prefers minions, then champions, then structures
        if (t != null)
        {
            float gap = u.DistTo(t.transform.position) - t.Radius;
            if (gap > range) { u.GoTo(t.transform.position); return; }
            u.Halt();
            var look = t.transform.position;
            look.y = transform.position.y;
            transform.LookAt(look);
            if (Time.time >= next)
            {
                next = Time.time + 1f;
                t.TakeDamage(u.Total.AD, DamageType.Physical, u);
                Vfx.Auto(u, Vfx.TeamColor(u.Team), range < 3f, t); // team-coloured slash (melee) or bolt (caster)
            }
            return;
        }

        while (wp < path.Count && u.DistTo(path[wp]) < 2f) wp++;
        u.GoTo(wp < path.Count ? path[wp] : goal);
    }
}
