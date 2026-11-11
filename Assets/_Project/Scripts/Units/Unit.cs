using System.Collections.Generic;
using DawnOfShapes.Core;
using DawnOfShapes.Factions;
using UnityEngine;
using UnityEngine.AI;

namespace DawnOfShapes.Units
{
    public enum UnitOrder
    {
        Idle,
        Move,
        Attack,
        AttackMove,
        BuildWall
    }

    [RequireComponent(typeof(NavMeshAgent))]
    public class Unit : Entity
    {
        private static readonly List<Unit> allUnits = new List<Unit>();
        public static IReadOnlyList<Unit> AllUnits => allUnits;

        [SerializeField] private UnitDefinition definition;
        [SerializeField] private GameObject selectionIndicator;

        private NavMeshAgent agent;
        private PlayerState state;
        private UnitOrder order;
        private Vector3 orderPoint;
        private Entity orderTarget;
        private Entity engaged;
        private float cooldown;
        private float scanTimer;
        private float repathTimer;
        private float slowFactor = 1f;
        private float slowTimer;

        public UnitDefinition Definition => definition;
        public UnitOrder Order => order;
        public bool IsSelected { get; private set; }
        public bool IsIdle => order == UnitOrder.Idle && engaged == null;
        public bool IsSlowed => slowTimer > 0f;
        public Entity CurrentTarget => order == UnitOrder.Attack ? orderTarget : engaged;

        public override string DisplayName => definition != null ? definition.displayName : name;
        public override float Armour => definition.armour + Bonus(UpgradeType.Armour);
        public float Damage => definition.damage * (1f + Bonus(UpgradeType.Damage));
        public float Speed => definition.moveSpeed * (1f + Bonus(UpgradeType.Speed)) * slowFactor;

        private float Bonus(UpgradeType type) => state != null ? state.GetBonus(type) : 0f;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            allUnits.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            allUnits.Remove(this);
        }

        public void Initialise(UnitDefinition def, int owner, GameObject indicator)
        {
            definition = def;
            selectionIndicator = indicator;
            InitEntity(owner, def.maxHealth, def.Radius);
            state = MatchManager.Instance != null ? MatchManager.Instance.GetState(owner) : null;

            agent.radius = def.Radius;
            agent.height = def.scale.y;
            agent.speed = Speed;
            agent.angularSpeed = 720f;
            agent.acceleration = 40f;
            agent.stoppingDistance = 0.3f;
            scanTimer = Random.value * 0.3f;
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (selectionIndicator != null) selectionIndicator.SetActive(selected);
        }

        // ---- Commands ----

        public void CommandMove(Vector3 destination)
        {
            order = UnitOrder.Move;
            orderTarget = null;
            engaged = null;
            orderPoint = SampleNavMesh(destination);
            if (agent.isOnNavMesh) agent.SetDestination(orderPoint);
        }

        public void CommandAttack(Entity target)
        {
            if (!IsValidTarget(target)) return;
            order = UnitOrder.Attack;
            orderTarget = target;
            engaged = null;
            repathTimer = 0f;
        }

        public void CommandAttackMove(Vector3 destination)
        {
            order = UnitOrder.AttackMove;
            orderTarget = null;
            engaged = null;
            orderPoint = SampleNavMesh(destination);
            if (agent.isOnNavMesh) agent.SetDestination(orderPoint);
        }

        public bool CommandBuildWall(Vector3 point)
        {
            if (!definition.canBuildWalls) return false;
            order = UnitOrder.BuildWall;
            orderTarget = null;
            engaged = null;
            orderPoint = point;
            if (agent.isOnNavMesh) agent.SetDestination(SampleNavMesh(point));
            return true;
        }

        public void CommandStop() => SetIdle();

        public void ApplySlow(float amount, float duration)
        {
            slowFactor = Mathf.Min(slowFactor, 1f - amount);
            slowTimer = Mathf.Max(slowTimer, duration);
        }

        // ---- Behaviour ----

        private void Update()
        {
            if (IsDead || definition == null || !agent.isOnNavMesh) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            cooldown -= dt;
            scanTimer -= dt;
            repathTimer -= dt;
            if (slowTimer > 0f)
            {
                slowTimer -= dt;
                if (slowTimer <= 0f) slowFactor = 1f;
            }
            agent.speed = Speed;

            switch (order)
            {
                case UnitOrder.Move:
                    if (HasArrived()) SetIdle();
                    break;

                case UnitOrder.Attack:
                    if (!IsValidTarget(orderTarget)) SetIdle();
                    else Engage(orderTarget);
                    break;

                case UnitOrder.AttackMove:
                    UpdateAutoEngage();
                    if (engaged != null)
                    {
                        Engage(engaged);
                    }
                    else
                    {
                        MoveAgentTo(orderPoint);
                        if (HasArrived()) SetIdle();
                    }
                    break;

                case UnitOrder.BuildWall:
                    if (FlatDistanceTo(orderPoint) <= 3f)
                    {
                        if (MatchManager.Instance != null) MatchManager.Instance.TryBuildWall(this, orderPoint);
                        SetIdle();
                    }
                    else if (!agent.pathPending && !agent.hasPath)
                    {
                        SetIdle(); // unreachable
                    }
                    break;

                default:
                    bool wasEngaged = engaged != null;
                    UpdateAutoEngage();
                    if (engaged != null) Engage(engaged);
                    else if (wasEngaged && agent.hasPath) agent.ResetPath();
                    break;
            }

            if (order != UnitOrder.BuildWall && !agent.hasPath && !agent.pathPending) ApplySeparation(dt);
        }

