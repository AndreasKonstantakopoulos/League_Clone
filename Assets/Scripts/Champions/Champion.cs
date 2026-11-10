using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// A player-controlled champion: movement, auto attacks, levelling, abilities, gold and items.
public class Champion : MonoBehaviour
{
    public static Champion Local;
    public static readonly List<Champion> All = new List<Champion>();
    public const int MaxLevel = 18, MaxItems = 6;

    public ChampionDef Def;
    public Unit U;
    public int Level = 1, Xp, SkillPoints = 1;
    public float Gold = 500f;
    public readonly int[] Ranks = new int[4];
    public readonly float[] CdEnd = new float[4];
    public readonly List<ItemDef> Items = new List<ItemDef>();
    public bool Dead;
    public float RespawnAt;

    Unit target;
    bool moveOrder;
    float nextAttack;
    Vector3 spawnPos;
    Renderer[] renders;

    public const float BaseRadius = 14f, RecallTime = 6f;
    public bool Recalling;
    float recallStart, nextRecallFx;

    public int XpToNext => 100 + 40 * Level;
    // Shopping and the healing fountain only work inside your own base.
    public bool AtBase => !Dead && U.DistTo(GameManager.I.SpawnOf(U.Team)) < BaseRadius;
    public float RecallProgress => Recalling ? Mathf.Clamp01((Time.time - recallStart) / RecallTime) : 0f;

    public void Init(ChampionDef def, Team team, Vector3 spawn, bool local)
    {
        Def = def;
        spawnPos = spawn;
        var col = GetComponent<Collider>();
        if (col != null) Destroy(col);
        transform.position = spawn;
        var agent = gameObject.AddComponent<NavMeshAgent>();
        agent.baseOffset = 1f; // capsule pivot is its centre
        agent.acceleration = 40f;
        agent.angularSpeed = 600f;
        agent.stoppingDistance = 0.1f;
        U = gameObject.AddComponent<Unit>();
        U.Init(team, UnitKind.Champion, def.Base, 0.5f);
        U.GoldBounty = 300f;
        U.XpBounty = 150f;
        renders = GetComponentsInChildren<Renderer>();
        GetComponent<Renderer>().material.color = def.Color;
        RecalcStats();
        All.Add(this);
        if (local) Local = this;
    }

    void OnDestroy()
    {
        All.Remove(this);
        if (Local == this) Local = null;
    }

    // base + growth per level + items; keeps the missing-health gap when max health changes
    void RecalcStats()
    {
        float oldHp = U.Total.Health, oldMana = U.Total.Mana;
        var s = Def.Base + Def.Growth.Scaled(Level - 1);
        foreach (var it in Items) s = s + it.Bonus;
        U.Base = s;
        U.Recalc();
        U.Health += U.Total.Health - oldHp;
        U.Mana += U.Total.Mana - oldMana;
    }

    void Update()
    {
        if (GameManager.I == null || GameManager.I.State != MatchState.Playing) return;
        Gold += Rules.PassiveGold * Time.deltaTime;
        if (Dead)
        {
            if (Time.time >= RespawnAt) Respawn();
            return;
        }
        if (!U.Alive) return;
        if (AtBase) // fountain: fast regeneration at home
        {
            U.Heal(U.Total.Health * 0.1f * Time.deltaTime);
            U.Mana = Mathf.Min(U.Total.Mana, U.Mana + U.Total.Mana * 0.1f * Time.deltaTime);
        }
        if (this == Local) HandleInput();
        UpdateRecall();
        if (!Recalling) UpdateAttack();
    }

    // B channels for RecallTime seconds, then teleports home. Moving, casting, taking damage or being stunned cancels it.
    void StartRecall()
    {
        if (Recalling || AtBase || !U.CanCast) return;
        Recalling = true;
        recallStart = Time.time;
        target = null;
        moveOrder = false;
        U.Halt();
    }

    void UpdateRecall()
    {
        if (!Recalling) return;
        if (U.Stunned || U.LastDamaged > recallStart) { Recalling = false; return; }
        if (Time.time >= recallStart + RecallTime)
        {
            Recalling = false;
            Vfx.Ring(transform.position, 1.5f, new Color(0.6f, 0.85f, 1f));
            U.Agent.Warp(spawnPos);
            Vfx.Ring(transform.position, 1.5f, new Color(0.6f, 0.85f, 1f));
            return;
        }
        if (Time.time >= nextRecallFx)
        {
            nextRecallFx = Time.time + 0.4f;
            Vfx.Ring(transform.position, 1.2f, new Color(0.6f, 0.85f, 1f));
        }
    }

