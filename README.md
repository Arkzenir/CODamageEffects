# Damage Effects

A server-side Vintage Story mod that applies configurable **status effects** to players based
on the damage they receive. Requires **Overhaul Library** (`overhaullib`).

---

## Effects

| Type | What it does | `Strength` | `DurationSec` |
|---|---|---|---|
| `Bleed` | Deals `Strength` HP of Injury damage every second | HP per tick | Duration in seconds |
| `Slow` | Subtracts `Strength` from the `walkspeed` stat | Speed units removed | Duration in seconds |
| `Intoxication` | Disorients the player (see SlowTox note) | Toxin/stat amount | Duration in seconds |
| `Knockdown` | Near-total movement and jump lock | Unused — binary | Duration in seconds |
| `Dismount` | Instantly ejects the player from any mount or seat | Unused — binary | Unused — instant |
| `Poison` | Deals `Strength` HP of Poison damage every 2 seconds | HP per tick | Duration in seconds |
| `Burning` | Deals `Strength` HP of Fire damage every second | HP per tick | Duration in seconds |

Re-applying the same effect while it is already active **refreshes** it (extends duration,
escalates strength if higher) rather than creating a duplicate.

---

## Configuration

On first run, defaults are written to `VintagestoryData/ModConfig/damageeffects.json`.

### Top-level options

| Field | Default | Description |
|---|---|---|
| `EnableWeaponAttributeGating` | `true` | When `false`, all `RequireWeaponAttribute` conditions on individual effects are ignored — effects apply regardless of weapon attributes |

### Rule fields

```jsonc
{
  "Rules": [
    {
      // Damage types that trigger this rule. Empty = any type.
      // Valid: PiercingAttack | SlashingAttack | BluntAttack | Fire | Poison |
      //        Frost | Electricity | Heat | Gravity | Suffocation | Hunger |
      //        Crushing | Injury | Heal
      "DamageTypes": ["SlashingAttack"],

      // Minimum post-armor damage required. Default: 1.0
      "MinDamage": 3.0,

      // Whether this rule fires on melee hits, ranged hits, or both. Default: Any
      // Valid: Any | Melee | Ranged
      //   Melee  — direct hits only (attacker == damage source entity)
      //   Ranged — projectile hits only (projectile entity is the damage source)
      //   Any    — both (default)
      // Note: Handedness and WeaponGrip are melee-only and are ignored for Ranged rules.
      // For Ranged rules, WeaponCodes matches the launcher (bow/sling), not the projectile.
      "AttackSource": "Melee",

      // Body parts that must be struck. Empty = any part.
      // Valid: Head | Face | Neck | Torso | LeftArm | RightArm | LeftHand |
      //        RightHand | LeftLeg | RightLeg | LeftFoot | RightFoot
      "BodyParts": ["Torso", "LeftArm"],

      // Required hand the weapon is held in. Null/omitted = either hand.
      // Valid: MainHand | OffHand
      // Only enforced when the damage source carries hand information (melee attacks).
      // Environmental or unknown-source damage always passes this check.
      "Handedness": "MainHand",

      // Collectible codes the weapon must match. Empty = any weapon (including none).
      // Supports a single * wildcard. Examples: "game:sword-iron", "game:spear-*", "*:dagger-*"
      // Only enforced when a weapon is actually present in the damage source.
      "WeaponCodes": ["game:spear-*"],

      // Filter by weapon grip capability read from MeleeWeaponStats. Null/omitted = any weapon.
      // Valid: TwoHandedOnly | OneHandedOnly | CanBeEither
      //   TwoHandedOnly — has TwoHandedStance but no OneHandedStance (greatswords, halberds…)
      //   OneHandedOnly — has OneHandedStance but no TwoHandedStance (maces, daggers…)
      //   CanBeEither   — has both stances defined (longswords, bastard swords…)
      // Aliases: TwoHanded / 2H, OneHanded / 1H, Either
      // Only enforced for OverhaulLib MeleeWeapon items. Non-MeleeWeapon hits always pass.
      "WeaponGrip": "TwoHandedOnly",

      // Chance (0–100) this rule fires when all other conditions are met. Default: 100
      "ChancePct": 40.0,

      "Effects": [
        {
          "Type": "Bleed",
          "Strength": 1.0,
          "DurationSec": 12.0,

          // Optional: only apply this effect if the attacking weapon carries a specific
          // attribute. Checked on both the item type (JSON) and the item stack instance
          // (runtime-set). Requires EnableWeaponAttributeGating = true (default).
          "RequireWeaponAttribute": {
            "Key": "damageeffects:poisoned",
            // Optional expected value. Empty string = just require the key's presence.
            "Value": "true"
          }
        }
      ]
    }
  ]
}
```

