using System;
using System.Collections.Generic;
using UnityEngine;

public enum Team { Blue, Red, Neutral } // Neutral = jungle monsters
// The second team is called Red in code but shown as Purple, as on the 2013 map.
public static class TeamText
{
    public static string Name(Team t) { return t == Team.Red ? "Purple" : t.ToString(); }
}

public enum Role { Tank, Bruiser, Assassin, Marksman, Mage, Support }
public enum Shape { Self, SelfArea, TargetEnemy, TargetAlly, PointArea, Skillshot }
public enum EffectType { Damage, Heal, Shield, Stun, Root, Slow, Silence, Dash, Knockback, Pull, Buff }
public enum DamageType { Physical, Magic, True }
public enum Affects { Enemies, Allies, Caster }
// How an ability looks. Each style is its own particle recipe in Vfx.cs.
// What kind of passive a champion has; the numbers A, B, C mean something different for each kind (see Champion.TickPassive).
public enum PassiveKind { Stonebound, LastStand, ShadowVeil, Focus, Spellblade, HealingAura, Thorns, Aegis, Sprint, Lifesteal, Frenzy, Executioner, Wellspring, Spellrunner }
public enum VfxStyle { Default, Impact, Slash, Whirl, Slam, Dome, Aura, Dash, Strike, Arrow, Bolt, Beam, Trap, Nova, Field, Storm, Heal, Dawn }

[Serializable]
public class Stats
{
    public float Health, Mana, AD, AP, Armor, MR, AttackSpeed, MoveSpeed, HealthRegen, ManaRegen, CDR;

    public static Stats operator +(Stats a, Stats b) => new Stats
    {
        Health = a.Health + b.Health, Mana = a.Mana + b.Mana, AD = a.AD + b.AD, AP = a.AP + b.AP,
        Armor = a.Armor + b.Armor, MR = a.MR + b.MR, AttackSpeed = a.AttackSpeed + b.AttackSpeed,
        MoveSpeed = a.MoveSpeed + b.MoveSpeed, HealthRegen = a.HealthRegen + b.HealthRegen,
        ManaRegen = a.ManaRegen + b.ManaRegen, CDR = a.CDR + b.CDR
    };

    public Stats Scaled(float k) => new Stats
    {
        Health = Health * k, Mana = Mana * k, AD = AD * k, AP = AP * k, Armor = Armor * k, MR = MR * k,
        AttackSpeed = AttackSpeed * k, MoveSpeed = MoveSpeed * k, HealthRegen = HealthRegen * k,
        ManaRegen = ManaRegen * k, CDR = CDR * k
    };

    public override string ToString()
    {
        var parts = new List<string>();
        void Add(string n, float v, string fmt = "0") { if (v != 0f) parts.Add($"+{v.ToString(fmt)} {n}"); }
        Add("HP", Health); Add("Mana", Mana); Add("AD", AD); Add("AP", AP); Add("Armor", Armor); Add("MR", MR);
        Add("AS", AttackSpeed, "0.00"); Add("MS", MoveSpeed, "0.0"); Add("HP/s", HealthRegen, "0.0");
        Add("MP/s", ManaRegen, "0.0"); Add("% CDR", CDR * 100f);
        return string.Join(", ", parts);
    }
}

// One building block of an ability. An ability is just a list of these.
public class Effect
{
    public EffectType Type;
    public DamageType DType;
    public Affects Who = Affects.Enemies;
    public float Base, PerRank, AD, AP, Duration;
    public Stats Buff;
    public float MissingHp; // damage grows by up to this fraction as the target's health drops
    public float Vamp;      // fraction of damage dealt that heals the caster

    public Effect Executes(float k) { MissingHp = k; return this; }
    public Effect Vamps(float f) { Vamp = f; return this; }
    string Extras() =>
        (MissingHp > 0f ? $", up to +{MissingHp * 100f:0}% vs wounded" : "") + (Vamp > 0f ? $", heals you for {Vamp * 100f:0}% of it" : "");

