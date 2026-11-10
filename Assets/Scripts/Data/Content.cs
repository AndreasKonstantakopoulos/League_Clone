using System.Collections.Generic;
using UnityEngine;
using static Fx;

// All champions and items live here as plain tables. Adding content = adding rows.
public static class Content
{
    static ItemDef It(string name, int cost, Stats bonus, params string[] components) =>
        new ItemDef { Name = name, Cost = cost, Bonus = bonus, Components = components };

    public static readonly List<ItemDef> Items = new List<ItemDef>
    {
        // Components
        It("Long Sword", 350, St(ad: 10)),
        It("Pickaxe", 875, St(ad: 25)),
        It("Dagger", 300, St(aspd: 0.15f)),
        It("Amplifying Tome", 435, St(ap: 20)),
        It("Blasting Wand", 850, St(ap: 40)),
        It("Cloth Armor", 300, St(armor: 15)),
        It("Chain Vest", 800, St(armor: 40)),
        It("Null Mantle", 450, St(mr: 25)),
        It("Ruby Crystal", 400, St(hp: 150)),
        It("Giant's Belt", 900, St(hp: 350)),
        It("Sapphire Crystal", 350, St(mana: 250)),
        It("Boots", 300, St(ms: 0.5f)),
        It("Regen Charm", 300, St(hpr: 1.5f)),
        It("Mana Charm", 300, St(mpr: 1.5f)),
        It("Chrono Lens", 600, St(cdr: 0.10f)),
        // Combined
        It("Swift Boots", 900, St(ms: 0.9f, aspd: 0.2f), "Boots", "Dagger"),
        It("Crimson Fang", 2600, St(ad: 60), "Pickaxe", "Long Sword"),
        It("Gale Edge", 2700, St(ad: 30, aspd: 0.45f), "Dagger", "Pickaxe"),
        It("Hunter's Bow", 1900, St(ad: 25, aspd: 0.5f), "Long Sword", "Dagger", "Dagger"),
        It("Executioner", 3300, St(ad: 90), "Pickaxe", "Pickaxe"),
        It("Duelist's Blade", 2900, St(ad: 40, hp: 250), "Pickaxe", "Ruby Crystal"),
        It("Arcane Staff", 2800, St(ap: 90), "Blasting Wand", "Amplifying Tome"),
        It("Voidhat", 3400, St(ap: 120), "Blasting Wand", "Blasting Wand"),
        It("Sage Orb", 2400, St(ap: 50, mana: 300, cdr: 0.10f), "Amplifying Tome", "Sapphire Crystal", "Chrono Lens"),
        It("Bastion Plate", 2700, St(armor: 70, hp: 300), "Chain Vest", "Ruby Crystal"),
        It("Warden Mail", 3100, St(armor: 75, hpr: 3f), "Chain Vest", "Cloth Armor"),
        It("Spirit Cloak", 2500, St(mr: 60, hp: 350), "Null Mantle", "Giant's Belt"),
        It("Titan's Heart", 3000, St(hp: 700), "Giant's Belt", "Ruby Crystal"),
        It("Frost Core", 2600, St(hp: 300, mana: 400, cdr: 0.15f), "Sapphire Crystal", "Ruby Crystal", "Chrono Lens"),
        It("Lifewell", 2300, St(hpr: 4f, mpr: 3f, hp: 200), "Regen Charm", "Mana Charm", "Ruby Crystal"),
    };

    public static ItemDef Item(string name) => Items.Find(i => i.Name == name);

