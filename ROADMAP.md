# FreeWorld — Long-Term Game Roadmap
> Vision: A persistent online open-world simulator where players can build, craft, fight, live,
> trade, learn, and shape a shared reality. One massive map, infinite choices, no forced path.

---

## CURRENT STATE (Days 1–4)
- [x] Main menu, pause, loading screen
- [x] Dynamic crosshair, jump, surface textures
- [x] Procedural enemy bodies + CC0 humanoid model pipeline
- [x] Enemy AI (patrol, chase, attack, flank, cover, adaptive difficulty)
- [x] Shooting system (burst, reload, headshots, muzzle flash, tracers)
- [x] VFX (bullet holes, blood, sparks, bloom, vignette)
- [x] Wave spawner + kill counter + HUD
- [x] WeaponAudioBank — real CC0 gun sounds (pistol, rifle, shotgun), zero-latency WAV
- [x] 3D spatial audio on all enemies

---

## PHASE 1 — Solid Single-Player Foundation
> Goal: A fun, stable solo experience. All core verbs working.

### 1.1 Player Systems
- [x] Player health & armor (PlayerHealth.cs)
- [x] Stamina — drains on sprint, regens after delay (PlayerVitals.cs)
- [x] Hunger — passive drain, starvation damage at zero (PlayerVitals.cs)
- [x] Thirst — drains faster than hunger, dehydration damage at zero (PlayerVitals.cs)
- [x] Vitals HUD bars — STA / HNG / THR rendered at runtime, color-coded by level
- [x] Player inventory (grid-based, drag & drop)
- [x] Player stats: strength, speed, crafting skill, combat skill (level up over time)
- [ ] Player character customization (skin, clothing, face)
- [x] Save/load single-player game state (JSON or binary serialization)

### 1.2 World & Map
- [x] Large procedural terrain (Unity Terrain + multi-octave Perlin noise, 1000×1000)
- [x] Biome system: forest, desert, mountains, plains, wetlands (temperature × humidity map)
- [x] Day/night cycle with dynamic lighting (sun rotation, ambient, fog blending)
- [x] Weather system: rain, fog, snow, storm (biome-weighted transitions)
- [x] Minimap (overhead RenderTexture camera, circular UI, player dot)

### 1.3 Building & Construction
- [ ] Snap-based building grid (floor, wall, roof, door, window, stairs)
- [ ] Material tiers: wood → stone → metal → reinforced
- [ ] Damage & decay — structures degrade over time without maintenance
- [ ] Furniture & decoration objects
- [ ] Foundation stability physics (no floating castles)

### 1.4 Crafting
- [ ] Recipe system (ScriptableObject per recipe)
- [ ] Crafting bench tiers: hand → workbench → forge → electronics bench → lab
- [ ] Resource gathering: chop trees, mine rocks, harvest plants, scavenge ruins
- [ ] Tool durability — tools wear out and need repair
- [ ] Cooking system (raw food → cooked → buffs)

### 1.5 Combat Expansion
- [ ] Melee weapons (knife, axe, bat, sword)
- [ ] Ranged weapons (pistol, rifle, shotgun, bow, crossbow, sniper)
- [ ] Throwables (grenade, molotov, smoke)
- [ ] Armor system (helmet, vest, legs, boots — damage reduction per slot)
- [ ] Enemy factions with different behaviours (bandits, soldiers, wildlife, cultists)
- [ ] Wildlife: passive animals (deer, rabbit) + hostile (wolf, bear)
- [ ] Boss encounters tied to region progression

### 1.6 Economy & Trading (NPC)
- [ ] NPC traders in towns/outposts — buy and sell items
- [ ] Dynamic NPC pricing based on supply demand (simple formula)
- [ ] Quest system: fetch, kill, escort, build — rewards currency + XP
- [ ] Currency: physical coins/credits found in world or earned from quests

---

## PHASE 2 — Multiplayer Infrastructure
> Goal: The same world, shared between real players. Foundation must be solid before going wide.

### 2.1 Networking Layer
- [ ] Choose networking solution: Unity Netcode for GameObjects vs Mirror vs Fish-Net
- [ ] Dedicated server build (headless Unity)
- [ ] Client-server authoritative architecture (server owns all state — no cheating)
- [ ] Lag compensation for combat (hit registration)
- [ ] Basic anti-cheat: server-side validation of all position/damage/inventory changes
- [ ] Persistent world server — world stays alive when players log off

### 2.2 Player Persistence
- [ ] Account system: username + password (hashed, salted — bcrypt/Argon2, never plaintext)
- [ ] Server-side player save: inventory, position, stats, owned structures
- [ ] Session tokens (JWT or similar) — no credentials stored client-side
- [ ] Rate limiting on login endpoint — prevent brute force