    // Damage/heal/shield amount, slow fraction, or dash/knockback/pull distance.
    public float Amount(Unit caster, int rank) =>
        Base + PerRank * (rank - 1) + AD * caster.Total.AD + AP * caster.Total.AP;

    // Live version for tooltips: real numbers for this caster (so item bonuses show up) at this rank.
    public string Describe(Unit caster, int rank)
    {
        float baseAmt = Base + PerRank * (Mathf.Max(1, rank) - 1);
        float bonus = AD * caster.Total.AD + AP * caster.Total.AP;
        string from = AD > 0f && AP > 0f ? $"{AD * 100f:0}% AD and {AP * 100f:0}% AP"
                    : AD > 0f ? $"{AD * 100f:0}% AD" : $"{AP * 100f:0}% AP";
        string detail = AD > 0f || AP > 0f ? $" <color=#9aa>({baseAmt:0} base + {bonus:0} from {from})</color>" : "";
        string text;
        switch (Type)
        {
            case EffectType.Damage:
            {
                string col = DType == DamageType.Physical ? "#ff9a40" : DType == DamageType.Magic ? "#b57aff" : "#ffffff";
                text = $"Deals <color={col}>{baseAmt + bonus:0}</color> {DType.ToString().ToLower()} damage{detail}{Extras()}";
                break;
            }
            case EffectType.Heal: text = $"Heals <color=#6fe07a>{baseAmt + bonus:0}</color>{detail}"; break;
            case EffectType.Shield: text = $"Shields <color=#6fd3ff>{baseAmt + bonus:0}</color> for {Duration:0.#}s{detail}"; break;
            default: text = Describe(); break;
        }
        return text + (Who == Affects.Caster ? " (self)" : Who == Affects.Allies ? " (allies)" : "");
    }

    string Scaling() => (AD > 0f ? $" +{AD * 100f:0}% AD" : "") + (AP > 0f ? $" +{AP * 100f:0}% AP" : "");

    // Short text for the champion-select screen.
    public string Describe()
    {
        switch (Type)
        {
            case EffectType.Damage: return $"{Base:0}+{PerRank:0}/rank {DType} damage{Scaling()}{Extras()}";
            case EffectType.Heal:
            case EffectType.Shield: return $"{Type} {Base:0}+{PerRank:0}/rank{Scaling()}";
            case EffectType.Stun:
            case EffectType.Root:
            case EffectType.Silence: return $"{Type} {Duration:0.#}s";
            case EffectType.Slow: return $"Slow {Base * 100f:0}% for {Duration:0.#}s";
            case EffectType.Dash:
            case EffectType.Knockback:
            case EffectType.Pull: return $"{Type} {Base:0.#}m";
            default: return $"Buff ({Buff}) for {Duration:0.#}s";
        }
    }
}

public class AbilityDef
{
    public string Name;
    public Shape Shape;
    public float Range, Radius, Cooldown, CdPerRank, Mana;
    public bool Pierce;
    public int MaxRank = 5;
    public Effect[] Effects;
    public VfxStyle Style;
    public float Power = 0.4f; // 0..1: how big the effect is and how hard the camera shakes
    public Color? Tint;        // overrides the damage-type colour for this ability

    public AbilityDef Looks(VfxStyle style, float power, Color? tint = null) { Style = style; Power = power; Tint = tint; return this; }
    public AbilityDef Pierces() { Pierce = true; return this; }
    public AbilityDef Ult() { MaxRank = 3; CdPerRank = Cooldown * 0.1f; return this; }
    public float CooldownAt(int rank, float cdr) =>
        Mathf.Max(1f, Cooldown - CdPerRank * (rank - 1)) * (1f - Mathf.Min(cdr, 0.4f));

