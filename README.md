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