### 2.3 World Synchronization
- [ ] Entity interest management — only sync what's near each player (spatial partitioning)
- [ ] Structure ownership synced to server DB
- [ ] Shared resource nodes — if player A chops tree, it's gone for player B
- [ ] Server-authoritative time (day/night same for all players)

### 2.4 Social Layer
- [ ] Player name tags (visible in world)
- [ ] Proximity voice chat (3D spatial, falls off with distance)
- [ ] Text chat (local / global / party channels)
- [ ] Friends list + party system
- [ ] Player inspection (see equipped gear)

---

## PHASE 3 — Regions & Factions
> Goal: The map feels alive with distinct zones, political power, and emergent conflict.

### 3.1 Region System
- [ ] Named regions on world map, each with unique biome + rules
- [ ] Region control — factions or player groups can "own" a region
- [ ] Ownership grants: tax income from NPC trade, resource spawn bonuses, fast-travel node
- [ ] Contested regions — periodic capture events (flag or structure-based)
- [ ] Safe zones (starter towns, no PvP) + open PvP zones + hardcore zones

### 3.2 Player Factions / Guilds
- [ ] Create or join a faction (name, tag, banner color)
- [ ] Faction rank system (recruit → member → officer → leader)
- [ ] Shared faction warehouse / storage
- [ ] Faction territory — claim land, build faction base
- [ ] Ally / enemy / neutral status between factions
- [ ] Faction leaderboard (territory held, kills, wealth)

### 3.3 NPC Factions
- [ ] 4–6 lore NPC factions with territory on spawn
- [ ] NPC factions patrol, defend, expand dynamically
- [ ] Players can align with NPC factions for quests + bonuses
- [ ] NPC factions can be wiped out by players (changes region loot/atmosphere)

---

## PHASE 4 — Economy & Society
> Goal: A real player-driven economy. Money flows because everyone needs something from someone else.

### 4.1 Player Market
- [ ] Player-to-player trading (direct trade window)
- [ ] Auction house / market board — post sell orders, browse buy orders
- [ ] Shop plots — players rent a stall in a town, set custom prices
- [ ] Currency sinks (crafting fees, fast travel cost, structure tax) to prevent inflation
- [ ] Item rarity tiers: common → uncommon → rare → epic → legendary (crafting or loot)

### 4.2 Professions
Players specialize. Not everyone fights.
- [ ] **Miner** — extracts raw ores faster, finds rare veins
- [ ] **Carpenter/Builder** — constructs structures faster, unlocks blueprints
- [ ] **Blacksmith** — crafts better weapons and armor
- [ ] **Farmer** — grows crops, breeds animals for food supply
- [ ] **Merchant** — opens own shop, gets trade XP bonus
- [ ] **Medic** — crafts better healing items, can revive downed players
- [ ] **Hacker/Engineer** — constructs electronics, traps, drones, automated turrets
- [ ] **Scout** — reveals map, tracks players, stealth bonuses
- [ ] Each profession has a 20-level skill tree (passive bonuses + active unlocks)

### 4.3 Land Ownership & Rent
- [ ] Claim plots with a deed item (bought or earned)
- [ ] Unclaimed land is public — anyone can build but not own
- [ ] Owned land: owner controls who can build/access
- [ ] Plot tax paid in currency to server — unpaid plot becomes claimable after X days
- [ ] Apartment/room renting between players

---

## PHASE 5 — Knowledge, Learning & Civilization
> Goal: Players can educate themselves in-game. The world has "technology levels" that everyone pushes forward together.

### 5.1 Research Tree (Civilization-wide)
- [ ] Global tech tree visible to all players
- [ ] Nodes unlock via "research points" contributed by any player at a Research Station
- [ ] Unlocks cascade: stone age → iron age → industrial → modern → future
- [ ] Each unlock adds new craftable items, structures, and mechanics for everyone on the server
- [ ] Some nodes require rare materials or completed quests to unlock — community effort

### 5.2 In-World Education
- [ ] Books / data pads found in world — readable, contain lore and crafting hints
- [ ] Scribeable notebooks — players write their own books, leave them in world
- [ ] Skill manuals — reading grants XP in a profession skill
- [ ] Schools / libraries (player-built) — standing near them gives passive XP regen

### 5.3 Redstone-style Logic (Late Phase 5)
- [ ] Wire components: switch, sensor, timer, logic gate (AND/OR/NOT/XOR)
- [ ] Connect to: doors, lights, traps, alarms, turrets, water pumps
- [ ] Enables: auto farms, base security systems, puzzle rooms, player-built machines
- [ ] Programmable controllers (simple scripting interface in-game)

