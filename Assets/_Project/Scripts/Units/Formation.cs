using System.Collections.Generic;
using UnityEngine;

namespace DawnOfShapes.Units
{
    /// <summary>
    /// Spreads a group order over a square grid so units arrive apart instead of piling onto one point.
    /// </summary>
    public static class Formation
    {
        /// <summary>Centre-to-centre spacing as a multiple of unit radius.</summary>
        public const float SpacingPerRadius = 2.5f;

        public const float MinimumSpacing = 2.5f;

        public static float SpacingFor(IReadOnlyList<Unit> units)
        {
            float maxRadius = 0f;
            foreach (var u in units)
            {
                if (u != null) maxRadius = Mathf.Max(maxRadius, u.Radius);
            }
            return Mathf.Max(MinimumSpacing, maxRadius * SpacingPerRadius);
        }

        /// <summary>
        /// Grid slot for index <paramref name="i"/> of <paramref name="count"/>, centred on zero,
        /// with x along <paramref name="right"/> and rows along <paramref name="forward"/>.
        /// </summary>
        public static Vector3 Offset(int i, int count, float spacing, Vector3 right, Vector3 forward)
        {
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt(count / (float)columns);
            float x = (i % columns - (columns - 1) * 0.5f) * spacing;
            float z = -(i / columns - (rows - 1) * 0.5f) * spacing;
            return right * x + forward * z;
        }

        /// <summary>Issues move or attack-move orders to a group in formation around a point.</summary>
        public static void Order(IReadOnlyList<Unit> units, Vector3 point, Vector3 facing, bool attackMove)
        {
            if (units.Count == 0) return;
            facing.y = 0f;
            Vector3 forward = facing.sqrMagnitude > 0.001f ? facing.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float spacing = SpacingFor(units);
            for (int i = 0; i < units.Count; i++)
            {
                Vector3 target = point + Offset(i, units.Count, spacing, right, forward);
                if (attackMove) units[i].CommandAttackMove(target);
                else units[i].CommandMove(target);
            }
        }
    }
}
