# League Clone

A 3v3 MOBA for playing with friends: two lanes, a jungle between them, towers, a Nexus, minion waves every 20 seconds, gold, items, and 20 champions with 4 abilities each.

Open this folder with Unity 6 LTS in Unity Hub. Packages resolve on first open; create the URP pipeline asset then (Project Settings > Graphics).

## Playing (solo prototype, M2-M5)

1. In Unity run `Tools > Build Greybox Map` (once; re-run to rebuild). Create the URP asset first if the map is pink.
2. Open `Assets/Scenes/ChampSelect.unity` and press Play. Pick a champion and press Start (the match is the separate `Map` scene).
3. Right-click: move / attack. Q W E R: abilities (aim with the mouse). `+ rank` buttons spend skill points. B: shop while standing at your base.
4. Minions spawn every 20 s per lane. Destroy the outer then inner towers in a lane, then the Nexus.

Content (champions, abilities, items) is plain tables in `Assets/Scripts/Data/Content.cs`.

### Icons and effects

- Champion icons are drawn in code (champion colour, role-coloured ring, initials). To use real art, put a PNG named exactly like the champion (e.g. `Orrin Ironwall.png`) in `Assets/Resources/Icons/`; it replaces the placeholder automatically.
- Auto attacks spawn a few small pale sparks. Abilities spawn bigger bursts coloured by effect (orange physical, purple magic, green heal, cyan shield, yellow crowd control), plus rings for area abilities and trails for skillshots. See `Assets/Scripts/Core/Vfx.cs`.

### Tooltips

Hover an ability in the bottom bar for its shape, range, cooldown (with item cooldown reduction) and real damage/heal/shield numbers, split into base and the part that comes from AD/AP. Hover a shop item to see what it adds and how much extra each of your abilities gets from it.
