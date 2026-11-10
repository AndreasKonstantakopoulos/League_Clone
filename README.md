# League Clone

A 3v3 MOBA for playing with friends: two lanes, a jungle between them, towers, a Nexus, minion waves every 20 seconds, gold, items, and 20 champions with 4 abilities each.

Open this folder with Unity 6 LTS in Unity Hub. Packages resolve on first open; create the URP pipeline asset then (Project Settings > Graphics).

## Playing (solo prototype, M2-M5)

1. In Unity run `Tools > Build Greybox Map` (once; re-run to rebuild). Create the URP asset first if the map is pink.
2. Open `Assets/Scenes/ChampSelect.unity` and press Play. Pick a champion and press Start (the match is the separate `Map` scene).
3. Right-click: move / attack. Q W E R: abilities (aim with the mouse). `+ rank` buttons spend skill points. B: recall (6 s channel, cancelled by moving, casting or damage). P or click the gold shopkeeper at your base: open the shop anywhere, but buying and selling only work inside your base.
4. Minions spawn every 20 s per lane. Destroy the outer then inner towers in a lane, then the Nexus.

Content (champions, abilities, items) is plain tables in `Assets/Scripts/Data/Content.cs`.

### Icons and effects

- Champion icons are drawn in code (champion colour, role-coloured ring, initials). To use real art, put a PNG named exactly like the champion (e.g. `Orrin Ironwall.png`) in `Assets/Resources/Icons/`; it replaces the placeholder automatically.
- Auto attacks are small and pale: a short slash for melee, a thin bolt for ranged. Abilities are bigger and coloured. Each ability picks a style with `.Looks(VfxStyle, power, tint)` in `Content.cs`: Slash, Whirl, Slam, Dome, Aura, Dash, Strike, Arrow, Bolt, Beam, Trap, Nova, Field, Storm, Heal, Dawn or Impact. `power` (0 to 1) scales the effect and the camera shake. The recipes are in `Assets/Scripts/Core/Vfx.cs`.
- Hits also show what they did: stars over stunned targets, vines for roots, frost for slows, a purple ring for silence.

### Tooltips

Hover an ability in the bottom bar for its shape, range, cooldown (with item cooldown reduction) and real damage/heal/shield numbers, split into base and the part that comes from AD/AP. Hover a shop item to see what it adds and how much extra each of your abilities gets from it.

The map size is one constant (`K` in `Assets/Editor/BuildGreyboxMap.cs`, now 1.6 = 160x96 m). Change it and re-run `Tools > Build Greybox Map`.

### Jungle (M7)

- Four camps (a Brute and two Imps each) spawn at 15 s and respawn 60 s after being cleared. They pay gold and XP, and fight back if you get close or hit them. They walk home and heal if you pull them too far.
- Two big objectives sit in the middle: the Ember Drake (spawns at 1:30, respawns every 3 min) and the Void Titan (spawns at 4:00, respawns every 5 min). Both stomp nearby enemies. Killing one gives your whole team a timed buff plus gold, and a banner announces it. Timers are on the top-left of the screen. All the timings are constants in `Rules` (`GameManager.cs`) and are short on purpose for testing.
- Monster stats and the buffs are tables at the top of `Assets/Scripts/Objectives/JungleManager.cs`.

### Playing with friends (online)

