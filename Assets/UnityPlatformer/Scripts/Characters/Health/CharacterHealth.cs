using System;
using UnityEngine;
using UnityEngine.Assertions;

/// TODO handle lives / Game over
/// TODO handle character alignment
/// TODO handle Damage direction here or in the HitBox but must be done :)

namespace UnityPlatformer {
  /// <summary>
  /// Tracks character health and lives.
  ///
  /// Triggers character damage/death
  /// </summary>
  public class CharacterHealth : MonoBehaviour {
    /// <summary>
    /// Character alignment
    /// </summary>
    public Alignment alignment = Alignment.None;
    /// <summary>
    /// Can recieve Damage from friends? (same alignment)
    /// </summary>
    public bool friendlyFire = false;
    /// <summary>
    /// Health the character will have when game starts
    /// </summary>
    [Comment("Health the character will have when game starts")]
    public int startingHealth = 1;
    /// <summary>
    /// Maximum health (-1 no maximum). NOTE if startingHealth == maxHealth will trigger onMaxHealth on Start.
    /// </summary>
    [Comment("Maximum health (-1 no maximum). NOTE if startingHealth == maxHealth will trigger onMaxHealth on Start.")]
    public int maxHealth = 1;
    /// <summary>
    /// Lives the character starts with (-1 no lives)
    /// </summary>
    [Comment("Lives the character starts with (-1 no lives)")]
    public int startingLives = 1;
    /// <summary>
    /// Maximum lives of the character. 2,147,483,647 is the maximum :)
    /// </summary>
    [Comment("Maximum lives of the character. 2,147,483,647 is the maximum :)")]
    public int maxLives = 1;
    /// <summary>
    /// After any Damage how much time the character will be invulnerable to any Damage (0 to disable)
    /// </summary>
    [Comment("After any Damage how much time the character will be invulnerable to any Damage (0 to disable)")]
    public float invulnerabilityTimeAfterDamage = 2.0f;
    /// <summary>
    /// List of damages that are ignored
    ///
    /// NOTE: this can give your character super powers! use it with caution!
    /// </summary>
    [Comment("List of damages that are ignored")]
    [EnumFlagsAttribute]
    public DamageType immunity = 0;
    /// <summary>
    /// Fired when Character heal and now it's at maxHealth
    /// </summary>
    public Action onMaxHealth;
    /// <summary>
    /// Fired when character is damaged.
    ///
    /// Will be fired even it the character is inmmune
    /// </summary>
    public Action onDamage;
    /// <summary>
    /// Fired after onDamage and Character is inmmune to given Damage
    /// </summary>
    public Action onImmunity;
    /// <summary>
    /// Callback type for onHurt
    /// </summary>
    public delegate void HurtCallback(Damage dt, CharacterHealth to);
    /// <summary>
    /// This Character health is reduced, will fire after onDamage
    ///
    /// dt is the Damage dealed
    /// to is the CharacterHealth that hurt me, if possible, could be null
    /// </summary>
    public HurtCallback onInjured;
    /// <summary>
    /// This Character deal damage to other
    ///
    /// dt is the Damage dealed
    /// to is the CharacterHealth hurted
    /// </summary>
    public HurtCallback onHurt;
    /// <summary>
    /// Display some greenish starts floating around!
    /// </summary>
    public Action onHeal;
    /// <summary>
    /// Play death animation, turn off the music... those sort of things
    /// </summary>
    public Action onDeath;
    /// <summary>
    /// Credits...
    /// </summary>
    public Action onGameOver;
    /// <summary>
    /// Play that funky music! Quake-damage!
    ///
    /// NOTE this can be fired many times before onInvulnerabilityEnd
    /// </summary>
    public Action onInvulnerabilityStart;
    /// <summary>
    /// Stop that funky music!
    /// </summary>
    public Action onInvulnerabilityEnd;
    /// <summary>
    /// After death when there are lives player can respawn
    /// </summary>
    public Action onRespawn;
    // NOTE do not use setter/getter to trigger death, we need to preserve
    // logical Action dispacthing
    /// <summary>
    /// Character health
    /// </summary>
    [HideInInspector]
    public int health = 0;
    /// <summary>
    /// Character lives
    /// </summary>
    [HideInInspector]
    public int lives = 0;
    /// <summary>
    /// Character owner of this CharacterHealth
    /// </summary>
    [HideInInspector]
    public Character character;
    /// <summary>
    /// Time counter for invulnerability
    /// </summary>
    private Cooldown invulnerability;
    /// <summary>
    /// check missconfiguration and initialization
    /// </summary>
    public void Start() {
      Assert.IsFalse(startingHealth < maxHealth, "(CharacterHealth) startingHealth < maxHealth: " + gameObject.GetFullName());
      Assert.IsFalse(startingLives < maxLives, "(CharacterHealth) startingLives < maxLives: " + gameObject.GetFullName());

      character = GetComponent<Character>();
      Assert.IsNotNull(character, "(CharacterHealth) Character is required: " + gameObject.GetFullName());

      Heal(startingHealth);
      lives = startingLives;

      invulnerability = new Cooldown(invulnerabilityTimeAfterDamage);
      invulnerability.onReady += () => {
        if (onInvulnerabilityEnd != null) {
          onInvulnerabilityEnd();
        }
      };
      invulnerability.onReset += () => {
        if (onInvulnerabilityStart != null) {
          onInvulnerabilityStart();
        }
      };
    }
    /// <summary>
    /// Turns a character invulnerable, but still can be killed using Kill
    ///
    /// NOTE use float.MaxValue for unlimited time
    /// </summary>
    public void SetInvulnerable(float time) {
      invulnerability.Set(time);
      invulnerability.Reset();
    }
    /// <summary>
    /// disable invulnerability
    /// </summary>
    public void SetVulnerable() {
      invulnerability.Clear();
    }
    /// <summary>
    /// Character is invulnerable?
    /// </summary>
    public bool IsInvulnerable() {
      // is death? leave him alone...
      return health <= 0 || !invulnerability.Ready();
    }
    /// <summary>
    /// Kill the character even if it's invulnerable
    /// </summary>
    public void Kill() {
      health = 0;
      Die();
    }
    /// <summary>
    /// Try to Damage the Character
    /// </summary>
    public void Damage(Damage dmg) {
      Debug.LogFormat("Object: {0} recieve damage {1} health {2} from: {3}",
        gameObject.GetFullName(), dmg.amount, health, dmg.causer.gameObject.GetFullName());

      Assert.IsNotNull(dmg.causer, "(CharacterHealth) Damage without causer: " + dmg.gameObject.GetFullName());

      if (friendlyFire && dmg.causer.alignment == alignment && !dmg.friendlyFire) {
        Debug.LogFormat("Damage is not meant for friends, ignore");
        return;
      }

      if (Damage(dmg.amount, dmg.type, dmg.causer)) {
        if (dmg.causer.onHurt != null) {
          dmg.causer.onHurt(dmg, this);
        }
      }
    }
    /// <summary>
    /// Try to Damage the Character
    /// </summary>
    public bool Damage(int amount, DamageType dt, CharacterHealth causer = null) {
      Debug.LogFormat("immunity {0} DamageType {1} alignment {2}", immunity, dt, alignment);
      if (!friendlyFire && causer.alignment == alignment) {
        Debug.LogFormat("Cannot recieve damage from the same alignment");
        return false;
      }

      if ((immunity & dt) == dt) {
        Debug.LogFormat("Inmune to {0} attacks", dt);

        if (onDamage != null) {
          onDamage();
        }

        if (onImmunity != null) {
          onImmunity();
        }

        return false;
      }

      return Damage(amount, causer);
    }
    /// <summary>
    /// Try to Damage the Character
    ///
    /// triggers onDamage
    /// triggers onDeath
    /// NOTE this won't trigger onHurtCharacter
    /// </summary>
    public bool Damage(int amount = 1, CharacterHealth causer = null) {
      if (amount <= 0) {
        Debug.LogWarning("amount <= 0 ??");
      }

      if (IsInvulnerable()) {
        Debug.Log(gameObject.GetFullName() + " is invulnerable, ignore damage");

        if (onDamage != null) {
          onDamage();
        }

        if (onImmunity != null) {
          onImmunity();
        }

        return false;
      }

      health -= amount;

      // do not set invulnerable a dead Character
      if (health > 0) {
        SetInvulnerable(invulnerabilityTimeAfterDamage);
      }

      if (onDamage != null) {
        onDamage();
      }

      if (onInjured != null) {
        onInjured(null, causer);
      }

      if (health <= 0) {
        Die();
      }

      return true;

    }
    /// <summary>
    /// No healt
    /// </summary>
    public bool isDead() {
      return health <= 0;
    }
    /// <summary>
    /// increse health character if possible maxHealth not reached.
    /// Trigger onMaxHealth
    /// </summary>
    public void Heal(int amount = 1) {
      health += amount;
      if (onHeal != null) {
        onHeal();
      }

      if (maxHealth != -1 && health >= maxHealth) {
        health = maxHealth;
        if (onMaxHealth != null) {
          onMaxHealth();
        }
      }
    }
    public void DisableAllHitBoxes() {
      Debug.Log(gameObject.GetFullName() + " disable all HitBox(es)");
      var lch = GetComponentsInChildren<HitBox> ();
      foreach (var x in lch) {
         x.gameObject.SetActive(false);
      }

      Debug.Log(gameObject.GetFullName() + " disable all Damage(s)");
      var ldt = GetComponentsInChildren<Damage> ();
      foreach (var x in ldt) {
         x.gameObject.SetActive(false);
      }
    }
    /// <summary>
    /// Disable HitBox(es) and DamageType(s)
    /// Trigger onDeath
    /// </summary>
    public void Die() {
      --lives;

      if (onDeath != null) {
        Debug.Log(gameObject.GetFullName() + " died!");
        onDeath();
      }

      if (lives == 0) {
        if (onGameOver != null) {
          Debug.Log(gameObject.GetFullName() + " game-over!");
          onGameOver();
        }
      } else {
        // disable invulnerability

        // respawn
        Heal(startingHealth);

        if (onRespawn != null) {
          onRespawn();
        }
      }
    }
  }
}
