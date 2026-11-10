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
    public bool Mirror;      // client-side copy: shows what the host reports and sends commands instead of acting
    public ulong OwnerId;    // which connected player controls this champion
    public int PassiveStacks; // for the HUD: stacks, or 0/1 for passives that are simply on or off

    float lastAction, passiveTimer, lastHit;   // lastAction: last attack or cast (stealth passive); lastHit: last focused attack
    Unit focusTarget;
    bool spellCharged, aegisUsed;
    float frenzyUntil, spellrunUntil;
    Renderer body;
    Material normalMat, ghostMat;
    bool isGhost;

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
        U.Thorns = def.Passive.Kind == PassiveKind.Thorns ? def.Passive.A : 0f;
        U.GoldBounty = 300f;
        U.XpBounty = 150f;
        renders = GetComponentsInChildren<Renderer>();
        GetComponent<Renderer>().material.color = def.Color;
        body = GetComponent<Renderer>();
        normalMat = body.sharedMaterial;
        RecalcStats();
        All.Add(this);
        if (local) Local = this;
    }

    // Client side: wraps a mirrored unit. All numbers arrive from the host (see Net.OnOwn / OnSnap).
    public void InitMirror(ChampionDef def, Unit u, ulong owner, bool local)
    {
        Def = def;
        U = u;
        OwnerId = owner;
        Mirror = true;
        renders = GetComponentsInChildren<Renderer>();
        body = GetComponent<Renderer>();
        normalMat = body.sharedMaterial;
        if (!local) return;
        Local = this;
        var cam = Camera.main;
        if (cam != null && cam.GetComponent<CameraFollow>() == null) cam.gameObject.AddComponent<CameraFollow>().Target = transform;
    }

    public void SetMirrorVisible(bool visible) { foreach (var r in renders) r.enabled = visible; }

    public void ApplyMirror(int level, int xp, int skillPoints, float gold, int[] ranks, float[] cdLeft, List<string> items,
        bool dead, float respawnLeft, float recall, int passiveStacks)
    {
        PassiveStacks = passiveStacks;
        Level = level; Xp = xp; SkillPoints = skillPoints; Gold = gold;
        for (int i = 0; i < 4; i++) { Ranks[i] = ranks[i]; CdEnd[i] = Time.time + cdLeft[i]; }
        Items.Clear();
        foreach (var n in items) { var it = Content.Item(n); if (it != null) Items.Add(it); }
        Dead = dead;
        RespawnAt = Time.time + respawnLeft;
        Recalling = recall >= 0f;
        recallStart = Time.time - Mathf.Clamp01(recall) * RecallTime;
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
        ApplyStealthLook();
        if (Mirror) // clients only forward input; the host does the rest
        {
            if (this == Local && U.Alive) HandleInput();
            return;
        }
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
        TickPassive(Time.deltaTime);
        if (this == Local) HandleInput();
        UpdateRecall();
        if (!Recalling) UpdateAttack();
    }

    // ---- passives (run on the simulating peer; values are shown on the HUD through PassiveStacks) ----
    void NoteAction()
    {
        lastAction = Time.time;
        U.Stealthed = false;
    }

    // Bonus damage fraction for this auto attack: Steady Aim stacks, or Executioner against weak targets.
    float AutoBonus(Unit t)
    {
        var p = Def.Passive;
        if (p.Kind == PassiveKind.Executioner)
        {
            bool low = t.Health / t.Total.Health < p.B;
            PassiveStacks = low ? 1 : 0;
            return low ? p.A : 0f;
        }
        if (p.Kind != PassiveKind.Focus) return 0f;
        if (t != focusTarget || Time.time - lastHit > p.C) PassiveStacks = 0;
        focusTarget = t;
        lastHit = Time.time;
        float bonus = p.A * PassiveStacks;
        PassiveStacks = Mathf.Min(PassiveStacks + 1, (int)p.B);
        return bonus;
    }

    void SetPassiveBonus(Stats s)
    {
        U.PassiveBonus = s;
        U.Recalc();
    }

    void TickPassive(float dt)
    {
        var p = Def.Passive;
        passiveTimer -= dt;
        bool tick = passiveTimer <= 0f; // slower checks run 4 times a second
        if (tick) passiveTimer = 0.25f;
        switch (p.Kind)
        {
            case PassiveKind.Stonebound:
            {
                if (!tick) break;
                int n = 0;
                foreach (var e in Unit.All)
                    if (e.Alive && !e.IsStructure && e.Team != U.Team && U.DistTo(e.transform.position) <= p.B) n++;
                n = Mathf.Min(n, (int)p.C);
                if (n == PassiveStacks) break;
                PassiveStacks = n;
                SetPassiveBonus(Fx.St(armor: p.A * n, mr: p.A * n));
                break;
            }
            case PassiveKind.LastStand:
            {
                if (!tick) break;
                int on = U.Health / U.Total.Health < p.A ? 1 : 0;
                if (on == PassiveStacks) break;
                PassiveStacks = on;
                SetPassiveBonus(on == 1 ? p.Bonus + Fx.St(hpr: p.B * U.Total.Health) : new Stats());
                break;
            }
            case PassiveKind.ShadowVeil:
            {
                // hidden after A quiet seconds; the first attack, cast or hit shows him again
                bool hidden = Time.time - Mathf.Max(lastAction, U.LastDamaged) >= p.A;
                if ((hidden ? 1 : 0) != PassiveStacks)
                {
                    PassiveStacks = hidden ? 1 : 0;
                    SetPassiveBonus(hidden ? Fx.St(ms: p.C) : new Stats());
                }
                U.Stealthed = hidden;
                if (hidden)
                {
                    U.Heal(U.Total.Health * p.B * dt);
                    U.Mana = Mathf.Min(U.Total.Mana, U.Mana + U.Total.Mana * p.B * dt);
                }
                break;
            }
            case PassiveKind.Focus:
                if (Time.time - lastHit > p.C) PassiveStacks = 0;
                break;
            case PassiveKind.Executioner:
                if (Time.time - lastAction > 2f) PassiveStacks = 0;
                break;
            case PassiveKind.Thorns: // the reflection itself is in Unit.TakeDamage
            case PassiveKind.Lifesteal: // applied when she attacks
                PassiveStacks = 1;
                break;
            case PassiveKind.Wellspring:
                U.Mana = Mathf.Min(U.Total.Mana, U.Mana + U.Total.Mana * p.A * dt);
                PassiveStacks = 1;
                break;
            case PassiveKind.Spellrunner:
                PassiveStacks = Time.time < spellrunUntil ? 1 : 0;
                break;
            case PassiveKind.Frenzy:
                if (PassiveStacks > 0 && Time.time > frenzyUntil) { PassiveStacks = 0; SetPassiveBonus(new Stats()); }
                break;
            case PassiveKind.Sprint:
            {
                bool fast = Time.time - Mathf.Max(lastAction, U.LastDamaged) >= p.A;
                if ((fast ? 1 : 0) == PassiveStacks) break;
                PassiveStacks = fast ? 1 : 0;
                SetPassiveBonus(fast ? Fx.St(ms: p.C) : new Stats());
                break;
            }
            case PassiveKind.Aegis: // one shield per quiet spell, refreshed after the next fight
            {
                float quiet = Time.time - Mathf.Max(lastAction, U.LastDamaged);
                if (quiet < 0.5f) aegisUsed = false;
                else if (quiet >= p.A && !aegisUsed) { aegisUsed = true; U.AddShield(U.Total.Health * p.B, 600f); }
                PassiveStacks = U.Shield > 0f ? 1 : 0;
                break;
            }
            case PassiveKind.Spellblade:
                PassiveStacks = spellCharged ? 1 : 0;
                break;
            case PassiveKind.HealingAura:
            {
                if (!tick) break;
                foreach (var e in Unit.All)
                    if (e.Alive && e.Team == U.Team && !e.IsStructure && U.DistTo(e.transform.position) <= p.B)
                        e.Heal(e.Total.Health * p.A * 0.25f);
                PassiveStacks = 1;
                break;
            }
        }
    }

    // Invisible champions: enemies see nothing, you and your team see a faint ghost. Also keeps dead champions hidden.
    void ApplyStealthLook()
    {
        if (body == null) return;
        var viewer = Local != null ? Local.U.Team : Team.Blue;
        bool stealth = U.Stealthed && U.Alive;
        bool ally = U.Team == viewer;
        bool ghost = stealth && Vision.Sees(viewer, U); // allies always, enemies only while a tower or the Nexus reveals them: drawn see-through
        if (ghost != isGhost)
        {
            if (ghostMat == null)
                ghostMat = new Material(Shader.Find("Sprites/Default")) { color = new Color(Def.Color.r, Def.Color.g, Def.Color.b, 0.35f) };
            body.sharedMaterial = ghost ? ghostMat : normalMat;
            isGhost = ghost;
        }
        body.enabled = U.Alive && Vision.Sees(viewer, U); // out of sight, or invisible to the other team, means not drawn
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
    // Input becomes a command. On the host (or solo) it runs right away; on a client it is sent to the host.
    void HandleInput()
    {
        bool remote = Net.IsClientOnly;
        if (GameInput.KeyDown('B')) { if (remote) { StopPrediction(); Net.SendRecall(); } else CmdRecall(); }
        if (GameInput.RightClickDown && !Hud.PointerOverUi) // clicks on the minimap, panel or shop must not move you
        {
            var p = GameInput.MouseGround();
            var e = Unit.NearestTo(p, 2f, u => u.Team != U.Team && !u.Invulnerable && Vision.Targetable(U.Team, u));
            if (e != null) { if (remote) { StopPrediction(); Net.SendAttack(e); } else CmdAttack(e); }
            else RequestMove(p);
        }
        for (int i = 0; i < 4; i++)
            if (GameInput.KeyDown("QWER"[i]))
            {
                var aim = GameInput.MouseGround();
                if (remote) Net.SendCast(i, aim); else CmdCast(i, aim);
            }
    }

    // Commands: run on the host for every champion. Clients are untrusted, so each one re-checks what it is asked to do.
    // Move order from anywhere (minimap): act directly, or ask the host.
    public void RequestMove(Vector3 p)
    {
        if (!Net.IsClientOnly) { CmdMove(p); return; }
        Net.SendMove(p);
        PredictMove(p); // start walking right away instead of waiting a round trip for the host
    }

    // ---- client-side prediction of your own walking ----
    // The client has the same navmesh, so it can walk your champion immediately and compare with the host's positions as they arrive.
    // Only plain move orders are predicted; attacks, abilities and dashes still follow the host.
    NavMeshAgent predAgent;
    bool predicting;

    void PredictMove(Vector3 p)
    {
        if (!Mirror || this != Local || !U.Alive) return;
        if (!NavMesh.SamplePosition(p, out var dest, 4f, NavMesh.AllAreas)) return;
        if (!predicting)
        {
            if (!NavMesh.SamplePosition(transform.position, out var here, 4f, NavMesh.AllAreas)) return;
            if (predAgent == null)
            {
                predAgent = gameObject.AddComponent<NavMeshAgent>();
                predAgent.baseOffset = 1f; // capsule pivot is its centre
                predAgent.acceleration = 40f;
                predAgent.angularSpeed = 600f;
                predAgent.stoppingDistance = 0.1f;
            }
            GetComponent<NetGhost>().enabled = false; // the agent moves us now
            predAgent.enabled = true;
            predAgent.Warp(here.position);
            predicting = true;
        }
        predAgent.speed = U.Total.MoveSpeed;
        predAgent.SetDestination(dest.position);
    }

    public void StopPrediction()
    {
        if (!predicting) return;
        predicting = false;
        predAgent.enabled = false;
        GetComponent<NetGhost>().enabled = true; // back to following the host's numbers
    }

    // Called for every snapshot: stop predicting once we arrive, or if we drifted far from where the host says we are
    // (blocked, stunned, rooted or slowed), so the host always wins.
    public void Reconcile(Vector3 hostPos)
    {
        if (!predicting) return;
        bool arrived = !predAgent.pathPending && predAgent.remainingDistance <= predAgent.stoppingDistance + 0.2f;
        if (!U.Alive || arrived || Unit.FlatDist(transform.position, hostPos) > 4f) StopPrediction();
    }
    public void CmdMove(Vector3 p) { Recalling = false; target = null; MoveTo(p); }
    public void CmdAttack(Unit t)
    {
        if (t == null || t.Team == U.Team || t.Invulnerable || !Vision.Targetable(U.Team, t)) return;
        Recalling = false;
        target = t;
        moveOrder = false;
    }
    public void CmdCast(int slot, Vector3 aim) { TryCast(slot, aim); }
    public void CmdRecall() { StartRecall(); }

    // What the shop and skill buttons call: act directly, or ask the host.
    public void RequestBuy(ItemDef it) { if (Net.IsClientOnly) Net.SendBuy(it); else Buy(it); }
    public void RequestSell(int index) { if (Net.IsClientOnly) Net.SendSell(index); else Sell(index); }
    public void RequestRank(int slot) { if (Net.IsClientOnly) Net.SendRank(slot); else Rank(slot); }

    void MoveTo(Vector3 p)
    {
        if (NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas) && U.GoTo(hit.position))
            moveOrder = true;
    }

    // ---- auto attacks ----
    void UpdateAttack()
    {
        if (U.Stunned) return;
        if (target != null && (!target.Alive || target.Invulnerable || !Vision.Targetable(U.Team, target))) target = null;
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
            NoteAction();
            var pas = Def.Passive;
            float dmg = U.Total.AD * (1f + AutoBonus(target));
            target.TakeDamage(dmg, DamageType.Physical, U); // shortcut: ranged attacks hit instantly, no projectile
            if (pas.Kind == PassiveKind.Lifesteal) U.Heal(dmg * pas.A); // shortcut: heals from damage before armor
            else if (pas.Kind == PassiveKind.Frenzy)
            {
                PassiveStacks = Mathf.Min(PassiveStacks + 1, (int)pas.B);
                frenzyUntil = Time.time + pas.C;
                SetPassiveBonus(Fx.St(aspd: pas.A * PassiveStacks));
            }
            if (spellCharged) // Arcane Charge: the first attack after a spell hits harder and refunds mana
            {
                var p = Def.Passive;
                spellCharged = false;
                target.TakeDamage(p.A + p.B * U.Total.AP, DamageType.Magic, U);
                U.Mana = Mathf.Min(U.Total.Mana, U.Mana + p.C);
            }
            Vfx.Auto(U, Def.Color, Def.AttackRange <= 3f, target);
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
            tgt = Unit.NearestTo(aim, 3f, u => u.Team != U.Team && !u.Invulnerable && Vision.Targetable(U.Team, u) && U.DistTo(u.transform.position) <= a.Range);
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
        NoteAction();
        if (Def.Passive.Kind == PassiveKind.Spellblade) spellCharged = true;
        else if (Def.Passive.Kind == PassiveKind.Spellrunner) // casting makes you faster for a moment
        {
            U.AddBuff(Fx.St(ms: Def.Passive.A), Def.Passive.B);
            spellrunUntil = Time.time + Def.Passive.B;
        }
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

        Vfx.Cast(a, U, Def.Color, pos, dir, point, dist, hit, this == Local);
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
            case EffectType.Damage:
            {
                float dmg = amt * (1f + e.MissingHp * (1f - t.Health / t.Total.Health)); // wounded targets take more
                t.TakeDamage(dmg, e.DType, U);
                if (e.Vamp > 0f) U.Heal(dmg * e.Vamp); // shortcut: heals from damage before armor, not after
                break;
            }
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
        lastAction = Time.time; // a stealth passive should not make you invisible the instant you respawn
        U.Agent.enabled = true;
        U.Agent.Warp(spawnPos);
        U.Health = U.Total.Health;
        U.Mana = U.Total.Mana;
        U.Shield = 0f;
        foreach (var r in renders) r.enabled = true;
    }
}
