using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum UnitKind { Champion, Minion, Tower, Nexus, Monster }

// Anything that has health and can be hit: champions, minions, towers, the Nexus.
public class Unit : MonoBehaviour
{
    public static readonly List<Unit> All = new List<Unit>();
    public static event Action<Unit, Unit> AnyDied; // (victim, killer)

    public Team Team;
    public UnitKind Kind;
    public Stats Base = new Stats();   // before temporary buffs
    public Stats Total = new Stats();  // base + active buffs
    public float Health, Mana, Shield, Radius = 0.5f;
    public float GoldBounty, XpBounty;
    public float LastDamaged = -999f;
    public Unit LastAttacker; // who hit us last (jungle monsters use it to pick a target)
    public readonly List<Unit> Requires = new List<Unit>(); // all must be dead before this can be damaged
    public NavMeshAgent Agent;

    struct Buff { public Stats S; public float Until; }
    readonly List<Buff> buffs = new List<Buff>();
    float stunUntil, rootUntil, silenceUntil, slowUntil, slowAmt, shieldUntil;

    public bool Alive => Health > 0f;
    public bool IsStructure => Kind == UnitKind.Tower || Kind == UnitKind.Nexus;
    public bool Stunned => Time.time < stunUntil;
    public bool Rooted => Time.time < rootUntil;
    public bool Silenced => Time.time < silenceUntil;
    public bool CanMove => Alive && !Stunned && !Rooted;
    public bool CanCast => Alive && !Stunned && !Silenced;
    public bool Invulnerable => Requires.Exists(u => u != null && u.Alive);

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public void Init(Team team, UnitKind kind, Stats stats, float radius)
    {
        Team = team; Kind = kind; Base = stats; Radius = radius;
        Agent = GetComponent<NavMeshAgent>();
        Recalc();
        Health = Total.Health;
        Mana = Total.Mana;
    }

    public void Recalc()
    {
        var t = Base;
        foreach (var b in buffs) t = t + b.S;
        Total = t;
    }

    void Update()
    {
        if (!Alive) return;
        float dt = Time.deltaTime;
        if (buffs.RemoveAll(b => Time.time >= b.Until) > 0) Recalc();
        if (Shield > 0f && Time.time >= shieldUntil) Shield = 0f;
        Health = Mathf.Min(Total.Health, Health + Total.HealthRegen * dt);
        Mana = Mathf.Min(Total.Mana, Mana + Total.ManaRegen * dt);
        if (AgentOk)
        {
            Agent.speed = Total.MoveSpeed * (Time.time < slowUntil ? 1f - slowAmt : 1f);
            Agent.isStopped = !CanMove;
        }
    }

    bool AgentOk => Agent != null && Agent.enabled && Agent.isOnNavMesh;

    public bool GoTo(Vector3 p)
    {
        if (!CanMove || !AgentOk) return false;
        Agent.isStopped = false;
        Agent.SetDestination(p);
        return true;
    }

    public void Halt() { if (AgentOk) Agent.ResetPath(); }

    // ---- damage and effects ----
    public void TakeDamage(float amount, DamageType type, Unit src)
    {
        if (!Alive || amount <= 0f || Invulnerable) return;
        LastDamaged = Time.time; // interrupts recall
        if (src != null) LastAttacker = src;
        if (type != DamageType.True)
            amount *= 100f / (100f + Mathf.Max(0f, type == DamageType.Physical ? Total.Armor : Total.MR));
        if (Shield > 0f)
        {
            float absorbed = Mathf.Min(Shield, amount);
            Shield -= absorbed;
            amount -= absorbed;
        }
        Health -= amount;
        if (Health <= 0f) { Health = 0f; Die(src); }
    }

    void Die(Unit killer)
    {
        AnyDied?.Invoke(this, killer);
        if (Kind != UnitKind.Champion) Destroy(gameObject); // champions respawn instead
    }

    public void Heal(float a) { if (Alive && !IsStructure) Health = Mathf.Min(Total.Health, Health + a); }

    public void AddShield(float a, float dur)
    {
        if (!Alive || IsStructure) return;
        Shield += a;
        shieldUntil = Mathf.Max(shieldUntil, Time.time + dur);
    }

    public void Stun(float d) { if (Alive && !IsStructure) stunUntil = Mathf.Max(stunUntil, Time.time + d); }
    public void Root(float d) { if (Alive && !IsStructure) rootUntil = Mathf.Max(rootUntil, Time.time + d); }
    public void Silence(float d) { if (Alive && !IsStructure) silenceUntil = Mathf.Max(silenceUntil, Time.time + d); }

    public void Slow(float frac, float d)
    {
        if (!Alive || IsStructure) return;
        slowAmt = Mathf.Clamp(frac, 0f, 0.9f);
        slowUntil = Time.time + d;
    }

    public void AddBuff(Stats s, float dur)
    {
        if (!Alive || IsStructure) return;
        buffs.Add(new Buff { S = s, Until = Time.time + dur });
        Recalc();
    }

    // Slide along the navmesh (dashes, knockbacks, pulls).
    public void Displace(Vector3 dir, float dist, float dur)
    {
        dir.y = 0f;
        if (!Alive || IsStructure || !AgentOk || dist <= 0f || dir.sqrMagnitude < 0.0001f) return;
        StartCoroutine(DisplaceRoutine(dir.normalized * dist, dur));
    }

    IEnumerator DisplaceRoutine(Vector3 delta, float dur)
    {
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            if (!Alive || !AgentOk) yield break;
            Agent.Move(delta * (Time.deltaTime / dur));
            yield return null;
        }
    }

    // ---- queries (flat XZ distance) ----
    public static float FlatDist(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
    public float DistTo(Vector3 p) => FlatDist(transform.position, p);

    // Best enemy in range: lowest score wins (distance plus penalties, so minions are preferred over champions).
    public static Unit BestEnemy(Unit self, float range, float champPenalty, float structPenalty)
    {
        Unit best = null;
        float bestScore = float.MaxValue;
        foreach (var u in All)
        {
            if (u == self || !u.Alive || u.Team == self.Team || u.Invulnerable) continue;
            float d = self.DistTo(u.transform.position) - u.Radius;
            if (d > range) continue;
            float s = d + (u.Kind == UnitKind.Champion ? champPenalty : 0f) + (u.IsStructure ? structPenalty : 0f);
            if (s < bestScore) { bestScore = s; best = u; }
        }
        return best;
    }

    public static Unit NearestTo(Vector3 p, float maxDist, Func<Unit, bool> filter)
    {
        Unit best = null;
        float bestD = maxDist;
        foreach (var u in All)
        {
            if (!u.Alive || !filter(u)) continue;
            float d = FlatDist(p, u.transform.position);
            if (d <= bestD) { bestD = d; best = u; }
        }
        return best;
    }

    public static void InRadius(Vector3 p, float r, List<Unit> output)
    {
        foreach (var u in All)
            if (u.Alive && FlatDist(p, u.transform.position) - u.Radius <= r) output.Add(u);
    }
}