All matching rules apply — there is no first-match-wins behaviour. Invalid enum values in
`DamageTypes` or `BodyParts` are skipped with a server log warning.

---

## Weapon Attributes

Effects can be gated on a named attribute present on the attacking weapon, enabling
per-item-stack behaviours like poisoned weapons:

**From JSON** (applies to every instance of the item type):
```json
{
  "attributes": {
    "damageeffects:poisoned": "true"
  }
}
```

**At runtime** (e.g. from another mod or a command):
```csharp
slot.Itemstack.Attributes.SetString("damageeffects:poisoned", "true");
```

The key name is entirely up to you — `damageeffects:poisoned` is just the convention used in
the default config. Any string key/value pair works.

Set `EnableWeaponAttributeGating: false` in the config to disable all attribute checks
globally (all effects always apply when other rule conditions are met).

---

## Handedness

The `Handedness` rule field filters by which hand the attacking weapon was wielded in.
This works by comparing the weapon `ItemStack` from the OverhaulLib damage source against
the attacker's known left/right hand slots:

- `"MainHand"` — requires the weapon to be in the attacker's right hand slot
- `"OffHand"` — requires the weapon to be in the attacker's left hand slot
- Omit the field (or set to `null`) — either hand, including no-weapon hits

**Note:** Handedness is only resolvable for melee attacks sourced through OverhaulLib's
`IWeaponDamageSource`. Environmental damage, projectile impacts without a known attacker,
and other non-weapon damage sources will pass a handedness check as if it were unset.

---

## Ranged Hits

Set `"AttackSource": "Ranged"` on a rule to restrict it to projectile damage only. The detection is based on OverhaulLib's damage source structure: a ranged hit has the projectile entity as `SourceEntity` and the original shooter as `CauseEntity`, whereas a melee hit has the attacker as both.

```jsonc
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

**`WeaponCodes` for ranged rules** matches the **launcher** (bow, sling…) rather than the projectile item (arrow, stone…). This is the most useful thing to filter on — e.g. `"game:bow-*"` would restrict a rule to bow fire only.

**`Handedness` and `WeaponGrip`** are melee-only concepts and are silently ignored when `AttackSource` is `Ranged`. Setting them on a ranged rule has no effect.

---

## SlowTox Compatibility

If [SlowTox](https://mods.vintagestory.at/slowtox) is installed, the `Intoxication` effect
automatically routes through SlowTox's public API:

| Without SlowTox | With SlowTox |
|---|---|
| Writes the vanilla `intoxication` entity stat | Adds `Strength` to `slowtox:newToxins` |
| Clears the stat on expiry | Adds the same amount to `slowtox:detoxicants` on expiry |

The effect then participates in SlowTox's tolerance, metabolism (sleepable), and
benefit/penalty system. Detection is automatic; SlowTox is not a required dependency.

The `Slow` and `Knockdown` effects use namespaced stat keys (`damageeffects:slow`,
`damageeffects:knockdown-walk/jump`) and do not conflict with SlowTox's own walkspeed penalty.