    // ---- input ----
    void HandleInput()
    {
        if (GameInput.KeyDown('B')) StartRecall();
        if (GameInput.RightClickDown)
        {
            Recalling = false;
            var p = GameInput.MouseGround();
            var e = Unit.NearestTo(p, 2f, u => u.Team != U.Team && !u.Invulnerable);
            if (e != null) { target = e; moveOrder = false; }
            else { target = null; MoveTo(p); }
        }
        for (int i = 0; i < 4; i++)
            if (GameInput.KeyDown("QWER"[i])) TryCast(i, GameInput.MouseGround());
    }

    void MoveTo(Vector3 p)
    {
        if (NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas) && U.GoTo(hit.position))
            moveOrder = true;
    }

    // ---- auto attacks ----
    void UpdateAttack()
    {
        if (U.Stunned) return;
        if (target != null && (!target.Alive || target.Invulnerable)) target = null;
        if (target == null && !moveOrder) target = Unit.BestEnemy(U, Def.AttackRange, 6f, 3f); // idle: pick a fight
        if (target == null)
        {
            if (moveOrder && U.Agent.enabled && !U.Agent.pathPending && U.Agent.remainingDistance <= U.Agent.stoppingDistance + 0.2f)
                moveOrder = false;
            return;
        }
        float gap = U.DistTo(target.transform.position) - target.Radius;
        if (gap > Def.AttackRange) { U.GoTo(target.transform.position); return; }
        U.Halt();
        FaceFlat(target.transform.position);
        if (Time.time >= nextAttack)
        {
            nextAttack = Time.time + 1f / Mathf.Max(0.1f, U.Total.AttackSpeed);
            target.TakeDamage(U.Total.AD, DamageType.Physical, U); // shortcut: ranged attacks hit instantly, no projectile
            Vfx.Hit(target.transform.position + Vector3.up);
        }
    }

    void FaceFlat(Vector3 p)
    {
        p.y = transform.position.y;
        if ((p - transform.position).sqrMagnitude > 0.01f) transform.LookAt(p);
    }

    // ---- abilities ----
    public bool TryCast(int slot, Vector3 aim)
    {
        var a = Def.Abilities[slot];
        int rank = Ranks[slot];
        if (rank == 0 || Time.time < CdEnd[slot] || !U.CanCast || U.Mana < a.Mana) return false;

        Unit tgt = null;
        if (a.Shape == Shape.TargetEnemy)
        {
            tgt = Unit.NearestTo(aim, 3f, u => u.Team != U.Team && !u.Invulnerable && U.DistTo(u.transform.position) <= a.Range);
            if (tgt == null) return false;
        }
        else if (a.Shape == Shape.TargetAlly)
        {
            tgt = Unit.NearestTo(aim, 3f, u => u.Team == U.Team && !u.IsStructure && U.DistTo(u.transform.position) <= a.Range);
            if (tgt == null) tgt = U;
        }

        Vector3 pos = transform.position;
        Vector3 point = tgt != null ? tgt.transform.position : aim;
        Vector3 dir = point - pos;
        dir.y = 0f;
        float dist = dir.magnitude;
        dir = dist > 0.01f ? dir / dist : transform.forward;
        if (a.Shape == Shape.PointArea && dist > a.Range) { point = pos + dir * a.Range; dist = a.Range; }

        Recalling = false;
        U.Mana -= a.Mana;
        CdEnd[slot] = Time.time + a.CooldownAt(rank, U.Total.CDR);
        FaceFlat(point);

        var hit = new List<Unit>();
        switch (a.Shape)
        {
            case Shape.Self: hit.Add(U); break;
            case Shape.SelfArea: Unit.InRadius(pos, a.Radius, hit); break;
            case Shape.TargetEnemy:
            case Shape.TargetAlly: hit.Add(tgt); break;
            case Shape.PointArea: Unit.InRadius(point, a.Radius, hit); break;
            case Shape.Skillshot: CollectLine(pos, dir, a, hit); break;
        }

        foreach (var e in a.Effects)
        {
            float amt = e.Amount(U, rank);
            if (e.Type == EffectType.Dash)
            {
                // never dash past the aim point; stop short of a targeted enemy
                U.Displace(dir, Mathf.Min(amt, dist - (a.Shape == Shape.TargetEnemy ? 1.2f : 0f)), 0.18f);
                continue;
            }
            if (e.Who == Affects.Caster) { Apply(e, amt, U); continue; }
            foreach (var t in hit)
                if (e.Who == Affects.Enemies ? t.Team != U.Team : t.Team == U.Team) Apply(e, amt, t);
        }

        // particles: area ring / skillshot trail where it was cast, then a burst on everything it touched
        var col = Vfx.ColorOf(a);
        switch (a.Shape)
        {
            case Shape.Self: Vfx.Ring(pos, 1.5f, col); break;
            case Shape.SelfArea: Vfx.Ring(pos, a.Radius, col); break;
            case Shape.PointArea: Vfx.Ring(point, a.Radius, col); break;
            case Shape.Skillshot: Vfx.Trail(pos, dir, a.Range, col); break;
        }
        foreach (var t in hit)
            if (t != U) Vfx.Impact(t.transform.position + Vector3.up, col);
        return true;
    }

    // Units along a line from the caster. Non-piercing shots stop at the first enemy.
    void CollectLine(Vector3 pos, Vector3 dir, AbilityDef a, List<Unit> hit)
    {
        var found = new List<(float t, Unit u)>();
        foreach (var u in Unit.All)
        {
            if (!u.Alive || u == U) continue;
            Vector3 d = u.transform.position - pos;
            d.y = 0f;
            float t = Vector3.Dot(d, dir);
            if (t < 0f || t > a.Range + u.Radius) continue;
            if ((d - dir * t).magnitude > a.Radius + u.Radius) continue;
            found.Add((t, u));
        }
        found.Sort((x, y) => x.t.CompareTo(y.t));
        foreach (var f in found)
        {
            if (a.Pierce) { hit.Add(f.u); continue; }
            if (f.u.Team == U.Team || f.u.Invulnerable) continue;
            hit.Add(f.u);
            return;
        }
    }

    void Apply(Effect e, float amt, Unit t)
    {
        switch (e.Type)
        {
            case EffectType.Damage: t.TakeDamage(amt, e.DType, U); break;
            case EffectType.Heal: t.Heal(amt); break;
            case EffectType.Shield: t.AddShield(amt, e.Duration); break;
            case EffectType.Stun: t.Stun(e.Duration); break;
            case EffectType.Root: t.Root(e.Duration); break;
            case EffectType.Silence: t.Silence(e.Duration); break;
            case EffectType.Slow: t.Slow(amt, e.Duration); break;
            case EffectType.Knockback: t.Displace(t.transform.position - transform.position, amt, 0.2f); break;
            case EffectType.Pull: t.Displace(transform.position - t.transform.position, Mathf.Min(amt, t.DistTo(transform.position) - 1f), 0.2f); break;
            case EffectType.Buff: t.AddBuff(e.Buff, e.Duration); break;
        }
    }

    // R unlocks at levels 6/11/16; other abilities every second level (1,3,5,7,9).
    public bool CanRank(int slot)
    {
        if (SkillPoints <= 0 || Ranks[slot] >= Def.Abilities[slot].MaxRank) return false;
        int need = slot == 3 ? 6 + 5 * Ranks[slot] : 1 + 2 * Ranks[slot];
        return Level >= need;
    }

    public void Rank(int slot)
    {
        if (!CanRank(slot)) return;
        Ranks[slot]++;
        SkillPoints--;
    }

    // ---- levels, gold, items ----
    public void GainXp(int amt)
    {
        if (Level >= MaxLevel) return;
        Xp += amt;
        while (Level < MaxLevel && Xp >= XpToNext)
        {
            Xp -= XpToNext;
            Level++;
            SkillPoints++;
            RecalcStats();
        }
    }

    // Price after crediting any components already owned.
    public int PriceOf(ItemDef it)
    {
        int price = it.Cost;
        var pool = new List<ItemDef>(Items);
        foreach (var c in it.Components)
        {
            var own = pool.Find(x => x.Name == c);
            if (own == null) continue;
            price -= own.Cost;
            pool.Remove(own);
        }
        return price;
    }

    public bool Buy(ItemDef it)
    {
        if (!AtBase) return false; // enforced here, not just in the UI
        int price = PriceOf(it);
        var after = new List<ItemDef>(Items);
        foreach (var c in it.Components)
        {
            var own = after.Find(x => x.Name == c);
            if (own != null) after.Remove(own);
        }
        if (Gold < price || after.Count >= MaxItems) return false;
        Gold -= price;
        after.Add(it);
        Items.Clear();
        Items.AddRange(after);
        RecalcStats();
        return true;
    }

    public void Sell(int index)
    {
        if (!AtBase || index < 0 || index >= Items.Count) return;
        Gold += Items[index].Cost * 0.7f;
        Items.RemoveAt(index);
        RecalcStats();
    }

    // ---- death and respawn ----
    public void OnDeath()
    {
        Dead = true;
        Recalling = false;
        target = null;
        moveOrder = false;
        RespawnAt = Time.time + 4f + 2f * Level;
        foreach (var r in renders) r.enabled = false;
        U.Agent.enabled = false;
    }

    void Respawn()
    {
        Dead = false;
        U.Agent.enabled = true;
        U.Agent.Warp(spawnPos);
        U.Health = U.Total.Health;
        U.Mana = U.Total.Mana;
        U.Shield = 0f;
        foreach (var r in renders) r.enabled = true;
    }
}
