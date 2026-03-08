# FreeWorld — FPS Game (Unity)

A **CS2 / PUBG-inspired** first-person shooter built with Unity.  
Designed for easy upgrades toward multiplayer, more game modes, and richer graphics.

---

## Project Structure

```
Assets/
├── Scripts/
│   ├── Player/
│   │   ├── PlayerController.cs   — Walk, sprint, crouch, jump (CharacterController)
│   │   ├── PlayerCamera.cs       — Mouse look, head bob, recoil recovery
│   │   └── PlayerHealth.cs       — HP, armor, death, respawn, regeneration
│   ├── Weapons/
│   │   ├── WeaponBase.cs         — Raycast shooting, recoil, ADS, reload, audio/VFX
│   │   ├── Shotgun.cs            — Extends WeaponBase with pellet spread
│   │   └── WeaponManager.cs      — Inventory (up to 5 slots), scroll/number switching
│   ├── Enemy/
│   │   ├── EnemyAI.cs            — NavMesh FSM: Idle → Patrol → Chase → Attack
│   │   └── EnemyHealth.cs        — HP, death, score reward, hit effects
│   ├── Managers/
│   │   ├── GameManager.cs        — Singleton: rounds, score, pause, game states
│   │   └── UIManager.cs          — HUD: health, ammo, crosshair, kill feed, screens
│   └── Utilities/
│       ├── IDamageable.cs        — Shared damage interface
│       ├── ObjectPool.cs         — Generic pooling for bullets/VFX
│       ├── EnemySpawner.cs       — Wave-based enemy spawning (scales per round)
│       └── Pickup.cs             — Health / Armor / Ammo collectibles
```

---

## Getting Started

### Requirements
| Tool | Version |
|------|---------|
| Unity | 2022.3 LTS (or newer) |
| Render Pipeline | URP (recommended for nice visuals) |
| TextMeshPro | Install via **Window → Package Manager → TMP Essentials** |
| AI Navigation | Install via **Window → Package Manager → AI Navigation** |

---

## Scene Setup (Step-by-Step)

### 1. Create the Player

1. **GameObject → 3D Object → Capsule** → rename to `Player`
2. Set **Tag** to `Player`
3. Add components:
   - `CharacterController` (Height: 1.8, Center Y: 0.9)
   - `PlayerController` (set Ground Check child)
   - `PlayerHealth`
4. Create empty child: `GroundCheck` — position at (0, -0.9, 0)
5. Assign `GroundCheck` to `PlayerController → Ground Check`

### 2. Setup FPS Camera

1. Create a **Camera** as child of Player at `(0, 0.75, 0)`
2. Attach `PlayerCamera` — drag the Player root into `Player Body`

### 3. Add a Weapon

1. Create an empty child under Camera: `WeaponHolder`
2. Create a Cube child: `AK47_Model` — position at `(0.2, -0.15, 0.5)`, scale small
3. Attach `WeaponBase` (or a subclass) to the weapon GameObject
4. Fill in Inspector: damage, fire rate, sounds, muzzle point Transform
5. Attach `WeaponManager` to Player root — drag AK47 into slot 0

### 4. Bake NavMesh (for Enemy AI)

1. Open **Window → AI → Navigation**
2. Select your floor/level geometry → check **Navigation Static**
3. Hit **Bake**

### 5. Create an Enemy

1. Create a Capsule → rename `Enemy`
2. Set **Tag** to `Enemy`
3. Add a child `Head` Capsule → set **Tag** to `Head` (headshot detection)
4. Add components:
   - `NavMeshAgent`
   - `EnemyAI` — assign patrol points, player layer, obstacle mask
   - `EnemyHealth`
5. Set `Player Layer` to the layer your Player is on

### 6. Wire up GameManager & UIManager

1. Create empty GameObject: `GameManager` — attach `GameManager`
2. Build a **Canvas** (Screen Space Overlay):
   - HealthBar (Slider), AmmoText (TMP), etc.
3. Attach `UIManager` to the Canvas — drag in all the UI references

---

## Controls

| Action | Key |
|--------|-----|
| Move | WASD |
| Sprint | Hold Left Shift |
| Crouch | Left Ctrl (toggle) |
| Jump | Space |
| Shoot | Left Mouse Button |
| ADS | Right Mouse Button |
| Reload | R |
| Switch Weapon | Scroll Wheel / 1–5 |
| Pause | Escape |

---

## Upgrade Roadmap

| Phase | Feature |
|-------|---------|
| **v0.2** | Grenade, knife melee, multiple maps |
| **v0.3** | Weapon recoil pattern (like CS2), bullet decals, shell casings |
| **v0.4** | Ragdoll death physics, enemy animations, IK |
| **v0.5** | Buy menu + economy (CS2 style) |
| **v1.0** | Multiplayer via Unity Netcode for GameObjects / Mirror |

---

## Tips for Great Graphics (URP)

- Enable **SSAO**, **Bloom**, and **Motion Blur** in the URP Renderer
- Use a **Post-Process Volume** on your camera
- Add **Fog** for atmosphere (`Lighting → Environment`)
- Download free assets from **Unity Asset Store**: poly packs, weapon models, character rigs
- Use **Shader Graph** for custom weapon shaders

---

*Built with GitHub Copilot — FreeWorld FPS Engine*
