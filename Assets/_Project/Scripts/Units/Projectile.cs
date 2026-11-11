using System.Collections.Generic;
using DawnOfShapes.Core;
using UnityEngine;

namespace DawnOfShapes.Units
{
    /// <summary>
    /// Lobbed shot that lands where the target was when it was fired (dodgeable) and deals splash damage.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        private static readonly List<Entity> hitBuffer = new List<Entity>();

        private Vector3 start;
        private Vector3 end;
        private Entity target;
        private float damage;
        private UnitDefinition source;
        private int teamId;
        private float duration;
        private float elapsed;

        public static void Launch(Vector3 from, Entity target, float damage, UnitDefinition source, int ownerPlayerId, int teamId, Color color)
        {
            var visuals = MatchManager.Instance != null ? MatchManager.Instance.Visuals : null;
            var go = new GameObject("Projectile");
            go.transform.position = from;
            go.transform.localScale = Vector3.one * 0.6f;
            go.AddComponent<MeshFilter>().sharedMesh = visuals != null ? visuals.projectileMesh : null;
            var renderer = go.AddComponent<MeshRenderer>();
            if (visuals != null) renderer.sharedMaterial = visuals.unlitMaterial;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);

            var p = go.AddComponent<Projectile>();
            p.start = from;
            p.end = target.transform.position;
            p.target = target;
            p.damage = damage;
            p.source = source;
            p.teamId = teamId;
            float distance = Vector3.Distance(from, p.end);
            p.duration = Mathf.Max(0.2f, distance / Mathf.Max(1f, source.projectileSpeed));
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 pos = Vector3.Lerp(start, end, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * Mathf.Max(2f, Vector3.Distance(start, end) * 0.25f);
            transform.position = pos;
            if (t >= 1f) Impact();
        }

        private void Impact()
        {
            if (source.splashRadius > 0f)
            {
                hitBuffer.Clear();
                foreach (var e in Entity.All)
                {
                    if (e.IsDead || e.TeamId == teamId) continue;
                    float d = Mathf.Max(0f, e.FlatDistanceTo(end) - e.Radius);
                    if (d <= source.splashRadius) hitBuffer.Add(e);
                }
                // Apply after collecting: deaths can spawn units and change Entity.All.
                foreach (var e in hitBuffer) Unit.DealHit(e, damage, source);
            }
            else if (target != null && !target.IsDead)
            {
                Unit.DealHit(target, damage, source);
            }
            Destroy(gameObject);
        }
    }
}
