using System.Collections.Generic;
using System.Linq;
using DawnOfShapes.Core;
using DawnOfShapes.Map;
using DawnOfShapes.Units;
using UnityEngine;

namespace DawnOfShapes.AI
{
    /// <summary>
    /// Simple opponent: keeps production running, defends its base, sends groups to the nearest
    /// points it does not hold (then the enemy base), builds walls and researches when it has spare Form.
    /// </summary>
    public class AIController : MonoBehaviour
    {
        private const float ThinkInterval = 1f;
        private const float DefenceRadius = 32f;

        private readonly List<Unit> myUnits = new List<Unit>();
        private readonly HashSet<Unit> holding = new HashSet<Unit>();
        private int playerId;
        private UnitDefinition nextUnit;
        private float thinkTimer;
        private MatchManager match;
        private PlayerState state;

        public void Initialise(int aiPlayerId)
        {
            playerId = aiPlayerId;
            match = MatchManager.Instance;
            state = match.GetState(playerId);
            thinkTimer = Random.Range(0.5f, 1.5f);
        }

        private void Update()
        {
            if (match == null || state == null || match.Phase != MatchPhase.Playing || state.Eliminated) return;
            thinkTimer -= Time.deltaTime;
            if (thinkTimer > 0f) return;
            thinkTimer = ThinkInterval;
            Think();
        }

        private void Think()
        {
            var baseB = state.Base;
            if (baseB == null || baseB.IsDead) return;

            myUnits.Clear();
            foreach (var u in Unit.AllUnits)
            {
                if (!u.IsDead && u.OwnerPlayerId == playerId) myUnits.Add(u);
            }

            // Research spending takes priority once it is due, otherwise production eats every bit of Form.
            if (!Research()) Produce();
            if (Defend()) return;
            BuildWalls();
            Attack();
        }

        /// <summary>Returns true while saving Form for research (production should wait).</summary>
        private bool Research()
        {
            var faction = state.Faction;
            if (state.Research == null || state.Research.IsDead)
            {
                if (match.MatchTime < 90f) return false;
                if (!match.TryBuildResearch(playerId)) return state.Form < faction.ResearchBuildingCost;
                return false;
            }
            if (state.Research.IsBusy) return false;

            // One upgrade roughly every two minutes after the building is up.
            int done = faction.Upgrades.Count(state.HasUpgrade);
            if (match.MatchTime < 120f + done * 120f) return false;
            foreach (var up in faction.Upgrades)
            {
                if (state.HasUpgrade(up)) continue;
                if (state.Research.TryResearch(up)) return false;
                return true; // save up for it
            }
            return false;
        }

        private void Produce()
        {
            var baseB = state.Base;
            if (baseB.Queue.Count >= 2) return;
            int queuedPop = baseB.Queue.Sum(d => d.popCost);
            // Commit to one choice and save for it, so expensive units get built as often as cheap ones.
            if (nextUnit == null) nextUnit = PickUnit();
            if (nextUnit == null) return;
            if (match.HasPopRoom(playerId, nextUnit.popCost + queuedPop) && baseB.TryQueue(nextUnit)) nextUnit = null;
        }

        private UnitDefinition PickUnit()
        {
            var options = state.Faction.Units.Where(u => u.producible).ToList();
            if (options.Count == 0) return null;

            int builders = myUnits.Count(u => u.Definition.canBuildWalls);
            float total = 0f;
            var weights = new float[options.Count];
            for (int i = 0; i < options.Count; i++)
            {
                weights[i] = options[i].canBuildWalls ? (builders < 2 ? 0.4f : 0f) : 1f;
                total += weights[i];
            }
            if (total <= 0f) return null;

            float roll = Random.value * total;
            for (int i = 0; i < options.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0f) return options[i];
            }
            return options[options.Count - 1];
        }

        private bool Defend()
        {
            Vector3 basePos = state.Base.transform.position;
            Entity threat = null;
            float best = DefenceRadius;
            foreach (var e in Entity.All)
            {
                if (e.IsDead || e.TeamId == state.TeamId || !(e is Unit)) continue;
                float d = e.FlatDistanceTo(basePos);
                if (d < best)
                {
                    best = d;
                    threat = e;
                }
            }
            if (threat == null) return false;

            var defenders = myUnits.Where(u => u.FlatDistanceTo(basePos) < 80f && u.CurrentTarget == null).ToList();
            Formation.Order(defenders, threat.transform.position, threat.transform.position - basePos, true);
            return true;
        }

        private void BuildWalls()
        {
            if (state.Form < 140f) return;
            var heldPoints = match.Points.Where(p => p.OwnerPlayerId == playerId).ToList();
            if (heldPoints.Count == 0) return;
            foreach (var u in myUnits)
            {
                if (!u.Definition.canBuildWalls || !u.IsIdle || Random.value > 0.3f) continue;
                var point = heldPoints[Random.Range(0, heldPoints.Count)];
                Vector2 dir = Random.insideUnitCircle.normalized;
                u.CommandBuildWall(point.transform.position + new Vector3(dir.x, 0f, dir.y) * (point.Radius + 2f));
                return;
            }
        }

        private void Attack()
        {
            // Units standing on a point that is not ours yet are capturing: leave them. Keep one guard per held point.
            holding.Clear();
            foreach (var p in match.Points)
            {
                bool ours = p.OwnerPlayerId >= 0 && p.OwnerTeamId == state.TeamId;
                int guards = 0;
                foreach (var u in myUnits)
                {
                    if (!u.IsIdle || u.FlatDistanceTo(p.transform.position) > p.Radius) continue;
                    if (!ours || guards < 1)
                    {
                        holding.Add(u);
                        guards++;
                    }
                }
            }

            var idle = myUnits.Where(u => u.IsIdle && !u.Definition.canBuildWalls && !holding.Contains(u)).ToList();
            int groupSize = match.MatchTime < 90f ? 1 : 4;
            if (idle.Count < groupSize) return;

            Vector3 centroid = Vector3.zero;
            foreach (var u in idle) centroid += u.transform.position;
            centroid /= idle.Count;

            Vector3 target;
            CapturePoint nearest = null;
            float best = float.MaxValue;
            foreach (var p in match.Points)
            {
                if (p.OwnerPlayerId >= 0 && p.OwnerTeamId == state.TeamId) continue;
                float d = Vector3.Distance(centroid, p.transform.position);
                if (d < best)
                {
                    best = d;
                    nearest = p;
                }
            }

            if (nearest != null)
            {
                target = nearest.transform.position;
            }
            else
            {
                var enemy = match.States.Where(s => !s.Eliminated && s.TeamId != state.TeamId && s.Base != null)
                    .OrderBy(s => Vector3.Distance(centroid, s.Base.transform.position)).FirstOrDefault();
                if (enemy == null) return;
                target = enemy.Base.transform.position;
            }

            Formation.Order(idle, target, target - centroid, true);
        }
    }
}