---

## PHASE 6 — Politics & World Rules
> Goal: Players make the laws. The world has emergent governance.

### 6.1 Voting & Laws
- [ ] Region council — top N faction leaders by territory vote on region rules
- [ ] Votable rules per region: PvP on/off, tax rate, open/closed borders, contraband items
- [ ] Server-wide constitution — global rules voted on by all active players quarterly
- [ ] Law enforcement role — player sheriffs can arrest/detain in safe zones

### 6.2 Crime & Punishment
- [ ] Bounty system — players with high kill count get a bounty on their head
- [ ] Wanted level — guards attack on sight in NPC towns if wanted
- [ ] Prison mechanic — serve time (offline) or pay fine to clear record
- [ ] Smuggling — banned items can still be moved but risk confiscation

### 6.3 Diplomacy
- [ ] Alliance treaties (written contracts, signed by both faction leaders)
- [ ] Non-aggression pacts with expiry date
- [ ] Trade agreements (reduced market tax between allied factions)
- [ ] Declaration of war — faction war enabled, no penalties for killing each other

---

## PHASE 7 — Endgame & "Construct New Reality"
> Goal: The most dedicated players literally reshape the world.

### 7.1 Mega Structures
- [ ] Wonders of the world — massive collaborative builds requiring thousands of resources
- [ ] Each wonder gives a server-wide passive buff when complete
- [ ] Can be destroyed in war — losing a wonder removes its buff
- [ ] Examples: Grand Market (trade fees −10%), Iron Keep (spawn defense), the Archive (research speed +15%)

### 7.2 World Events
- [ ] Meteor strike — destroys a region, rare ore deposit appears in crater
- [ ] Faction invasion — a new hostile NPC faction spawns and attacks player territory
- [ ] Plague — spreads between players, requires medics to craft cure, changes region atmosphere
- [ ] Portal opening — dimensional rift spawns boss + exotic loot
- [ ] All events broadcast on world map + in-game news board

### 7.3 Server Seasons & Resets
- [ ] Season length: 3–6 months
- [ ] At season end: partial wipe (resources/structures reset, player skills + cosmetics kept)
- [ ] Season leaderboard rewards: unique cosmetic titles, banner colors, statue in next season's map
- [ ] Each season introduces one new biome or mechanic

### 7.4 Modding & Player-Created Content (Ultimate Endgame)
- [ ] Mod API — players write C# mods that add items, recipes, NPCs
- [ ] In-game mod workshop — browse and install community mods per server
- [ ] Custom quest editor — build and publish quests with dialogue, rewards, triggers
- [ ] Server owners choose which mods are enabled

---

## TECHNICAL ARCHITECTURE DECISIONS (To Resolve)

| Decision | Options | Status |
|---|---|---|
| Networking | Unity Netcode / Mirror / Fish-Net / custom | ⬜ Not decided |
| Backend / DB | Node.js+PostgreSQL / Nakama / PlayFab | ⬜ Not decided |
| Auth security | Argon2 hashing, JWT sessions, HTTPS only | ⬜ Plan defined |
| World size | Single terrain 16km² / streaming chunks | ⬜ Not decided |
| Map gen | Procedural per season / hand-crafted regions | ⬜ Not decided |
| Anti-cheat | Server-authoritative + anomaly detection | ⬜ Plan defined |
| Monetization | Cosmetics-only (never pay-to-win) | ✅ Decided |
| Platform target | PC (Windows/Linux) first, console later | ⬜ Not decided |

---

## RULES & DESIGN PRINCIPLES

1. **No pay-to-win.** Cosmetics only. Every gameplay advantage is earned in-world.
2. **Server authority.** All game state lives on the server. Clients render, never decide.
3. **No forced path.** Fighter, farmer, merchant, politician, architect — all equally valid.
4. **Consequence matters.** Death has cost. Destruction is real. Choices persist.
5. **Community makes the world.** Laws, factions, prices, and conflicts emerge from players.
6. **Accessibility.** New players can thrive in safe starter zones without being griefed.
7. **Transparency.** Open development, public roadmap, player voting on major features.

---

## MILESTONE TARGETS

| Milestone | Content | Target |
|---|---|---|
| **M1** | Playable solo demo (combat + build + craft loop) | Phase 1 complete |
| **M2** | 2–10 player co-op on LAN / private server | Phase 2 complete |
| **M3** | Public alpha — 50 player server, one region | Phase 3 partial |
| **M4** | Open beta — 200 players, full map, economy live | Phase 4 complete |
| **M5** | Season 1 launch — full feature set | Phases 5–6 complete |
| **M6** | Season 2 + modding tools | Phase 7 complete |

---

*Last updated: Day 4*