    public string Describe() =>
        $"{Brief()}\n{Shape}, range {Range:0.#}{(Radius > 0f ? $", radius {Radius:0.#}" : "")}, cooldown {Cooldown:0}s, {Mana:0} mana\n" +
        string.Join(", ", Array.ConvertAll(Effects, e => e.Describe()));

    string ShapeText()
    {
        switch (Shape)
        {
            case Shape.Self: return "Self cast";
            case Shape.SelfArea: return "Around you";
            case Shape.TargetEnemy: return "Target enemy";
            case Shape.TargetAlly: return "Target ally";
            case Shape.PointArea: return "Ground area";
            default: return Pierce ? "Skillshot (pierces)" : "Skillshot (first enemy hit)";
        }
    }

    // Hover text in the style of the genre: header, one-line summary, live numbers, then per-rank tables.
    public string Tooltip(Unit c, int rank, char key = ' ')
    {
        int r = Mathf.Max(1, rank);
        var sb = new System.Text.StringBuilder();
        sb.Append($"<b>{(key == ' ' ? "" : $"[{key}] ")}{Name}</b>   <color=#9aa>{(rank == 0 ? "not learned" : $"rank {rank}/{MaxRank}")}</color>\n");
        sb.Append($"<color=#8fd3ff>{CooldownAt(r, c.Total.CDR):0.#}s</color> cooldown   <color=#7fa6ff>{Mana:0}</color> mana   <color=#9aa>{ShapeText()}, range {Range:0.#}{(Radius > 0f ? $", radius {Radius:0.#}" : "")}</color>\n");
        sb.Append(Brief()).Append("\n\n");
        foreach (var e in Effects) sb.Append(e.Describe(c, r)).Append('\n');
        sb.Append('\n');
        var main = Array.Find(Effects, e => e.Type == EffectType.Damage || e.Type == EffectType.Heal || e.Type == EffectType.Shield);
        if (main != null)
            sb.Append($"<color=#9aa>{(main.Type == EffectType.Damage ? "Damage" : main.Type == EffectType.Heal ? "Healing" : "Shield")}</color>   {PerRank(rank, k => (main.Base + main.PerRank * (k - 1)).ToString("0"))}\n");
        sb.Append($"<color=#9aa>Cooldown</color>   {PerRank(rank, k => CooldownAt(k, c.Total.CDR).ToString("0.#"))}");
        return sb.ToString();
    }

    // [ a / b / c ] with the current rank highlighted.
    string PerRank(int current, Func<int, string> value)
    {
        var parts = new string[MaxRank];
        for (int k = 1; k <= MaxRank; k++) parts[k - 1] = k == current ? $"<b><color=#ffd24a>{value(k)}</color></b>" : value(k);
        return "[ " + string.Join(" / ", parts) + " ]";
    }

    public string Desc; // one short sentence for tooltips
    public AbilityDef Says(string text) { Desc = text; return this; }

    // One brief sentence on what it does: the hand-written text, or one generated from its effects.
    public string Brief()
    {
        if (!string.IsNullOrEmpty(Desc)) return Desc;
        var verbs = new List<string>();
        foreach (var e in Effects)
            switch (e.Type)
            {
                case EffectType.Damage: verbs.Add("deals " + e.DType.ToString().ToLower() + " damage"); break;
                case EffectType.Heal: verbs.Add("heals"); break;
                case EffectType.Shield: verbs.Add("shields"); break;
                case EffectType.Stun: verbs.Add("stuns"); break;
                case EffectType.Root: verbs.Add("roots"); break;
                case EffectType.Silence: verbs.Add("silences"); break;
                case EffectType.Slow: verbs.Add("slows"); break;
                case EffectType.Dash: verbs.Add("dashes forward"); break;
                case EffectType.Knockback: verbs.Add("knocks back"); break;
                case EffectType.Pull: verbs.Add("pulls in"); break;
                default: verbs.Add("grants a bonus"); break;
            }
        string list = verbs.Count > 1 ? string.Join(", ", verbs.GetRange(0, verbs.Count - 1)) + " and " + verbs[verbs.Count - 1] : verbs[0];
        string open;
        switch (Shape)
        {
            case Shape.Self: open = "Self cast:"; break;
            case Shape.SelfArea: open = "Around you,"; break;
            case Shape.TargetEnemy: open = "On an enemy,"; break;
            case Shape.TargetAlly: open = "On an ally,"; break;
            case Shape.PointArea: open = "In a target area,"; break;
            default: open = Pierce ? "A piercing skillshot that" : "A skillshot that"; break;
        }
        return open + " " + list + ".";
    }
}

public class PassiveDef
{
    public string Name, Desc;
    public PassiveKind Kind;
    public float A, B, C;
    public Stats Bonus; // flat stats some passives grant while active
}

public class ChampionDef
{
    public PassiveDef Passive;
    public string Name;
    public Role Role;
    public Color Color;
    public float AttackRange;
    public Stats Base, Growth;      // growth is added once per level above 1
    public AbilityDef[] Abilities;  // Q, W, E, R
}

public class ItemDef
{
    public string Name;
    public int Cost;
    public Stats Bonus;
    public string[] Components = new string[0];
}

// Short builders so content tables stay readable.
public static class Fx
{
    public static Stats St(float hp = 0, float mana = 0, float ad = 0, float ap = 0, float armor = 0, float mr = 0,
        float aspd = 0, float ms = 0, float hpr = 0, float mpr = 0, float cdr = 0) => new Stats
    {
        Health = hp, Mana = mana, AD = ad, AP = ap, Armor = armor, MR = mr, AttackSpeed = aspd,
        MoveSpeed = ms, HealthRegen = hpr, ManaRegen = mpr, CDR = cdr
    };

    static Effect Dmg(DamageType t, float b, float pr, float ad, float ap) =>
        new Effect { Type = EffectType.Damage, DType = t, Base = b, PerRank = pr, AD = ad, AP = ap };
    public static Effect Phys(float b, float pr, float ad = 0, float ap = 0) => Dmg(DamageType.Physical, b, pr, ad, ap);
    public static Effect Magic(float b, float pr, float ad = 0, float ap = 0) => Dmg(DamageType.Magic, b, pr, ad, ap);
    public static Effect Heal(float b, float pr, float ap = 0, Affects who = Affects.Allies) =>
        new Effect { Type = EffectType.Heal, Base = b, PerRank = pr, AP = ap, Who = who };
    public static Effect Shield(float b, float pr, float ap = 0, float dur = 3f, Affects who = Affects.Allies) =>
        new Effect { Type = EffectType.Shield, Base = b, PerRank = pr, AP = ap, Duration = dur, Who = who };
    public static Effect Stun(float d) => new Effect { Type = EffectType.Stun, Duration = d };
    public static Effect Root(float d) => new Effect { Type = EffectType.Root, Duration = d };
    public static Effect Silence(float d) => new Effect { Type = EffectType.Silence, Duration = d };
    public static Effect Slow(float frac, float d) => new Effect { Type = EffectType.Slow, Base = frac, Duration = d };
    public static Effect Dash(float dist) => new Effect { Type = EffectType.Dash, Base = dist, Who = Affects.Caster };
    public static Effect Knock(float dist) => new Effect { Type = EffectType.Knockback, Base = dist };
    public static Effect Pull(float dist) => new Effect { Type = EffectType.Pull, Base = dist };
    public static Effect Buff(Stats s, float dur, Affects who = Affects.Caster) =>
        new Effect { Type = EffectType.Buff, Buff = s, Duration = dur, Who = who };

    public static PassiveDef Pas(PassiveKind kind, string name, string desc, float a = 0, float b = 0, float c = 0, Stats bonus = null) =>
        new PassiveDef { Kind = kind, Name = name, Desc = desc, A = a, B = b, C = c, Bonus = bonus };

    public static AbilityDef Ab(string name, Shape shape, float range, float radius, float cd, float mana, params Effect[] fx) =>
        new AbilityDef { Name = name, Shape = shape, Range = range, Radius = radius, Cooldown = cd, CdPerRank = 0.5f, Mana = mana, Effects = fx };
}