- In champion select use **Play with friends**. One player presses **Host a game** and gets a short **join code** (with a Copy button). Friends type the code and press **Join**. Nobody sees anyone's IP: traffic goes through Unity Relay. Players alternate Blue, Red, Blue... and the host presses **Start match**. Up to 6 players.
- One-time setup (needs your Unity account, I can't do it for you): sign in to the Unity Hub/Editor, link the project to a Unity Cloud project (Project Settings > Services), and make sure Relay is enabled for it in the Unity Dashboard. Anonymous sign-in is used, so players need no accounts.
- The host runs the whole game. Clients send commands (move, attack, cast, recall, buy, sell, rank) and mirror what the host sends: champions, minions, monsters, towers, health bars, objectives, buffs and all particle effects. Your own gold, cooldowns and items are sent privately to you.
- The old **Direct** row (IP and port) still works for LAN play, but it shares the host's IP, so use the code for friends over the internet.
- Code: `Assets/Scripts/Networking/` (`Net.cs` has the lobby, commands, snapshots and effect messages).

### Passives

Every champion has a passive, defined next to the abilities in `Content.cs` (`Pas(...)`) and run by `Champion.TickPassive`. They are shown in the champion-select screen and as the **P** slot left of Q in the HUD (lit while active, with stack count; hover for details).
Orrin: Stonebound (armor and magic resist per nearby enemy). Kael: Undying Rage (below 40% health: damage, attack speed, regeneration). Vex: Shadow Veil (invisible after 3 quiet seconds, regenerates and moves faster; attacking, casting or taking damage reveals him; enemies cannot target him). Sera: Steady Aim (stacking damage on one target). Ilyra: Arcane Charge (next attack after a spell deals bonus magic damage and refunds mana). Mirel: Dawn's Grace (regenerates nearby allies).

### Roster (M6)

20 champions, 80 abilities, each with a passive and its own effect look: 4 tanks (Orrin Ironwall, Grum Boulderfist, Seren Wardenheart, Brannoch Stormshield), 3 bruisers (Kael Ashblade, Dara Bloodmoon, Hrolf Wavebreaker), 3 assassins (Vex Nightshade, Riku Silentstep, Zephyra Quickblade), 3 marksmen (Sera Longshot, Cassian Redhawk, Marek Emberstring), 4 mages (Ilyra Stormcaller, Nyx Emberweaver, Quillon Frostweaver, Vashti Voidcaller) and 3 supports (Mirel Dawnwhisper, Oren Lanternkeeper, Pia Thistledown). All of it is table data in `Assets/Scripts/Data/Content.cs`; passives are run by `Champion.TickPassive` (14 kinds, see `PassiveKind`).

### Fog of war

Only your own side gives vision: your champion (12 m), minions (8 m), towers (13 m) and Nexus (11 m). Anything else is hidden while it is outside that vision: enemy champions, minions, jungle monsters and enemy towers and Nexus, until you scout them. The ground you cannot see is darkened, and spell, attack and tower-shot effects out of sight are not drawn. Hidden buildings are not remembered once you move away: they disappear again. In online games the host also stops sending hidden units and their effects, so a modified client cannot see through the fog (enemy buildings are always sent, since a destroyed one must disappear for everyone, but the client only draws them in vision). Jungle walls block vision, except that standing close to a wall you can see a few metres past it (the walls are thin); the deep middle of the jungle stays dark until you walk in. The two distances are constants at the top of `Vision.cs`. Rules are in `Assets/Scripts/Core/Vision.cs`, the overlay is `Fog.cs`.

### Detection and the Nexus

Towers and the Nexus detect invisible champions within their vision range: the champion is drawn as a see-through ghost for the detecting team, can be targeted while revealed, and the structures shoot it. This works for every tower, including inner ones that cannot be attacked yet. The Nexus also fires at enemies near it (11 m, 150 damage a second). Tower and Nexus numbers: `Tower.Configure` and `GameManager.MakeStructure`.

### Map layout and minimap

Re-run `Tools > Build Greybox Map` to get the layout, which follows Twisted Treeline: a wide field mirrored left to right. Each base has its Nexus and an S-shaped ridge closing off its inner side, so it opens onto the two lanes. The top lane leaves the base, runs down and across under a big walled alcove at the top centre (where the Ember Drake pit sits). The bottom lane runs flat along the bottom edge. Between them is the jungle: a wall under the top lane, two diagonal walls, and a row of walls above the bottom lane, with the four camps and the Void Titan pit in the pocket between the diagonals. The lane and wall coordinates are listed at the top of `Assets/Editor/BuildGreyboxMap.cs`.

The minimap is in the bottom-right corner: it shows what you can see (fog included), your champion with a white ring, and a white box for what your camera sees. Click it with the left or right mouse button and your champion walks there. Clicks on the minimap, the bottom panel and the shop no longer move your champion in the world.

### Lag

The host simulates everything, so a joining player normally sees everything about one round trip late. To hide that: your own walking is predicted on your machine (it walks right away on the same navmesh, and the host's position corrects it if you disagree), the host sends updates 30 times a second, and clients see a **Ping** readout top-right. If the ping is low but the game still stutters, that machine is the bottleneck (try lower graphics quality or a lower resolution); if the ping is high, the connection is. Attacks, abilities and dashes are not predicted yet.

### Ability tooltips

Hover an ability for: its key and name with the rank, cooldown, mana and range, a one-sentence summary of what it does, the live damage/heal/shield numbers (with the part that comes from your AD/AP), and per-rank tables with the current rank highlighted. The summary is written with `.Says("...")` in `Content.cs`; if an ability has none, one is generated from its effects.
