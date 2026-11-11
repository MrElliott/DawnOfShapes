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

            foreach (var u in Unit.AllUnits)
            {
                if (u.IsDead || !u.IsIdle) continue;
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
                if (nearest != null) u.CommandAttackMove(nearest.transform.position);
            }
        }
    }
}