    public static readonly List<ChampionDef> Champions = new List<ChampionDef>
    {
        new ChampionDef
        {
            Name = "Orrin Ironwall", Role = Role.Tank, Color = new Color(0.55f, 0.6f, 0.7f), AttackRange = 2.2f,
            Base = St(hp: 680, mana: 280, ad: 58, armor: 38, mr: 36, aspd: 0.62f, ms: 5.8f, hpr: 1.5f, mpr: 1f),
            Growth = St(hp: 95, mana: 40, ad: 3.5f, armor: 4.5f, mr: 1.25f, aspd: 0.02f),
            Abilities = new[]
            {
                Ab("Shield Bash", Shape.TargetEnemy, 3.5f, 0, 8, 40, Phys(60, 30, ad: 0.6f), Stun(1f)),
                Ab("Stone Skin", Shape.Self, 0, 0, 14, 60, Shield(80, 40, dur: 4f, who: Affects.Caster), Buff(St(armor: 20), 4f)),
                Ab("Earthshatter", Shape.SelfArea, 0, 5, 10, 55, Magic(50, 30), Slow(0.35f, 2f)),
                Ab("Unstoppable Charge", Shape.Skillshot, 10, 1.5f, 55, 100, Dash(9), Phys(100, 60, ad: 0.8f), Stun(1f)).Pierces().Ult(),
            }
        },
        new ChampionDef
        {
            Name = "Kael Ashblade", Role = Role.Bruiser, Color = new Color(0.85f, 0.45f, 0.2f), AttackRange = 2.2f,
            Base = St(hp: 620, mana: 260, ad: 64, armor: 32, mr: 32, aspd: 0.67f, ms: 6f, hpr: 1.5f, mpr: 1f),
            Growth = St(hp: 90, mana: 35, ad: 3.8f, armor: 3.8f, mr: 1.25f, aspd: 0.025f),
            Abilities = new[]
            {
                Ab("Cleaving Slash", Shape.Skillshot, 4.5f, 1.5f, 5, 30, Phys(50, 30, ad: 0.9f)).Pierces(),
                Ab("Battle Cry", Shape.Self, 0, 0, 12, 40, Buff(St(ad: 20, aspd: 0.3f), 5f)),
                Ab("Leaping Strike", Shape.PointArea, 6.5f, 2.5f, 10, 50, Dash(6.5f), Phys(60, 35, ad: 0.6f), Slow(0.3f, 1.5f)),
                Ab("Whirling Fury", Shape.SelfArea, 0, 4, 50, 100, Phys(120, 80, ad: 1f), Heal(100, 60, who: Affects.Caster)).Ult(),
            }
        },
        new ChampionDef
        {
            Name = "Vex Nightshade", Role = Role.Assassin, Color = new Color(0.45f, 0.2f, 0.6f), AttackRange = 2.2f,
            Base = St(hp: 560, mana: 250, ad: 62, armor: 27, mr: 32, aspd: 0.7f, ms: 6.4f, hpr: 1.3f, mpr: 1f),
            Growth = St(hp: 80, mana: 35, ad: 3.6f, armor: 3.5f, mr: 1.25f, aspd: 0.03f),
            Abilities = new[]
            {
                Ab("Shadow Cut", Shape.Skillshot, 7, 1.2f, 6, 45, Phys(70, 40, ad: 0.9f)),
                Ab("Shroud", Shape.Self, 0, 0, 12, 50, Buff(St(ms: 2f, aspd: 0.2f), 3f), Shield(60, 30, dur: 2f, who: Affects.Caster)),
                Ab("Blink Strike", Shape.TargetEnemy, 8, 0, 9, 55, Dash(8), Magic(60, 40, ad: 0.5f), Slow(0.3f, 1f)),
                Ab("Death Mark", Shape.TargetEnemy, 9, 0, 50, 100, Dash(9), Phys(150, 100, ad: 1.2f), Silence(1f)).Ult(),
            }
        },
        new ChampionDef
        {
            Name = "Sera Longshot", Role = Role.Marksman, Color = new Color(0.9f, 0.8f, 0.3f), AttackRange = 7.5f,
            Base = St(hp: 540, mana: 280, ad: 60, armor: 26, mr: 30, aspd: 0.68f, ms: 5.8f, hpr: 1.2f, mpr: 1f),
            Growth = St(hp: 78, mana: 35, ad: 3.4f, armor: 3.3f, mr: 1.25f, aspd: 0.03f),
            Abilities = new[]
            {
                Ab("Piercing Arrow", Shape.Skillshot, 12, 1.2f, 6, 50, Phys(60, 35, ad: 1f)).Pierces(),
                Ab("Rapid Fire", Shape.Self, 0, 0, 14, 50, Buff(St(aspd: 0.6f, ms: 0.5f), 4f)),
                Ab("Snare Trap", Shape.PointArea, 9, 2.5f, 12, 60, Root(1.5f), Magic(40, 25, ad: 0.4f)),
                Ab("Eagle Strike", Shape.Skillshot, 20, 2, 60, 100, Phys(200, 120, ad: 1.5f)).Pierces().Ult(),
            }
        },
        new ChampionDef
        {
            Name = "Ilyra Stormcaller", Role = Role.Mage, Color = new Color(0.3f, 0.7f, 1f), AttackRange = 7f,
            Base = St(hp: 520, mana: 380, ad: 52, armor: 22, mr: 30, aspd: 0.62f, ms: 5.7f, hpr: 1.1f, mpr: 1.5f),
            Growth = St(hp: 75, mana: 50, ad: 3f, armor: 3f, mr: 1.25f, aspd: 0.02f),
            Abilities = new[]
            {
                Ab("Arc Bolt", Shape.Skillshot, 10, 1.2f, 5, 55, Magic(70, 45, ap: 0.7f)),
                Ab("Static Field", Shape.PointArea, 9, 3.5f, 10, 70, Magic(50, 35, ap: 0.5f), Slow(0.4f, 2f)),
                Ab("Gale Step", Shape.Self, 0, 0, 12, 60, Dash(6), Shield(50, 30, ap: 0.4f, who: Affects.Caster)),
                Ab("Thunderstorm", Shape.PointArea, 10, 5, 55, 120, Magic(180, 120, ap: 0.9f), Stun(1f)).Ult(),
            }
        },
        new ChampionDef
        {
            Name = "Mirel Dawnwhisper", Role = Role.Support, Color = new Color(0.95f, 0.9f, 0.7f), AttackRange = 6f,
            Base = St(hp: 540, mana: 400, ad: 50, armor: 28, mr: 34, aspd: 0.62f, ms: 5.8f, hpr: 1.3f, mpr: 2f),
            Growth = St(hp: 80, mana: 55, ad: 2.8f, armor: 3.5f, mr: 1.25f, aspd: 0.02f),
            Abilities = new[]
            {
                Ab("Mending Light", Shape.TargetAlly, 9, 0, 6, 60, Heal(70, 40, ap: 0.6f)),
                Ab("Warding Veil", Shape.SelfArea, 0, 6, 14, 70, Shield(60, 35, ap: 0.4f)),
                Ab("Binding Light", Shape.Skillshot, 10, 1.2f, 11, 60, Magic(50, 30, ap: 0.5f), Root(1.5f)),
                Ab("Radiant Dawn", Shape.SelfArea, 0, 8, 60, 120, Heal(150, 100, ap: 0.8f), Buff(St(ms: 1f), 3f, Affects.Allies)).Ult(),
            }
        },
    };

    // Startup self-check. Returns null when the tables are consistent, otherwise the problem.
    public static string Validate()
    {
        var names = new HashSet<string>();
        foreach (var i in Items)
        {
            if (!names.Add(i.Name)) return "Duplicate item " + i.Name;
            foreach (var c in i.Components)
                if (Item(c) == null) return $"Item {i.Name}: unknown component {c}";
        }
        var abilities = new HashSet<string>();
        foreach (var c in Champions)
        {
            if (c.Abilities.Length != 4) return $"{c.Name} must have 4 abilities";
            foreach (var a in c.Abilities)
            {
                if (!abilities.Add(a.Name)) return "Duplicate ability " + a.Name;
                if (a.Effects.Length == 0) return a.Name + " has no effects";
            }
        }
        return null;
    }
}
