using DawnOfShapes.Core;
using DawnOfShapes.Units;
using UnityEngine;

namespace DawnOfShapes.Map
{
    /// <summary>
    /// Strategic point. Units of one team standing in the radius, uncontested, capture it over time;
    /// two or more teams present freezes progress. Territory morphing will key off these later.
    /// </summary>
    public class CapturePoint : MonoBehaviour
    {
        public const int Unowned = -1;
        private const int RingSegments = 48;

        [SerializeField] private int pointId;
        [SerializeField] private float radius = 8f;
        [SerializeField] private Renderer markerRenderer;

        private int ownerPlayerId = Unowned;
        private int capturingPlayerId = Unowned;
        private float progress;
        private LineRenderer ring;
        private MaterialPropertyBlock block;

        public int PointId => pointId;
        public float Radius => radius;
        public int OwnerPlayerId => ownerPlayerId;
        public int OwnerTeamId => TeamOf(ownerPlayerId);
        public int CapturingPlayerId => capturingPlayerId;
        public float Progress => progress;
        public bool Contested { get; private set; }

        public void Initialise(int id, float captureRadius, Renderer marker)
        {
            pointId = id;
            radius = captureRadius;
            markerRenderer = marker;
        }

        private void Start()
        {
            block = new MaterialPropertyBlock();
            var visuals = MatchManager.Instance != null ? MatchManager.Instance.Visuals : null;
            var ringGo = new GameObject("ProgressRing");
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            ring = ringGo.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.widthMultiplier = 0.6f;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (visuals != null) ring.sharedMaterial = visuals.unlitMaterial;
            UpdateVisuals();
        }

        public void Tick(float dt, float captureTime)
        {
            int presentTeam = -1;
            int presentPlayer = Unowned;
            bool contested = false;
            var units = Unit.AllUnits;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.IsDead || u.FlatDistanceTo(transform.position) > radius) continue;
                if (presentTeam < 0)
                {
                    presentTeam = u.TeamId;
                    presentPlayer = u.OwnerPlayerId;
                }
                else if (u.TeamId != presentTeam)
                {
                    contested = true;
                    break;
                }
            }
            Contested = contested;
            float rate = dt / Mathf.Max(0.1f, captureTime);

            if (contested)
            {
                // Frozen.
            }
            else if (presentTeam >= 0)
            {
                bool ownersPresent = ownerPlayerId != Unowned && OwnerTeamId == presentTeam;
                if (ownersPresent)
                {
                    // Owners push back any enemy progress.
                    if (capturingPlayerId != Unowned && capturingPlayerId != ownerPlayerId)
                    {
                        progress -= rate;
                        if (progress <= 0f) ResetCapture();
                    }
                }
                else
                {
                    if (capturingPlayerId == Unowned || TeamOf(capturingPlayerId) != presentTeam)
                    {
                        capturingPlayerId = presentPlayer;
                        progress = 0f;
                    }
                    progress += rate;
                    if (progress >= 1f)
                    {
                        ownerPlayerId = capturingPlayerId;
                        ResetCapture();
                        var owner = PlayerRegistry.Instance != null ? PlayerRegistry.Instance.GetPlayer(ownerPlayerId) : null;
                        Debug.Log($"[DawnOfShapes] Capture point {pointId} taken by {(owner != null ? owner.DisplayName : ownerPlayerId.ToString())}.");
                    }
                }
            }
            else if (capturingPlayerId != Unowned)
            {
                // Abandoned partial captures decay slowly.
                progress -= rate * 0.5f;
                if (progress <= 0f) ResetCapture();
            }

            UpdateVisuals();
        }

        private void ResetCapture()
        {
            capturingPlayerId = Unowned;
            progress = 0f;
        }

        private void UpdateVisuals()
        {
            if (block == null) return;
            var visuals = MatchManager.Instance != null ? MatchManager.Instance.Visuals : null;
            Color neutral = visuals != null ? visuals.neutralColor : Color.grey;

            if (markerRenderer != null)
            {
                Color marker = ownerPlayerId != Unowned ? Color.Lerp(ColorOf(ownerPlayerId), neutral, 0.25f) : neutral;
                markerRenderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", marker);
                markerRenderer.SetPropertyBlock(block);
            }

            if (ring == null) return;
            float fill;
            Color ringColor;
            if (capturingPlayerId != Unowned)
            {
                fill = progress;
                ringColor = ColorOf(capturingPlayerId);
            }
            else if (ownerPlayerId != Unowned)
            {
                fill = 1f;
                ringColor = ColorOf(ownerPlayerId);
            }
            else
            {
                fill = 0f;
                ringColor = neutral;
            }
            if (Contested) ringColor = Color.Lerp(ringColor, Color.white, Mathf.PingPong(Time.unscaledTime * 3f, 1f));

            int count = Mathf.Max(0, Mathf.CeilToInt(RingSegments * fill));
            ring.positionCount = count > 0 ? count + 1 : 0;
            float r = radius + 0.4f;
            for (int i = 0; i <= count && count > 0; i++)
            {
                float a = Mathf.Min(i / (float)RingSegments, fill) * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r));
            }
            ring.GetPropertyBlock(block);
            block.SetColor("_BaseColor", ringColor);
            ring.SetPropertyBlock(block);
        }

        private static int TeamOf(int playerId)
        {
            if (playerId == Unowned) return -1;
            var player = PlayerRegistry.Instance != null ? PlayerRegistry.Instance.GetPlayer(playerId) : null;
            return player != null ? player.TeamId : playerId;
        }

        private static Color ColorOf(int playerId)
        {
            var player = PlayerRegistry.Instance != null ? PlayerRegistry.Instance.GetPlayer(playerId) : null;
            return player != null ? player.Color : Color.white;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
