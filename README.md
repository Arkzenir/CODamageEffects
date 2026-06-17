# CO Damage Effects

A server-side Vintage Story mod that applies configurable **status effects** to players based on the damage they receive. Requires the **Overhaul Library** family: `overhaullib` on VS 1.21.x, and `overhaulliblegacycompat` (Overhaul Library — Legacy Compat) on VS 1.22.x.

---

## Dependencies

| Mod | Required | Notes |
|---|---|---|
| [Overhaul Library](https://mods.vintagestory.at/overhaullib) (`overhaullib`) | **Yes (VS 1.21.x)** | Provides `PlayerDamageModelBehavior` and `OnReceiveDamage`. VS 1.21.x → v1.20.x+ |
| Overhaul Library — Legacy Compat (`overhaulliblegacycompat`) | **Yes (VS 1.22.x)** | In VS 1.22 the `CombatOverhaul.*` API the mod consumes was split out of `overhaullib` 2.x into this legacy-compat assembly (`OverhaullibLegacyCompat.dll`). Provides the same `PlayerDamageModelBehavior`, `OnReceiveDamage`, `IWeaponDamageSource`, `PlayerBodyPart`, and `MeleeWeapon*` types under the unchanged `CombatOverhaul.*` namespaces. |
| [SlowTox](https://mods.vintagestory.at/slowtox) (`slowtox`) | No | Auto-detected; `Intoxication` integrates with it when present |

This mod is **universal** — it must be installed on both client and server.

---

## Effects

| Type | What it does | `Strength` meaning | `DurationSec` |
|---|---|---|---|
| `Bleed` | Deals `Strength` HP of Injury damage every second | HP per tick | Seconds |
| `Slow` | Subtracts `Strength` from the `walkspeed` stat | Speed units removed | Seconds |
| `Intoxication` | Disorients the player (see [SlowTox Compatibility](#slowtox-compatibility)) | Toxin/stat amount | Seconds |
| `Knockdown` | Near-total movement and jump lock | Unused — binary | Seconds |
| `Dismount` | Instantly ejects the player from any mount or seat | Unused | Unused — instant |
| `Poison` | Deals `Strength` HP of Poison damage every 2 seconds | HP per tick | Seconds |
| `Burning` | Deals `Strength` HP of Fire damage every second | HP per tick | Seconds |
| `DamageMultiplier` | Multiplies the triggering hit's damage by `Strength` (e.g. `1.25` = +25%). Instant — `DurationSec` is ignored | Damage multiplier | Unused — instant |
| `FlatDamage` | Adds `Strength` HP directly to the triggering hit. Instant — `DurationSec` is ignored | HP added to hit | Unused — instant |

Re-applying the same effect while it is already active **refreshes** it: duration is extended, strength is escalated if the new hit is stronger. The effect is never duplicated.

---

## Configuration

The config is split across three files in `VintagestoryData/ModConfig/`. On first run, defaults are written automatically. Reference copies of all three files are shipped with the mod under `assets/codamageeffects/config/`.

| File | Contents |
|---|---|
| `codamageeffects_general.json` | Global switches and healing-reduction settings |
| `codamageeffects_pve.json` | PvE rules and rule groups |
| `codamageeffects_pvp.json` | PvP rules and rule groups |

### General options

| Field | Default | Description |
|---|---|---|
| `EnablePvE` | `true` | Evaluate the PvE rule set when the attacker is an AI or mob |
| `EnablePvP` | `true` | Evaluate the PvP rule set when the attacker is a player |
| `EnableWeaponAttributeGating` | `true` | When `false`, all rule-level weapon attribute requirements (`RequireWeaponStackAttributes`, `RequireWeaponTypeAttributes`) are ignored |
| `UseSlowToxIfAvailable` | `true` | Route `Intoxication` through SlowTox's API when SlowTox is loaded |
| `EnableHealingReduction` | `true` | Using a healing item reduces active effect duration and strength, measured from the item's authored `health` value. Fires even when NoInCombatHealing blocks the restore |
| `HealingDurationReductionPerHp` | `2.0` | Seconds removed per HP (item-authored value mode) |
| `HealingStrengthReductionPerHp` | `0.05` | Strength removed per HP (item-authored value mode) |
| `EnableHealingReductionActualGain` | `false` | Using a healing item reduces active effects based on HP the player *actually gains*. Blocked heals produce no reduction. Both modes can be active simultaneously |
| `ActualGainDurationReductionPerHp` | `2.0` | Seconds removed per HP actually gained |
| `ActualGainStrengthReductionPerHp` | `0.05` | Strength removed per HP actually gained |

### Rule fields

Rules live in the top-level `Rules` array of each file. All matching rules apply — there is no first-match-wins behaviour.

| Field | Type | Default | Description |
|---|---|---|---|
| `DamageTypes` | string[] | `[]` (any) | Damage types that trigger this rule. See valid values below |
| `MinDamage` | float | `1.0` | Minimum post-armor damage required |
| `BodyParts` | string[] | `[]` (any) | Body parts that must be struck. See valid values below |
| `AttackSource` | string | `"Any"` | `Any` \| `Melee` \| `Ranged` |
| `Handedness` | string | `null` (any) | `MainHand` \| `OffHand`. Melee only; ignored for ranged hits |
| `WeaponCodes` | string[] | `[]` (any) | Collectible codes with single-`*` glob support, e.g. `"game:spear-*"` |
| `WeaponGrip` | string | `null` (any) | `TwoHandedOnly` \| `OneHandedOnly` \| `CanBeEither`. Melee only |
| `RequireWeaponStackAttributes` | object[] | `[]` (any) | All listed conditions must match the weapon's **stack instance** attributes (`ItemStack.Attributes`). Use for runtime tags set via `tagweapon` command or another mod. Gated by `EnableWeaponAttributeGating` |
| `RequireWeaponTypeAttributes` | object[] | `[]` (any) | All listed conditions must match the weapon's **item type** attributes (values baked into the item JSON). Gated by `EnableWeaponAttributeGating` |
| `AttackerMounted` | bool? | `null` (any) | Require the attacker to be mounted (`true`) or unmounted (`false`) |
| `TargetMounted` | bool? | `null` (any) | Require the target player to be mounted (`true`) or unmounted (`false`) |
| `ChancePct` | float | `100.0` | Probability (0–100) the rule fires when all other conditions are met |
| `Effects` | object[] | `[]` | Effects applied when this rule fires |

**Valid `DamageTypes`:** `PiercingAttack` `SlashingAttack` `BluntAttack` `Fire` `Poison` `Frost` `Electricity` `Heat` `Gravity` `Suffocation` `Hunger` `Crushing` `Injury` `Heal`

**Valid `BodyParts`:** `Head` `Face` `Neck` `Torso` `LeftArm` `RightArm` `LeftHand` `RightHand` `LeftLeg` `RightLeg` `LeftFoot` `RightFoot`

**`WeaponGrip` aliases:** `TwoHandedOnly` / `TwoHanded` / `2H` — `OneHandedOnly` / `OneHanded` / `1H` — `CanBeEither` / `Either`

### Effect fields

| Field | Type | Default | Description |
|---|---|---|---|
| `Type` | string | `"Bleed"` | Effect type name (see Effects table above) |
| `Strength` | float | `1.0` | Effect intensity (meaning varies by type) |
| `DurationSec` | float | `5.0` | Duration in seconds. Ignored by instant effects (`Dismount`) |

### Example rule

```json
{
  "DamageTypes": [ "SlashingAttack" ],
  "MinDamage": 3.0,
  "BodyParts": [ "Torso", "LeftArm", "RightArm" ],
  "AttackSource": "Melee",
  "WeaponGrip": "TwoHandedOnly",
  "ChancePct": 40.0,
  "Effects": [
    { "Type": "Bleed", "Strength": 1.0, "DurationSec": 12.0 }
  ]
}
```

---

## Rule Groups

Rule groups associate several rules with a shared pool of effects. The shared effects fire **once** when any rule in the group matches, regardless of how many matched. Per-rule `Effects` still apply normally on top.

Use this to avoid double-applying a common effect when multiple rules in a pattern could match the same hit.

```json
"RuleGroups": [
  {
    "Name": "Slashing limb bleeds",
    "SharedEffects": [
      { "Type": "Bleed", "Strength": 0.7, "DurationSec": 9.0 }
    ],
    "Rules": [
      {
        "DamageTypes": [ "SlashingAttack" ],
        "MinDamage": 2.0,
        "BodyParts": [ "LeftArm", "RightArm", "LeftHand", "RightHand" ],
        "ChancePct": 30.0,
        "Effects": []
      },
      {
        "DamageTypes": [ "SlashingAttack" ],
        "MinDamage": 2.0,
        "BodyParts": [ "LeftLeg", "RightLeg", "LeftFoot", "RightFoot" ],
        "ChancePct": 25.0,
        "Effects": []
      }
    ]
  }
]
```

---

## Weapon Attributes

Rule-level attribute requirements are gated by `EnableWeaponAttributeGating`. There are two distinct scopes:

| Scope | Field | Where it looks | When to use |
|---|---|---|---|
| **Stack** | `RequireWeaponStackAttributes` | `ItemStack.Attributes` — the individual item instance | Runtime tags set via `tagweapon` command, smithing skill, or another mod |
| **Type** | `RequireWeaponTypeAttributes` | `Collectible.Attributes` — baked into the item JSON | Properties that apply to every instance of the item type |

Both lists may appear on the same rule; all conditions in both lists must pass for the rule to fire.

### Rule-level example (stack attribute)

The `Key` in the config must be the full namespaced form. Use `/codamageeffects tagweapon envenomed true` to set it on a specific weapon (the command auto-prefixes to `codamageeffects:envenomed`).

```json
{
  "DamageTypes": [ "PiercingAttack", "SlashingAttack" ],
  "MinDamage": 1.0,
  "RequireWeaponStackAttributes": [
    { "Key": "codamageeffects:envenomed", "Value": "true" }
  ],
  "ChancePct": 100.0,
  "Effects": [
    { "Type": "Poison", "Strength": 1.0, "DurationSec": 20.0 }
  ]
}
```

Set `"Value": ""` to require only the key's presence (any non-null value passes):

```json
{ "Key": "quality", "Value": "" }
```

### Rule-level example (type attribute)

```json
{
  "DamageTypes": [ "SlashingAttack" ],
  "MinDamage": 2.0,
  "RequireWeaponTypeAttributes": [
    { "Key": "codamageeffects:fire-enchanted", "Value": "true" }
  ],
  "ChancePct": 80.0,
  "Effects": [
    { "Type": "Burning", "Strength": 1.0, "DurationSec": 8.0 }
  ]
}
```

### Setting type-level attributes (item JSON)

Applies to every instance of that item type. Matched by `RequireWeaponTypeAttributes`:

```json
{
  "attributes": {
    "codamageeffects:fire-enchanted": "true"
  }
}
```

### Setting stack-level attributes (runtime)

Matched by `RequireWeaponStackAttributes`. Set via admin command:

```
/codamageeffects tagweapon poisoned true
/codamageeffects untagweapon poisoned
```

Keys that contain no `:` are automatically prefixed with `codamageeffects:`, so `poisoned` and `codamageeffects:poisoned` are identical. The full namespaced form is also accepted:

```
/codamageeffects tagweapon codamageeffects:poisoned true
```

Or from another mod:

```csharp
itemStack.Attributes.SetString("codamageeffects:poisoned", "true");
itemSlot.MarkDirty();
```

---

## Item Tooltip Tags

Tagged items show a visible label at the bottom of their tooltip so players can see at a glance what CODamageEffects conditions apply.

**Stack tags** (set at runtime via `tagweapon`) appear in orange:

> <span style="color:#ff8844">CDE: poisoned = true</span>

**Type tags** (baked into the item JSON) appear in blue:

> <span style="color:#44aaff">CDE type: fire-enchanted = true</span>

Tags are shown automatically — no config required. Only attributes whose keys begin with `codamageeffects:` are displayed; other item attributes are not affected.

To make a type tag visible in the tooltip, use the `codamageeffects:` prefix when writing the attribute in the item JSON:

```json
{
  "attributes": {
    "codamageeffects:fire-enchanted": "true"
  }
}
```

---

## Handedness

`Handedness` filters by which hand the attacking weapon was wielded in. Melee only — silently ignored for ranged hits.

- `"MainHand"` — right hand slot
- `"OffHand"` — left hand slot
- Omit or `null` — either hand, including no-weapon hits

Resolution compares the weapon `ItemStack` from the OverhaulLib damage source against the attacker's known hand slots. Environmental damage and other non-weapon sources always pass.

---

## Ranged Hits

Set `"AttackSource": "Ranged"` to restrict a rule to projectile damage. Detection is based on OverhaulLib's damage source: a ranged hit has the projectile entity as `SourceEntity` and the shooter as `CauseEntity`; a melee hit has the attacker as both.

`WeaponCodes` for ranged rules matches the **launcher** (bow, sling…), not the projectile item. `Handedness` and `WeaponGrip` are silently ignored on ranged rules.

```json
{
  "DamageTypes": [ "PiercingAttack" ],
  "MinDamage": 3.0,
  "BodyParts": [ "LeftArm", "RightArm", "LeftLeg", "RightLeg" ],
  "AttackSource": "Ranged",
  "ChancePct": 50.0,
  "Effects": [
    { "Type": "Bleed", "Strength": 0.6, "DurationSec": 8.0 }
  ]
}
```

---

## Mount Conditions

`AttackerMounted` and `TargetMounted` filter on whether each party is currently riding a mount. Set to `true` to require mounted, `false` to require unmounted. Omit or `null` to match either.

**Mounted attacker charge bleed:**

```json
{
  "DamageTypes": [ "PiercingAttack" ],
  "MinDamage": 5.0,
  "AttackerMounted": true,
  "ChancePct": 75.0,
  "Effects": [
    { "Type": "Bleed", "Strength": 2.0, "DurationSec": 15.0 }
  ]
}
```

**Mounted target dismount pressure:**

```json
{
  "DamageTypes": [ "BluntAttack" ],
  "MinDamage": 4.0,
  "TargetMounted": true,
  "ChancePct": 60.0,
  "Effects": [
    { "Type": "Dismount", "Strength": 1.0, "DurationSec": 0.0 }
  ]
}
```

---

## SlowTox Compatibility

If [SlowTox](https://mods.vintagestory.at/slowtox) is installed, the `Intoxication` effect automatically routes through SlowTox's public API:

| Without SlowTox | With SlowTox |
|---|---|
| Writes the vanilla `intoxication` entity stat | Adds `Strength` to `slowtox:newToxins` |
| Clears the stat on expiry | Adds the same amount to `slowtox:detoxicants` on expiry |

The effect then participates in SlowTox's tolerance, metabolism (sleepable), and benefit/penalty system. Detection is automatic — SlowTox is not a required dependency.

The `Slow` and `Knockdown` effects use namespaced stat keys (`codamageeffects:slow`, `codamageeffects:knockdown-walk`, `codamageeffects:knockdown-jump`) and do not conflict with SlowTox's own walkspeed penalty.

Set `"UseSlowToxIfAvailable": false` to always use the vanilla `intoxication` stat even when SlowTox is present.

---

## Healing Reduction Mode

Two independent modes control how healing reduces active effects. Both can be enabled simultaneously for combined reduction.

### `EnableHealingReduction` — item-authored value (default: `true`)

At startup, the mod injects a `CollectibleBehavior` into every item that carries `BehaviorHealingItem`. When such an item is used, the reduction fires immediately using the item's authored `health` value — independent of whether the heal was actually blocked (e.g. by **NoInCombatHealing**). This is the default mode and the most NoInCombatHealing-friendly option.

**Limitations:**
- Only covers items with `BehaviorHealingItem`. Passive regen and food are not captured.
- For heal-over-time items (bandages with `EffectDurationSec > 0`), vanilla converts the initial heal into a DoT sequence internally and the initial application does not fire the behavior; only per-tick ticks do. HoT item usage will not trigger a full reduction on application.

### `EnableHealingReductionActualGain` — actual HP gained (default: `false`)

The mod polls the player's HP each tick. If HP increased since the last tick, the difference is used as the heal amount. This covers all healing sources (items, passive regen, food) but produces zero reduction when a mod like NoInCombatHealing prevents actual HP gain.

---

## Admin Commands

All subcommands require the `commandplayer` privilege. Actions are logged to the server log with the caller's name.

### Effect management

| Command | Description |
|---|---|
| `/codamageeffects apply <player> <effect> [strength] [duration]` | Apply an effect directly to an online player |
| `/codamageeffects remove <player> <effect>` | Remove a specific active effect from a player |
| `/codamageeffects removeall <player>` | Remove all active effects from a player |
| `/codamageeffects list <player>` | List all currently active effects on a player |

**Effect names:** `Bleed` `Slow` `Intoxication` `Knockdown` `Poison` `Burning` `Dismount`

```
/codamageeffects apply Alice Bleed 1.5 10
/codamageeffects remove Alice Bleed
/codamageeffects removeall Alice
/codamageeffects list Alice
```

### Weapon tagging

Tag the item **you are currently holding** with a custom key-value attribute. The tag persists on that specific item stack. Rules using `RequireWeaponStackAttributes` will check these tags, and tagged items display a coloured label in their tooltip.

| Command | Description |
|---|---|
| `/codamageeffects tagweapon <key> <value>` | Add or overwrite an attribute on the held item |
| `/codamageeffects untagweapon <key>` | Remove an attribute from the held item |

Keys with no `:` are automatically prefixed with `codamageeffects:`, so the short form is preferred in chat:

```
/codamageeffects tagweapon poisoned true
/codamageeffects untagweapon poisoned
```

The full namespaced form is identical and also accepted:

```
/codamageeffects tagweapon codamageeffects:poisoned true
```

---

## Building

1. Copy `Properties/localSettings.props` and fill in `<GameDirectory>` (or set the
   `VINTAGE_STORY` environment variable).
2. Ensure `overhaullib.dll` is at `$(OverhaulLibDir)/overhaullib.dll`.
3. `dotnet build` — output in `bin/Debug/Mods/damageeffects/`.
4. `dotnet build -c Release` — also produces `Releases/damageeffects_1.0.0.zip`.
