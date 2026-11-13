using System;
using System.Collections.Generic;
using UnityEngine;

namespace DawnOfShapes.Core
{
    /// <summary>
    /// Anything owned by a player that has health and can be attacked: units, bases, research buildings, walls.
    /// </summary>
    public abstract class Entity : MonoBehaviour
    {
        private static readonly List<Entity> all = new List<Entity>();
        public static IReadOnlyList<Entity> All => all;

        [SerializeField] private int ownerPlayerId;

        public int OwnerPlayerId => ownerPlayerId;
        public int TeamId { get; private set; }
        public float MaxHealth { get; private set; }
        public float Health { get; private set; }
        public float Radius { get; private set; } = 0.5f;
        public bool IsDead { get; private set; }
        public virtual float Armour => 0f;
        public abstract string DisplayName { get; }

        public event Action<Entity> Died;

        protected virtual void OnEnable() => all.Add(this);
        protected virtual void OnDisable() => all.Remove(this);

        protected void InitEntity(int owner, float maxHealth, float radius)
        {
            ownerPlayerId = owner;
            var player = PlayerRegistry.Instance != null ? PlayerRegistry.Instance.GetPlayer(owner) : null;
            TeamId = player != null ? player.TeamId : owner;
            MaxHealth = Health = maxHealth;
            Radius = radius;
        }

        public bool IsEnemyOf(Entity other) => other != null && other.TeamId != TeamId;

        /// <summary>Highest damage reduction armour can reach, in percent.</summary>
        public const float MaxArmour = 75f;

        /// <summary>Applies damage after armour, then <paramref name="multiplier"/> (shape counters), and returns
        /// the health actually removed.</summary>
        public float TakeDamage(float amount, float multiplier = 1f)
        {
            if (IsDead) return 0f;
            // Armour is a percentage damage reduction, so it scales the same against small and large hits.
            float reduction = Mathf.Clamp(Armour, 0f, MaxArmour) / 100f;
            float dealt = Mathf.Min(Health, amount * (1f - reduction) * multiplier);
            Health -= dealt;
            if (Health <= 0f)
            {
                Health = 0f;
                Die();
            }
            return dealt;
        }

        public float FlatDistanceTo(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        /// <summary>Edge-to-edge distance on the ground plane.</summary>
        public float SurfaceDistanceTo(Entity other)
        {
            return Mathf.Max(0f, FlatDistanceTo(other.transform.position) - other.Radius - Radius);
        }

        protected virtual void Die()
        {
            IsDead = true;
            Died?.Invoke(this);
            if (MatchManager.Instance != null) MatchManager.Instance.OnEntityDied(this);
            Destroy(gameObject);
        }
    }
}
