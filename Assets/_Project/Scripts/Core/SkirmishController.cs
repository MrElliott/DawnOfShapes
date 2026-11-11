using System.Collections.Generic;
using DawnOfShapes.Units;
using UnityEngine;

namespace DawnOfShapes.Core
{
    /// <summary>
    /// Runs a skirmish: idle units push towards the nearest enemy, and the fight ends when a side is wiped out.
    /// </summary>
    public class SkirmishController : MonoBehaviour
    {
        private const float Interval = 0.5f;
        private readonly Dictionary<int, int> alive = new Dictionary<int, int>();
        private readonly Dictionary<int, Vector3> centres = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, int> counts = new Dictionary<int, int>();
        private float timer;

        private void Update()
        {
            var match = MatchManager.Instance;
            if (match == null || match.Phase != MatchPhase.Playing) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = Interval;

            alive.Clear();
            foreach (var s in match.States) alive[s.TeamId] = 0;
            foreach (var u in Unit.AllUnits)
            {
                if (!u.IsDead) alive[u.TeamId] = alive.TryGetValue(u.TeamId, out int n) ? n + 1 : 1;
            }

            int teamsLeft = 0, lastTeam = -1;
            foreach (var pair in alive)
            {
                if (pair.Value > 0)
                {
                    teamsLeft++;
                    lastTeam = pair.Key;
                }
            }
            if (teamsLeft <= 1)
            {
                match.EndSkirmish(teamsLeft == 1 ? lastTeam : -1);
                return;
            }

            // Keep each unit's offset from its army's centre so the army advances spread out, not as a blob.
            centres.Clear();
            counts.Clear();
            foreach (var u in Unit.AllUnits)
            {
                if (u.IsDead) continue;
                centres[u.TeamId] = (centres.TryGetValue(u.TeamId, out var c) ? c : Vector3.zero) + u.transform.position;
                counts[u.TeamId] = (counts.TryGetValue(u.TeamId, out int n) ? n : 0) + 1;
            }

            foreach (var u in Unit.AllUnits)
            {
                if (u.IsDead || !u.IsIdle) continue;
                Vector3 offset = u.transform.position - centres[u.TeamId] / counts[u.TeamId];
                Unit nearest = null;
                float best = float.MaxValue;
                foreach (var other in Unit.AllUnits)
                {
                    if (other.IsDead || !u.IsEnemyOf(other)) continue;
                    float d = u.FlatDistanceTo(other.transform.position);
                    if (d < best)
                    {
                        best = d;
                        nearest = other;
                    }
                }
                if (nearest != null) u.CommandAttackMove(nearest.transform.position + Vector3.ClampMagnitude(offset, 12f));
            }
        }
    }
}