        /// <summary>
        /// Light steering that nudges stationary units apart from allies closer than
        /// <see cref="Formation.SpacingPerRadius"/> radii, so groups don't settle into tight blobs.
        /// </summary>
        private void ApplySeparation(float dt)
        {
            Vector3 push = Vector3.zero;
            for (int i = 0; i < allUnits.Count; i++)
            {
                var other = allUnits[i];
                if (other == this || other.IsDead || other.TeamId != TeamId) continue;
                Vector3 d = transform.position - other.transform.position;
                d.y = 0f;
                float want = (Radius + other.Radius) * Formation.SpacingPerRadius * 0.5f;
                float dist = d.magnitude;
                if (dist >= want) continue;
                // Coincident units get a deterministic sideways push based on instance order.
                Vector3 dir = dist > 0.001f ? d / dist : (GetInstanceID() < other.GetInstanceID() ? Vector3.right : Vector3.left);
                push += dir * (want - dist);
            }
            if (push.sqrMagnitude > 0.0001f)
            {
                agent.Move(Vector3.ClampMagnitude(push, 1f) * definition.moveSpeed * 0.5f * dt);
            }
        }

        private void UpdateAutoEngage()
        {
            if (engaged != null && (!IsValidTarget(engaged) || SurfaceDistanceTo(engaged) > definition.acquireRange * 1.5f))
            {
                engaged = null;
            }
            if (engaged == null && scanTimer <= 0f)
            {
                scanTimer = 0.3f;
                engaged = FindTarget(definition.acquireRange);
            }
        }

        private Entity FindTarget(float searchRange)
        {
            Entity best = null;
            float bestScore = float.MaxValue;
            var entities = Entity.All;
            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (e.IsDead || !IsEnemyOf(e)) continue;
                float d = SurfaceDistanceTo(e);
                if (d > searchRange) continue;
                // Prefer units over buildings.
                float score = d + (e is Unit ? 0f : 6f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = e;
                }
            }
            return best;
        }

        private void Engage(Entity target)
        {
            float d = SurfaceDistanceTo(target);
            if (d <= definition.range)
            {
                if (agent.hasPath) agent.ResetPath();
                Vector3 dir = target.transform.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 720f * Time.deltaTime);
                }
                if (cooldown <= 0f)
                {
                    cooldown = definition.attackCooldown;
                    PerformAttack(target);
                }
            }
            else if (repathTimer <= 0f)
            {
                repathTimer = 0.25f;
                agent.SetDestination(target.transform.position);
            }
        }

        private void PerformAttack(Entity target)
        {
            Color color = state != null ? state.Player.Color : Color.white;
            Vector3 muzzle = transform.position + Vector3.up * definition.scale.y * 0.8f;
            if (definition.projectileSpeed > 0f)
            {
                Projectile.Launch(muzzle, target, Damage, definition, OwnerPlayerId, TeamId, color);
            }
            else
            {
                DealHit(target, Damage, definition, OwnerPlayerId);
                if (definition.range > 2.5f)
                {
                    Tracer.Show(muzzle, target.transform.position + Vector3.up * 0.6f, color);
                }
            }
        }

        public static void DealHit(Entity target, float damage, UnitDefinition source, int attackerPlayerId)
        {
            if (target == null || target.IsDead) return;
            if (source != null && source.slowAmount > 0f && target is Unit unit)
            {
                unit.ApplySlow(source.slowAmount, source.slowDuration);
            }
            float dealt = target.TakeDamage(damage);

            var stats = MatchManager.Instance != null ? MatchManager.Instance.GetState(attackerPlayerId)?.Stats : null;
            if (stats != null && source != null)
            {
                string key = MatchStats.UnitKey(source.displayName);
                stats.AddDamage(key, dealt);
                if (target.IsDead) stats.Add(stats.Kills, key);
            }
        }

        protected override void Die()
        {
            state?.Stats.Add(state.Stats.UnitsLost, MatchStats.UnitKey(definition.displayName));
            if (definition.splitInto != null && definition.splitCount > 0)
            {
                for (int i = 0; i < definition.splitCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / definition.splitCount;
                    Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.2f;
                    UnitFactory.Spawn(definition.splitInto, OwnerPlayerId, transform.position + offset, transform.rotation);
                }
            }
            base.Die();
        }

        // ---- Helpers ----

        private bool IsValidTarget(Entity target) => target != null && !target.IsDead && IsEnemyOf(target);

        private void SetIdle()
        {
            order = UnitOrder.Idle;
            orderTarget = null;
            engaged = null;
            if (agent.isOnNavMesh && agent.hasPath) agent.ResetPath();
        }

        private bool HasArrived()
        {
            return !agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.5f);
        }

        private void MoveAgentTo(Vector3 point)
        {
            if (agent.pathPending) return;
            if (!agent.hasPath || (agent.destination - point).sqrMagnitude > 1f)
            {
                agent.SetDestination(point);
            }
        }

        public static Vector3 SampleNavMesh(Vector3 point)
        {
            return NavMesh.SamplePosition(point, out var hit, 6f, NavMesh.AllAreas) ? hit.position : point;
        }
    }
}
