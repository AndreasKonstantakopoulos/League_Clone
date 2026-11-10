using System.Collections.Generic;
using System.Linq;
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

    // Group the roster by role (stable), so champion select lists tanks together, then bruisers, and so on.
    static Content() { Champions = Champions.OrderBy(c => c.Role).ToList(); }

    public static ItemDef Item(string name) => Items.Find(i => i.Name == name);

    public static readonly List<ChampionDef> Champions = new List<ChampionDef>
    {
        new ChampionDef
        {
            Name = "Orrin Ironwall", Role = Role.Tank, Color = new Color(0.55f, 0.6f, 0.7f), AttackRange = 2.2f,
            Base = St(hp: 680, mana: 280, ad: 58, armor: 38, mr: 36, aspd: 0.62f, ms: 5.8f, hpr: 1.5f, mpr: 1f),
            Growth = St(hp: 95, mana: 40, ad: 3.5f, armor: 4.5f, mr: 1.25f, aspd: 0.02f),
            Passive = Pas(PassiveKind.Stonebound, "Stonebound", "Gains 4 armor and magic resist for every enemy within 7 m, up to 5 enemies.", a: 4f, b: 7f, c: 5f),
            Abilities = new[]
            {
                // Hammers one enemy: shoves it back, then stuns.
                Ab("Shield Bash", Shape.TargetEnemy, 3.5f, 0, 8, 40, Phys(60, 30, ad: 0.6f), Knock(1.5f), Stun(1f))
                    .Says("Hammers one enemy: shoves it back, then stuns.").Looks(VfxStyle.Impact, 0.35f, new Color(0.9f, 0.8f, 0.5f)),
                // Armour and magic resist plus a shield: he gets harder to kill the longer he holds it.
                Ab("Stone Skin", Shape.Self, 0, 0, 14, 60, Shield(80, 40, dur: 4f, who: Affects.Caster), Buff(St(armor: 20, mr: 20), 4f))
                    .Says("Armour and magic resist plus a shield: he gets harder to kill the longer he holds it.").Looks(VfxStyle.Dome, 0.2f, new Color(0.85f, 0.8f, 0.55f)),
                // Ground slam around him that slows everything it catches.
                Ab("Earthshatter", Shape.SelfArea, 0, 5, 10, 55, Magic(50, 30, ad: 0.5f), Slow(0.35f, 2f))
                    .Says("Ground slam around him that slows everything it catches.").Looks(VfxStyle.Slam, 0.6f, new Color(0.85f, 0.7f, 0.4f)),
                // Charges through a whole line of enemies and stuns them.
                Ab("Unstoppable Charge", Shape.Skillshot, 10, 1.5f, 55, 100, Dash(9), Phys(100, 60, ad: 0.8f), Stun(1f))
                    .Pierces().Ult().Says("Charges through a whole line of enemies and stuns them.").Looks(VfxStyle.Dash, 1f, new Color(0.95f, 0.85f, 0.5f)),
            }
        },
        new ChampionDef
        {
            Name = "Kael Ashblade", Role = Role.Bruiser, Color = new Color(0.85f, 0.45f, 0.2f), AttackRange = 2.2f,
            Base = St(hp: 620, mana: 260, ad: 64, armor: 32, mr: 32, aspd: 0.67f, ms: 6f, hpr: 1.5f, mpr: 1f),
            Growth = St(hp: 90, mana: 35, ad: 3.8f, armor: 3.8f, mr: 1.25f, aspd: 0.025f),
            Passive = Pas(PassiveKind.LastStand, "Undying Rage", "Below 40% health: +25 attack damage, +0.3 attack speed and regenerates 3% of max health per second.", a: 0.4f, b: 0.03f, bonus: St(ad: 25, aspd: 0.3f)),
            Abilities = new[]
            {
                // Short sweeping cut that heals him for part of the damage.
                Ab("Cleaving Slash", Shape.Skillshot, 4.5f, 1.5f, 5, 30, Phys(50, 30, ad: 0.9f).Vamps(0.2f))
                    .Pierces().Says("Short sweeping cut that heals him for part of the damage.").Looks(VfxStyle.Slash, 0.3f, new Color(1f, 0.45f, 0.12f)),
                // Attack damage, attack speed and a burst of speed, with a little healing.
                Ab("Battle Cry", Shape.Self, 0, 0, 12, 40, Buff(St(ad: 20, aspd: 0.3f, ms: 0.6f), 5f), Heal(40, 20, who: Affects.Caster))
                    .Says("Attack damage, attack speed and a burst of speed, with a little healing.").Looks(VfxStyle.Aura, 0.2f, new Color(1f, 0.4f, 0.1f)),
                // Leaps to the cursor and slams down, slowing what he lands on.
                Ab("Leaping Strike", Shape.PointArea, 6.5f, 2.5f, 10, 50, Dash(6.5f), Phys(60, 35, ad: 0.6f), Slow(0.3f, 1.5f))
                    .Says("Leaps to the cursor and slams down, slowing what he lands on.").Looks(VfxStyle.Slam, 0.5f, new Color(1f, 0.45f, 0.12f)),
                // Spins in a circle: more damage the weaker the enemy is, and he heals.
                Ab("Whirling Fury", Shape.SelfArea, 0, 4, 50, 100, Phys(120, 80, ad: 1f).Executes(0.5f), Heal(100, 60, who: Affects.Caster))
                    .Ult().Says("Spins in a circle: more damage the weaker the enemy is, and he heals.").Looks(VfxStyle.Whirl, 0.7f, new Color(1f, 0.5f, 0.15f)),
            }
        },
        new ChampionDef
        {
            Name = "Vex Nightshade", Role = Role.Assassin, Color = new Color(0.45f, 0.2f, 0.6f), AttackRange = 2.2f,
            Base = St(hp: 560, mana: 250, ad: 62, armor: 27, mr: 32, aspd: 0.7f, ms: 6.4f, hpr: 1.3f, mpr: 1f),
            Growth = St(hp: 80, mana: 35, ad: 3.6f, armor: 3.5f, mr: 1.25f, aspd: 0.03f),
            Passive = Pas(PassiveKind.ShadowVeil, "Shadow Veil", "After 3 s without attacking, casting or taking damage, turns invisible to enemies, moves faster and regenerates 4% of max health and mana per second. Any attack, cast or damage reveals him.", a: 3f, b: 0.04f, c: 1.5f),
            Abilities = new[]
            {
                // Long thin cut that hits the first enemy in line.
                Ab("Shadow Cut", Shape.Skillshot, 7, 1.2f, 6, 45, Phys(70, 40, ad: 0.9f))
                    .Says("Long thin cut that hits the first enemy in line.").Looks(VfxStyle.Slash, 0.3f, new Color(0.65f, 0.25f, 0.95f)),
                // Fades into shadow: a short shield and a burst of speed.
                Ab("Shroud", Shape.Self, 0, 0, 12, 50, Shield(60, 30, dur: 2f, who: Affects.Caster), Buff(St(ms: 2f, aspd: 0.2f), 3f))
                    .Says("Fades into shadow: a short shield and a burst of speed.").Looks(VfxStyle.Aura, 0.1f, new Color(0.55f, 0.2f, 0.85f)),
                // Blinks onto an enemy and slows it.
                Ab("Blink Strike", Shape.TargetEnemy, 8, 0, 9, 55, Dash(8), Magic(60, 40, ad: 0.5f), Slow(0.3f, 1f))
                    .Says("Blinks onto an enemy and slows it.").Looks(VfxStyle.Dash, 0.4f, new Color(0.65f, 0.25f, 0.95f)),
                // Execution: leaps to a target, hits far harder the lower its health, and silences it.
                Ab("Death Mark", Shape.TargetEnemy, 9, 0, 50, 100, Dash(9), Phys(150, 100, ad: 1.2f).Executes(1f), Silence(1f))
                    .Ult().Says("Execution: leaps to a target, hits far harder the lower its health, and silences it.").Looks(VfxStyle.Strike, 0.8f, new Color(0.75f, 0.3f, 1f)),
            }
        },
        new ChampionDef
        {
            Name = "Sera Longshot", Role = Role.Marksman, Color = new Color(0.9f, 0.8f, 0.3f), AttackRange = 7.5f,
            Base = St(hp: 540, mana: 280, ad: 60, armor: 26, mr: 30, aspd: 0.68f, ms: 5.8f, hpr: 1.2f, mpr: 1f),
            Growth = St(hp: 78, mana: 35, ad: 3.4f, armor: 3.3f, mr: 1.25f, aspd: 0.03f),
            Passive = Pas(PassiveKind.Focus, "Steady Aim", "Consecutive auto attacks on the same target (within 2.5 s) deal 8% more damage each, up to 5 times.", a: 0.08f, b: 5f, c: 2.5f),
            Abilities = new[]
            {
                // Fast arrow that passes through everything in its path.
                Ab("Piercing Arrow", Shape.Skillshot, 12, 1.2f, 6, 50, Phys(60, 35, ad: 1f))
                    .Pierces().Says("Fast arrow that passes through everything in its path.").Looks(VfxStyle.Arrow, 0.25f, new Color(1f, 0.85f, 0.3f)),
                // Faster attacks and movement for a few seconds.
                Ab("Rapid Fire", Shape.Self, 0, 0, 14, 50, Buff(St(aspd: 0.6f, ms: 0.5f), 4f))
                    .Says("Faster attacks and movement for a few seconds.").Looks(VfxStyle.Aura, 0.1f, new Color(1f, 0.85f, 0.3f)),
                // Trap on the ground: roots and damages whoever is inside.
                Ab("Snare Trap", Shape.PointArea, 9, 2.5f, 12, 60, Root(1.5f), Magic(40, 25, ad: 0.4f))
                    .Says("Trap on the ground: roots and damages whoever is inside.").Looks(VfxStyle.Trap, 0.3f, new Color(1f, 0.8f, 0.25f)),
                // Huge beam across the map; hits wounded enemies harder.
                Ab("Eagle Strike", Shape.Skillshot, 20, 2, 60, 100, Phys(200, 120, ad: 1.5f).Executes(0.3f))
                    .Pierces().Ult().Says("Huge beam across the map; hits wounded enemies harder.").Looks(VfxStyle.Beam, 1f, new Color(1f, 0.8f, 0.3f)),
            }
        },
        new ChampionDef
        {
            Name = "Ilyra Stormcaller", Role = Role.Mage, Color = new Color(0.3f, 0.7f, 1f), AttackRange = 7f,
            Base = St(hp: 520, mana: 380, ad: 52, armor: 22, mr: 30, aspd: 0.62f, ms: 5.7f, hpr: 1.1f, mpr: 1.5f),
            Growth = St(hp: 75, mana: 50, ad: 3f, armor: 3f, mr: 1.25f, aspd: 0.02f),
            Passive = Pas(PassiveKind.Spellblade, "Arcane Charge", "After casting an ability, her next auto attack deals 40 + 30% AP bonus magic damage and restores 15 mana.", a: 40f, b: 0.3f, c: 15f),
            Abilities = new[]
            {
                // Orb of lightning that explodes on the first enemy it meets.
                Ab("Arc Bolt", Shape.Skillshot, 10, 1.2f, 5, 55, Magic(70, 45, ap: 0.7f))
                    .Says("Orb of lightning that explodes on the first enemy it meets.").Looks(VfxStyle.Bolt, 0.25f, new Color(0.4f, 0.75f, 1f)),
                // Charged field on the ground that damages and slows.
                Ab("Static Field", Shape.PointArea, 9, 3.5f, 10, 70, Magic(50, 35, ap: 0.5f), Slow(0.4f, 2f))
                    .Says("Charged field on the ground that damages and slows.").Looks(VfxStyle.Field, 0.3f, new Color(0.4f, 0.75f, 1f)),
                // Rides the wind a short way and gains a shield.
                Ab("Gale Step", Shape.Self, 0, 0, 12, 60, Dash(6), Shield(50, 30, ap: 0.4f, who: Affects.Caster))
                    .Says("Rides the wind a short way and gains a shield.").Looks(VfxStyle.Dash, 0.2f, new Color(0.5f, 0.85f, 1f)),
                // Calls down a storm of lightning that stuns everything in the area.
                Ab("Thunderstorm", Shape.PointArea, 10, 5, 55, 120, Magic(180, 120, ap: 0.9f), Stun(1f))
                    .Ult().Says("Calls down a storm of lightning that stuns everything in the area.").Looks(VfxStyle.Storm, 1f, new Color(0.45f, 0.8f, 1f)),
            }
        },
        new ChampionDef
        {
            Name = "Mirel Dawnwhisper", Role = Role.Support, Color = new Color(0.95f, 0.9f, 0.7f), AttackRange = 6f,
            Base = St(hp: 540, mana: 400, ad: 50, armor: 28, mr: 34, aspd: 0.62f, ms: 5.8f, hpr: 1.3f, mpr: 2f),
            Growth = St(hp: 80, mana: 55, ad: 2.8f, armor: 3.5f, mr: 1.25f, aspd: 0.02f),
            Passive = Pas(PassiveKind.HealingAura, "Dawn's Grace", "Allies within 8 m, and herself, regenerate 1.2% of max health per second.", a: 0.012f, b: 8f),
            Abilities = new[]
            {
                // Heals one ally (herself if nobody is near the cursor).
                Ab("Mending Light", Shape.TargetAlly, 9, 0, 6, 60, Heal(70, 40, ap: 0.6f))
                    .Says("Heals one ally (herself if nobody is near the cursor).").Looks(VfxStyle.Heal, 0.1f, new Color(1f, 0.95f, 0.6f)),
                // Shields every ally around her.
                Ab("Warding Veil", Shape.SelfArea, 0, 6, 14, 70, Shield(60, 35, ap: 0.4f))
                    .Says("Shields every ally around her.").Looks(VfxStyle.Dome, 0.2f, new Color(1f, 0.95f, 0.6f)),
                // Bolt of light that damages and roots the first enemy it hits.
                Ab("Binding Light", Shape.Skillshot, 10, 1.2f, 11, 60, Magic(50, 30, ap: 0.5f), Root(1.5f))
                    .Says("Bolt of light that damages and roots the first enemy it hits.").Looks(VfxStyle.Bolt, 0.25f, new Color(1f, 0.9f, 0.5f)),
                // A burst of dawn: heals and speeds up every ally nearby.
                Ab("Radiant Dawn", Shape.SelfArea, 0, 8, 60, 120, Heal(150, 100, ap: 0.8f), Buff(St(ms: 1f), 3f, Affects.Allies))
                    .Ult().Says("A burst of dawn: heals and speeds up every ally nearby.").Looks(VfxStyle.Dawn, 0.6f, new Color(1f, 0.9f, 0.45f)),
            }
        },
        // ===================== TANKS =====================
        new ChampionDef
        {
            Name = "Grum Boulderfist", Role = Role.Tank, Color = new Color(0.5f, 0.4f, 0.3f), AttackRange = 2.2f,
            Base = St(hp: 700, mana: 260, ad: 55, armor: 40, mr: 34, aspd: 0.6f, ms: 5.6f, hpr: 1.8f, mpr: 1f),
            Growth = St(hp: 100, mana: 35, ad: 3.2f, armor: 4.8f, mr: 1.2f, aspd: 0.02f),
            Passive = Pas(PassiveKind.Thorns, "Spiked Hide", "Reflects 20% of the damage he takes from enemy attackers back to them as true damage.", a: 0.2f),
            Abilities = new[]
            {
                // Hurls a boulder that slows the first enemy it hits.
                Ab("Boulder Toss", Shape.Skillshot, 9, 1.5f, 7, 45, Phys(60, 35, ad: 0.7f), Slow(0.3f, 1.5f))
                    .Says("Hurls a boulder that slows the first enemy it hits.").Looks(VfxStyle.Bolt, 0.3f, new Color(0.75f, 0.6f, 0.4f)),
                // Hardens into rock: a shield and extra armor.
                Ab("Rock Armor", Shape.Self, 0, 0, 14, 60, Shield(100, 50, dur: 4f, who: Affects.Caster), Buff(St(armor: 30), 4f))
                    .Says("Hardens into rock: a shield and extra armor.").Looks(VfxStyle.Dome, 0.2f, new Color(0.75f, 0.6f, 0.4f)),
                // Stomps the ground: damages, shoves enemies away.
                Ab("Seismic Stomp", Shape.SelfArea, 0, 4.5f, 10, 55, Phys(50, 30, ad: 0.5f), Knock(2.5f))
                    .Says("Stomps the ground: damages, shoves enemies away.").Looks(VfxStyle.Slam, 0.55f, new Color(0.75f, 0.6f, 0.4f)),
                // Brings the ceiling down on a spot: heavy damage and a stun.
                Ab("Avalanche", Shape.PointArea, 9, 4.5f, 55, 100, Phys(120, 70, ad: 0.8f), Stun(1.2f))
                    .Ult().Says("Brings the ceiling down on a spot: heavy damage and a stun.").Looks(VfxStyle.Slam, 0.9f, new Color(0.8f, 0.65f, 0.45f)),
            }
        },
        new ChampionDef
        {
            Name = "Seren Wardenheart", Role = Role.Tank, Color = new Color(0.4f, 0.7f, 0.9f), AttackRange = 2.2f,
            Base = St(hp: 660, mana: 320, ad: 52, armor: 36, mr: 40, aspd: 0.6f, ms: 5.9f, hpr: 1.5f, mpr: 1.5f),
            Growth = St(hp: 92, mana: 45, ad: 3f, armor: 4.2f, mr: 1.8f, aspd: 0.02f),
            Passive = Pas(PassiveKind.Aegis, "Bulwark", "After 5 s without attacking, casting or taking damage, gains a shield worth 12% of her max health.", a: 5f, b: 0.12f),
            Abilities = new[]
            {
                // Throws a chained shield and drags the enemy toward her.
                Ab("Shield Throw", Shape.Skillshot, 8, 1.2f, 8, 50, Phys(55, 30, ad: 0.6f), Pull(3f))
                    .Says("Throws a chained shield and drags the enemy toward her.").Looks(VfxStyle.Bolt, 0.3f, new Color(0.6f, 0.85f, 1f)),
                // Shields every ally around her.
                Ab("Guardian Light", Shape.SelfArea, 0, 5, 14, 70, Shield(70, 40))
                    .Says("Shields every ally around her.").Looks(VfxStyle.Dome, 0.25f, new Color(0.6f, 0.85f, 1f)),
                // Rushes an enemy, damaging and slowing it.
                Ab("Rally Charge", Shape.TargetEnemy, 6, 0, 10, 55, Dash(6), Phys(50, 30, ad: 0.5f), Slow(0.35f, 1.5f))
                    .Says("Rushes an enemy, damaging and slowing it.").Looks(VfxStyle.Dash, 0.3f, new Color(0.6f, 0.85f, 1f)),
                // Makes a stand: big shield and resistances for her whole team nearby.
                Ab("Last Bastion", Shape.SelfArea, 0, 6, 60, 120, Shield(150, 100, dur: 5f), Buff(St(armor: 30, mr: 30), 5f, Affects.Allies))
                    .Ult().Says("Makes a stand: big shield and resistances for her whole team nearby.").Looks(VfxStyle.Dawn, 0.6f, new Color(0.6f, 0.85f, 1f)),
            }
        },
        new ChampionDef
        {
            Name = "Brannoch Stormshield", Role = Role.Tank, Color = new Color(0.3f, 0.4f, 0.8f), AttackRange = 2.2f,
            Base = St(hp: 650, mana: 300, ad: 56, armor: 34, mr: 36, aspd: 0.63f, ms: 6f, hpr: 1.4f, mpr: 1.2f),
            Growth = St(hp: 90, mana: 40, ad: 3.4f, armor: 4f, mr: 1.3f, aspd: 0.02f),
            Passive = Pas(PassiveKind.Sprint, "Charge Ahead", "After 4 s out of combat, gains +2 move speed until he attacks, casts or is hit.", a: 4f, c: 2f),
            Abilities = new[]
            {
                // Lightning hook that pulls the enemy in.
                Ab("Thunder Hook", Shape.Skillshot, 9, 1.2f, 9, 55, Magic(50, 30, ad: 0.5f), Pull(4f))
                    .Says("Lightning hook that pulls the enemy in.").Looks(VfxStyle.Bolt, 0.3f, new Color(0.5f, 0.7f, 1f)),
                // Crackling shield with a little speed.
                Ab("Static Shield", Shape.Self, 0, 0, 12, 60, Shield(70, 35, dur: 4f, who: Affects.Caster), Buff(St(armor: 15, ms: 1f), 4f))
                    .Says("Crackling shield with a little speed.").Looks(VfxStyle.Aura, 0.2f, new Color(0.5f, 0.7f, 1f)),
                // Dashes into an enemy and stuns it.
                Ab("Charging Bolt", Shape.TargetEnemy, 7, 0, 10, 60, Dash(7), Magic(60, 35, ad: 0.4f), Stun(0.8f))
                    .Says("Dashes into an enemy and stuns it.").Looks(VfxStyle.Dash, 0.4f, new Color(0.5f, 0.7f, 1f)),
                // Storms through a line of enemies, shoving them aside.
                Ab("Storm Rush", Shape.Skillshot, 12, 2, 55, 100, Dash(10), Magic(110, 70, ad: 0.7f), Knock(2.5f))
                    .Pierces().Ult().Says("Storms through a line of enemies, shoving them aside.").Looks(VfxStyle.Dash, 0.9f, new Color(0.55f, 0.75f, 1f)),
            }
        },
        // ===================== BRUISERS =====================
        new ChampionDef
        {
            Name = "Dara Bloodmoon", Role = Role.Bruiser, Color = new Color(0.7f, 0.1f, 0.25f), AttackRange = 2.2f,
            Base = St(hp: 600, mana: 240, ad: 66, armor: 30, mr: 32, aspd: 0.68f, ms: 6.1f, hpr: 1.4f, mpr: 1f),
            Growth = St(hp: 88, mana: 33, ad: 3.9f, armor: 3.6f, mr: 1.25f, aspd: 0.028f),
            Passive = Pas(PassiveKind.Lifesteal, "Bloodthirst", "Heals for 15% of the damage her auto attacks deal.", a: 0.15f),
            Abilities = new[]
            {
                Ab("Crimson Slash", Shape.Skillshot, 4, 1.6f, 5, 30, Phys(50, 30, ad: 0.9f))
                    .Pierces().Says("Slashes in a short line, hitting every enemy in front of her.").Looks(VfxStyle.Slash, 0.3f, new Color(0.9f, 0.1f, 0.25f)),
                // Trades a little health for power: stronger and faster for a few seconds.
                Ab("Blood Pact", Shape.Self, 0, 0, 12, 40, Heal(50, 25, who: Affects.Caster), Buff(St(ad: 25, aspd: 0.2f), 4f))
                    .Says("Trades a little health for power: stronger and faster for a few seconds.").Looks(VfxStyle.Aura, 0.2f, new Color(0.9f, 0.1f, 0.25f)),
                Ab("Hemorrhage", Shape.TargetEnemy, 4, 0, 9, 50, Phys(60, 30, ad: 0.6f), Slow(0.35f, 2f))
                    .Says("Rips into an enemy, dealing damage and slowing it.").Looks(VfxStyle.Impact, 0.35f, new Color(0.9f, 0.1f, 0.25f)),
                // Feeds on a weakened target: deals far more damage the lower its health and heals her.
                Ab("Crimson Feast", Shape.TargetEnemy, 4.5f, 0, 55, 100, Phys(150, 100, ad: 1f).Executes(0.6f).Vamps(0.5f))
                    .Ult().Says("Feeds on a weakened target: deals far more damage the lower its health and heals her.").Looks(VfxStyle.Strike, 0.7f, new Color(0.95f, 0.15f, 0.3f)),
            }
        },
        new ChampionDef
        {
            Name = "Hrolf Wavebreaker", Role = Role.Bruiser, Color = new Color(0.2f, 0.55f, 0.7f), AttackRange = 2.2f,
            Base = St(hp: 640, mana: 250, ad: 62, armor: 33, mr: 33, aspd: 0.65f, ms: 5.9f, hpr: 1.5f, mpr: 1f),
            Growth = St(hp: 92, mana: 34, ad: 3.6f, armor: 3.9f, mr: 1.25f, aspd: 0.025f),
            Passive = Pas(PassiveKind.Frenzy, "Berserk", "Each auto attack grants +6% attack speed, stacking up to 5 times for 4 s.", a: 0.06f, b: 5f, c: 4f),
            Abilities = new[]
            {
                Ab("Tidal Cleave", Shape.Skillshot, 5, 2, 7, 35, Phys(55, 30, ad: 0.85f), Knock(1.5f))
                    .Pierces().Says("Sweeps a wave through enemies in front of him, hurting and shoving them back.").Looks(VfxStyle.Slash, 0.3f, new Color(0.3f, 0.75f, 0.95f)),
                Ab("Sea Legs", Shape.Self, 0, 0, 12, 40, Shield(50, 25, dur: 3f, who: Affects.Caster), Buff(St(ms: 1f, aspd: 0.25f, armor: 10), 4f))
                    .Says("Steadies himself: a small shield, extra armor and a burst of speed.").Looks(VfxStyle.Aura, 0.2f, new Color(0.3f, 0.75f, 0.95f)),
                // Hurls himself forward and anchors the target in place.
                Ab("Anchor Drop", Shape.PointArea, 7, 2.5f, 11, 55, Dash(6), Phys(60, 35, ad: 0.6f), Root(0.8f))
                    .Says("Hurls himself forward and anchors the target in place.").Looks(VfxStyle.Slam, 0.5f, new Color(0.3f, 0.75f, 0.95f)),
                // Whirlpool: pulls everyone in, hurts and slows them.
                Ab("Maelstrom", Shape.SelfArea, 0, 5, 55, 100, Magic(110, 70, ad: 0.8f), Pull(3.5f), Slow(0.4f, 2f))
                    .Ult().Says("Whirlpool: pulls everyone in, hurts and slows them.").Looks(VfxStyle.Whirl, 0.8f, new Color(0.3f, 0.75f, 0.95f)),
            }
        },
        // ===================== ASSASSINS =====================
        new ChampionDef
        {
            Name = "Riku Silentstep", Role = Role.Assassin, Color = new Color(0.25f, 0.25f, 0.32f), AttackRange = 2.2f,
            Base = St(hp: 550, mana: 240, ad: 64, armor: 26, mr: 31, aspd: 0.72f, ms: 6.5f, hpr: 1.2f, mpr: 1f),
            Growth = St(hp: 78, mana: 33, ad: 3.7f, armor: 3.3f, mr: 1.25f, aspd: 0.032f),
            Passive = Pas(PassiveKind.Executioner, "Killer Instinct", "Auto attacks deal 25% more damage to enemies below 40% health.", a: 0.25f, b: 0.4f),
            Abilities = new[]
            {
                Ab("Throwing Knives", Shape.Skillshot, 8, 1, 6, 45, Phys(60, 40, ad: 0.9f))
                    .Says("Throws a knife that hits the first enemy in line.").Looks(VfxStyle.Arrow, 0.25f, new Color(0.85f, 0.85f, 1f)),
                // Smoke cloud on the ground: slows enemies and silences them.
                Ab("Smoke Bomb", Shape.PointArea, 6, 3, 12, 50, Slow(0.5f, 2f), Silence(1.2f))
                    .Says("Smoke cloud on the ground: slows enemies and silences them.").Looks(VfxStyle.Field, 0.2f, new Color(0.6f, 0.6f, 0.75f)),
                Ab("Backstab Dash", Shape.TargetEnemy, 7, 0, 9, 55, Dash(7), Phys(70, 45, ad: 0.7f))
                    .Says("Dashes to an enemy and stabs it.").Looks(VfxStyle.Dash, 0.35f, new Color(0.85f, 0.85f, 1f)),
                // Finishes a weakened target.
                Ab("Assassinate", Shape.TargetEnemy, 8, 0, 55, 100, Dash(8), Phys(160, 100, ad: 1.3f).Executes(0.8f))
                    .Ult().Says("Finishes a weakened target.").Looks(VfxStyle.Strike, 0.85f, new Color(0.9f, 0.9f, 1f)),
            }
        },
        new ChampionDef
        {
            Name = "Zephyra Quickblade", Role = Role.Assassin, Color = new Color(0.3f, 0.9f, 0.8f), AttackRange = 2.2f,
            Base = St(hp: 540, mana: 260, ad: 60, armor: 25, mr: 30, aspd: 0.74f, ms: 6.6f, hpr: 1.2f, mpr: 1.1f),
            Growth = St(hp: 76, mana: 36, ad: 3.4f, armor: 3.2f, mr: 1.25f, aspd: 0.035f),
            Passive = Pas(PassiveKind.Sprint, "Swift Feet", "After 3 s out of combat, gains +2.5 move speed until she attacks, casts or is hit.", a: 3f, c: 2.5f),
            Abilities = new[]
            {
                Ab("Gale Slash", Shape.Skillshot, 6, 1.4f, 5, 40, Magic(55, 35, ad: 0.6f))
                    .Pierces().Says("Cuts through every enemy in a line with a blade of wind.").Looks(VfxStyle.Slash, 0.3f, new Color(0.35f, 0.95f, 0.85f)),
                Ab("Wind Wall", Shape.Self, 0, 0, 12, 50, Shield(60, 30, dur: 2.5f, who: Affects.Caster), Buff(St(ms: 1.5f, aspd: 0.25f), 3f))
                    .Says("Wraps herself in wind: a short shield with extra move and attack speed.").Looks(VfxStyle.Aura, 0.15f, new Color(0.35f, 0.95f, 0.85f)),
                Ab("Zephyr Step", Shape.PointArea, 6.5f, 2.5f, 9, 50, Dash(6.5f), Magic(55, 35, ad: 0.5f), Slow(0.3f, 1.5f))
                    .Says("Dashes to a spot, damaging and slowing enemies where she lands.").Looks(VfxStyle.Dash, 0.35f, new Color(0.35f, 0.95f, 0.85f)),
                // A flurry of blades all around her.
                Ab("Thousand Cuts", Shape.SelfArea, 0, 3.5f, 50, 100, Phys(110, 70, ad: 0.9f), Slow(0.3f, 1.5f))
                    .Ult().Says("A flurry of blades all around her.").Looks(VfxStyle.Whirl, 0.75f, new Color(0.4f, 1f, 0.9f)),
            }
        },
        // ===================== MARKSMEN =====================
        new ChampionDef
        {
            Name = "Cassian Redhawk", Role = Role.Marksman, Color = new Color(0.8f, 0.3f, 0.2f), AttackRange = 7.5f,
            Base = St(hp: 530, mana: 270, ad: 61, armor: 25, mr: 30, aspd: 0.7f, ms: 5.8f, hpr: 1.2f, mpr: 1f),
            Growth = St(hp: 76, mana: 34, ad: 3.5f, armor: 3.2f, mr: 1.25f, aspd: 0.032f),
            Passive = Pas(PassiveKind.Frenzy, "Rapid Reload", "Each auto attack grants +8% attack speed, stacking up to 6 times for 3 s.", a: 0.08f, b: 6f, c: 3f),
            Abilities = new[]
            {
                Ab("Fire Arrow", Shape.Skillshot, 11, 1.2f, 6, 50, Phys(55, 35, ad: 1f))
                    .Says("Fires an arrow that hits the first enemy in line.").Looks(VfxStyle.Arrow, 0.25f, new Color(1f, 0.45f, 0.2f)),
                Ab("Hawk's Eye", Shape.Self, 0, 0, 14, 50, Buff(St(ad: 15, aspd: 0.3f), 4f))
                    .Says("Sharpens his aim: more damage and attack speed for a few seconds.").Looks(VfxStyle.Aura, 0.1f, new Color(1f, 0.45f, 0.2f)),
                // Net that roots the first enemy it hits.
                Ab("Net Shot", Shape.Skillshot, 9, 1.2f, 11, 55, Phys(40, 25, ad: 0.5f), Root(1.2f))
                    .Says("Net that roots the first enemy it hits.").Looks(VfxStyle.Bolt, 0.25f, new Color(0.9f, 0.8f, 0.5f)),
                // A volley falls on an area, damaging and slowing everything in it.
                Ab("Rain of Arrows", Shape.PointArea, 11, 4.5f, 55, 100, Phys(150, 90, ad: 1.2f), Slow(0.4f, 2f))
                    .Ult().Says("A volley falls on an area, damaging and slowing everything in it.").Looks(VfxStyle.Nova, 0.7f, new Color(1f, 0.5f, 0.2f)),
            }
        },
        new ChampionDef
        {
            Name = "Marek Emberstring", Role = Role.Marksman, Color = new Color(0.9f, 0.6f, 0.2f), AttackRange = 7f,
            Base = St(hp: 520, mana: 290, ad: 59, armor: 24, mr: 30, aspd: 0.66f, ms: 5.8f, hpr: 1.2f, mpr: 1f),
            Growth = St(hp: 74, mana: 36, ad: 3.3f, armor: 3.1f, mr: 1.25f, aspd: 0.03f),
            Passive = Pas(PassiveKind.Lifesteal, "Vampiric Shots", "Heals for 10% of the damage his auto attacks deal.", a: 0.1f),
            Abilities = new[]
            {
                Ab("Ember Shot", Shape.Skillshot, 10, 1.2f, 6, 50, Phys(55, 35, ad: 0.9f))
                    .Says("Fires a glowing shot at the first enemy in line.").Looks(VfxStyle.Bolt, 0.25f, new Color(1f, 0.6f, 0.2f)),
                Ab("Roll Away", Shape.Self, 0, 0, 10, 40, Dash(5), Buff(St(ms: 1.2f), 2f))
                    .Says("Rolls a short way and gains a burst of speed.").Looks(VfxStyle.Dash, 0.2f, new Color(1f, 0.6f, 0.2f)),
                // Smoky arrow that passes through enemies and slows them.
                Ab("Smoke Arrow", Shape.Skillshot, 9, 1.5f, 11, 55, Slow(0.5f, 2f), Magic(40, 25, ad: 0.4f))
                    .Pierces().Says("Smoky arrow that passes through enemies and slows them.").Looks(VfxStyle.Arrow, 0.25f, new Color(0.7f, 0.7f, 0.75f)),
                // Long fire beam through everything in front of him.
                Ab("Dragon's Breath", Shape.Skillshot, 14, 3, 60, 110, Magic(180, 110, ad: 1.2f))
                    .Pierces().Ult().Says("Long fire beam through everything in front of him.").Looks(VfxStyle.Beam, 0.9f, new Color(1f, 0.5f, 0.15f)),
            }
        },
        // ===================== MAGES =====================
        new ChampionDef
        {
            Name = "Nyx Emberweaver", Role = Role.Mage, Color = new Color(1f, 0.4f, 0.15f), AttackRange = 7f,
            Base = St(hp: 510, mana: 400, ad: 50, armor: 22, mr: 30, aspd: 0.62f, ms: 5.7f, hpr: 1.1f, mpr: 1.4f),
            Growth = St(hp: 74, mana: 52, ad: 2.9f, armor: 3f, mr: 1.25f, aspd: 0.02f),
            Passive = Pas(PassiveKind.Wellspring, "Inner Fire", "Regenerates 1.2% of her max mana every second.", a: 0.012f),
            Abilities = new[]
            {
                Ab("Fireball", Shape.Skillshot, 11, 1.4f, 5, 55, Magic(75, 45, ap: 0.75f))
                    .Says("Throws a fireball that explodes on the first enemy it hits.").Looks(VfxStyle.Bolt, 0.3f, new Color(1f, 0.5f, 0.15f)),
                // A column of flame that burns and briefly stuns.
                Ab("Flame Pillar", Shape.PointArea, 9, 3, 10, 65, Magic(60, 40, ap: 0.6f), Stun(0.6f))
                    .Says("A column of flame that burns and briefly stuns.").Looks(VfxStyle.Nova, 0.35f, new Color(1f, 0.5f, 0.15f)),
                // Dashes in a burst of fire that scorches the landing spot.
                Ab("Phoenix Step", Shape.PointArea, 5.5f, 2.5f, 12, 60, Dash(5.5f), Magic(50, 30, ap: 0.5f))
                    .Says("Dashes in a burst of fire that scorches the landing spot.").Looks(VfxStyle.Dash, 0.3f, new Color(1f, 0.55f, 0.15f)),
                Ab("Inferno", Shape.PointArea, 10, 5.5f, 55, 130, Magic(190, 120, ap: 1f), Slow(0.4f, 2f))
                    .Ult().Says("Calls a huge firestorm on an area, hurting and slowing everyone in it.").Looks(VfxStyle.Nova, 0.95f, new Color(1f, 0.45f, 0.1f)),
            }
        },
        new ChampionDef
        {
            Name = "Quillon Frostweaver", Role = Role.Mage, Color = new Color(0.6f, 0.85f, 1f), AttackRange = 7f,
            Base = St(hp: 520, mana: 380, ad: 51, armor: 23, mr: 31, aspd: 0.62f, ms: 5.7f, hpr: 1.1f, mpr: 1.3f),
            Growth = St(hp: 75, mana: 50, ad: 2.9f, armor: 3f, mr: 1.25f, aspd: 0.02f),
            Passive = Pas(PassiveKind.Spellrunner, "Cold Snap", "Casting an ability grants +1.5 move speed for 2 s.", a: 1.5f, b: 2f),
            Abilities = new[]
            {
                // Spear of ice through every enemy in a line; slows.
                Ab("Ice Lance", Shape.Skillshot, 11, 1.1f, 6, 55, Magic(65, 40, ap: 0.65f), Slow(0.35f, 1.5f))
                    .Pierces().Says("Spear of ice through every enemy in a line; slows.").Looks(VfxStyle.Arrow, 0.25f, new Color(0.65f, 0.9f, 1f)),
                // Freezing burst around her roots nearby enemies.
                Ab("Frost Nova", Shape.SelfArea, 0, 4, 11, 65, Magic(55, 35, ap: 0.55f), Root(1f))
                    .Says("Freezing burst around her roots nearby enemies.").Looks(VfxStyle.Nova, 0.35f, new Color(0.65f, 0.9f, 1f)),
                Ab("Glacial Slide", Shape.Self, 0, 0, 12, 55, Dash(6), Shield(40, 25, ap: 0.3f, who: Affects.Caster))
                    .Says("Slides on a trail of ice and gains a small shield.").Looks(VfxStyle.Dash, 0.2f, new Color(0.65f, 0.9f, 1f)),
                // Freezes a whole area solid.
                Ab("Absolute Zero", Shape.PointArea, 9, 5, 55, 125, Magic(170, 110, ap: 0.9f), Root(1.8f))
                    .Ult().Says("Freezes a whole area solid.").Looks(VfxStyle.Field, 0.8f, new Color(0.7f, 0.92f, 1f)),
            }
        },
        new ChampionDef
        {
            Name = "Vashti Voidcaller", Role = Role.Mage, Color = new Color(0.5f, 0.2f, 0.7f), AttackRange = 7f,
            Base = St(hp: 505, mana: 390, ad: 50, armor: 22, mr: 30, aspd: 0.6f, ms: 5.6f, hpr: 1.1f, mpr: 1.4f),
            Growth = St(hp: 73, mana: 51, ad: 2.8f, armor: 3f, mr: 1.25f, aspd: 0.02f),
            Passive = Pas(PassiveKind.Aegis, "Void Ward", "After 5 s without attacking, casting or taking damage, gains a shield worth 10% of her max health.", a: 5f, b: 0.1f),
            Abilities = new[]
            {
                Ab("Void Bolt", Shape.Skillshot, 10, 1.2f, 6, 55, Magic(70, 45, ap: 0.7f), Silence(0.8f))
                    .Says("Fires a bolt of void that damages and briefly silences the first enemy hit.").Looks(VfxStyle.Bolt, 0.3f, new Color(0.65f, 0.3f, 0.95f)),
                // Tears a rift that drags enemies into its centre.
                Ab("Rift", Shape.PointArea, 8, 3, 11, 65, Pull(3f), Magic(50, 35, ap: 0.5f))
                    .Says("Tears a rift that drags enemies into its centre.").Looks(VfxStyle.Nova, 0.35f, new Color(0.65f, 0.3f, 0.95f)),
                // Drains a target and heals her for part of it.
                Ab("Soul Siphon", Shape.TargetEnemy, 8, 0, 9, 55, Magic(60, 40, ap: 0.55f).Vamps(0.6f))
                    .Says("Drains a target and heals her for part of it.").Looks(VfxStyle.Impact, 0.35f, new Color(0.65f, 0.3f, 0.95f)),
                // A black hole: drags everything in, damages and stuns.
                Ab("Singularity", Shape.PointArea, 9, 5, 60, 130, Pull(4.5f), Magic(160, 100, ap: 0.85f), Stun(0.8f))
                    .Ult().Says("A black hole: drags everything in, damages and stuns.").Looks(VfxStyle.Nova, 0.9f, new Color(0.6f, 0.25f, 0.9f)),
            }
        },
        // ===================== SUPPORTS =====================
        new ChampionDef
        {
            Name = "Oren Lanternkeeper", Role = Role.Support, Color = new Color(1f, 0.8f, 0.4f), AttackRange = 6f,
            Base = St(hp: 535, mana: 390, ad: 49, armor: 28, mr: 34, aspd: 0.62f, ms: 5.8f, hpr: 1.3f, mpr: 2f),
            Growth = St(hp: 79, mana: 54, ad: 2.7f, armor: 3.5f, mr: 1.25f, aspd: 0.02f),
            Passive = Pas(PassiveKind.Spellrunner, "Guiding Step", "Casting an ability grants +1.2 move speed for 2.5 s.", a: 1.2f, b: 2.5f),
            Abilities = new[]
            {
                Ab("Lantern Toss", Shape.Skillshot, 10, 1.3f, 7, 55, Magic(50, 30, ap: 0.5f), Slow(0.3f, 1.5f))
                    .Says("Tosses a lantern that damages and slows the first enemy it hits.").Looks(VfxStyle.Bolt, 0.25f, new Color(1f, 0.8f, 0.4f)),
                // Hastens and shields one ally.
                Ab("Guiding Light", Shape.TargetAlly, 9, 0, 10, 60, Buff(St(ms: 1.5f, aspd: 0.3f), 4f, Affects.Allies), Shield(50, 30, ap: 0.35f))
                    .Says("Hastens and shields one ally.").Looks(VfxStyle.Heal, 0.15f, new Color(1f, 0.85f, 0.45f)),
                // A ward on the ground: heals allies inside, slows enemies.
                Ab("Lantern Ward", Shape.PointArea, 8, 4, 12, 70, Heal(40, 25, ap: 0.4f), Slow(0.35f, 2f))
                    .Says("A ward on the ground: heals allies inside, slows enemies.").Looks(VfxStyle.Field, 0.2f, new Color(1f, 0.85f, 0.45f)),
                // Beacon of light: heals and shields everyone nearby.
                Ab("Beacon of Hope", Shape.SelfArea, 0, 9, 60, 130, Heal(120, 80, ap: 0.7f), Shield(80, 50, ap: 0.4f))
                    .Ult().Says("Beacon of light: heals and shields everyone nearby.").Looks(VfxStyle.Dawn, 0.6f, new Color(1f, 0.85f, 0.45f)),
            }
        },
        new ChampionDef
        {
            Name = "Pia Thistledown", Role = Role.Support, Color = new Color(0.5f, 0.9f, 0.5f), AttackRange = 6.5f,
            Base = St(hp: 530, mana: 380, ad: 48, armor: 27, mr: 33, aspd: 0.62f, ms: 6f, hpr: 1.3f, mpr: 2f),
            Growth = St(hp: 78, mana: 52, ad: 2.6f, armor: 3.4f, mr: 1.25f, aspd: 0.02f),
            Passive = Pas(PassiveKind.Sprint, "Light Footed", "After 3 s out of combat, gains +2 move speed until she attacks, casts or is hit.", a: 3f, c: 2f),
            Abilities = new[]
            {
                Ab("Thorn Dart", Shape.Skillshot, 10, 1, 6, 50, Magic(45, 28, ap: 0.5f), Slow(0.4f, 1.5f))
                    .Says("Shoots a thorn that damages and slows the first enemy hit.").Looks(VfxStyle.Arrow, 0.2f, new Color(0.5f, 0.95f, 0.5f)),
                // Heals one ally and keeps healing them for a few seconds.
                Ab("Regrowth", Shape.TargetAlly, 9, 0, 8, 65, Heal(60, 35, ap: 0.55f), Buff(St(hpr: 3f), 4f, Affects.Allies))
                    .Says("Heals one ally and keeps healing them for a few seconds.").Looks(VfxStyle.Heal, 0.15f, new Color(0.5f, 0.95f, 0.5f)),
                // Roots an area with vines.
                Ab("Entangle", Shape.PointArea, 8, 3, 12, 65, Root(1.5f), Magic(35, 25, ap: 0.4f))
                    .Says("Roots an area with vines.").Looks(VfxStyle.Trap, 0.3f, new Color(0.5f, 0.95f, 0.5f)),
                // A healing grove: heals and toughens every ally nearby.
                Ab("Grove's Embrace", Shape.SelfArea, 0, 8, 60, 130, Heal(130, 90, ap: 0.75f), Buff(St(armor: 20, mr: 20), 4f, Affects.Allies))
                    .Ult().Says("A healing grove: heals and toughens every ally nearby.").Looks(VfxStyle.Dawn, 0.55f, new Color(0.5f, 0.95f, 0.5f)),
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
            if (c.Passive == null) return $"{c.Name} has no passive";
            foreach (var a in c.Abilities)
            {
                if (!abilities.Add(a.Name)) return "Duplicate ability " + a.Name;
                if (a.Effects.Length == 0) return a.Name + " has no effects";
            }
        }
        return null;
    }
}
