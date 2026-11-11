using System.Collections.Generic;
using DawnOfShapes.Core;
using DawnOfShapes.Units;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DawnOfShapes.Selection
{
    /// <summary>
    /// Left click / drag-box selects the local player's units (Shift adds), right click orders a move.
    /// </summary>
    public class SelectionManager : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float dragThreshold = 6f;
        [SerializeField] private float formationSpacing = 2.5f;
        [SerializeField] private Color boxFill = new Color(0.4f, 0.8f, 1f, 0.15f);
        [SerializeField] private Color boxBorder = new Color(0.4f, 0.8f, 1f, 0.9f);

        private readonly List<Unit> selected = new List<Unit>();
        private bool dragging;
        private Vector2 dragStart;
        private Texture2D whiteTex;

        public IReadOnlyList<Unit> Selected => selected;

        private int LocalPlayerId => PlayerRegistry.Instance != null ? PlayerRegistry.Instance.LocalPlayerId : 0;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            whiteTex = Texture2D.whiteTexture;
        }

        public void Configure(Camera cam) => targetCamera = cam;

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || targetCamera == null) return;

            Vector2 mousePos = mouse.position.ReadValue();
            bool additive = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                dragging = true;
                dragStart = mousePos;
            }

            if (mouse.leftButton.wasReleasedThisFrame && dragging)
            {
                dragging = false;
                if (Vector2.Distance(dragStart, mousePos) < dragThreshold) ClickSelect(mousePos, additive);
                else BoxSelect(dragStart, mousePos, additive);
            }

            if (mouse.rightButton.wasPressedThisFrame) IssueMove(mousePos);

            selected.RemoveAll(u => u == null);
        }

        private void ClickSelect(Vector2 screenPos, bool additive)
        {
            if (!additive) ClearSelection();
            Ray ray = targetCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit, 1000f))
            {
                var unit = hit.collider.GetComponentInParent<Unit>();
                if (unit != null && unit.OwnerPlayerId == LocalPlayerId)
                {
                    if (additive && unit.IsSelected) Deselect(unit);
                    else Select(unit);
                }
            }
        }

        private void BoxSelect(Vector2 a, Vector2 b, bool additive)
        {
            if (!additive) ClearSelection();
            Rect rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            foreach (var unit in Unit.All)
            {
                if (unit.OwnerPlayerId != LocalPlayerId) continue;
                Vector3 sp = targetCamera.WorldToScreenPoint(unit.transform.position);
                if (sp.z > 0f && rect.Contains(sp)) Select(unit);
            }
        }

        private void IssueMove(Vector2 screenPos)
        {
            if (selected.Count == 0) return;
            Ray ray = targetCamera.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, 1000f, groundMask)) return;

            // Simple square formation centred on the clicked point.
            int columns = Mathf.CeilToInt(Mathf.Sqrt(selected.Count));
            Vector3 right = Vector3.ProjectOnPlane(targetCamera.transform.right, Vector3.up).normalized;
            Vector3 forward = Vector3.Cross(right, Vector3.up);
            for (int i = 0; i < selected.Count; i++)
            {
                int row = i / columns;
                int col = i % columns;
                float x = (col - (columns - 1) * 0.5f) * formationSpacing;
                float z = -(row - (Mathf.CeilToInt(selected.Count / (float)columns) - 1) * 0.5f) * formationSpacing;
                selected[i].MoveTo(hit.point + right * x + forward * z);
            }
        }

        private void Select(Unit unit)
        {
            if (selected.Contains(unit)) return;
            selected.Add(unit);
            unit.SetSelected(true);
        }

        private void Deselect(Unit unit)
        {
            selected.Remove(unit);
            unit.SetSelected(false);
        }

        public void ClearSelection()
        {
            foreach (var unit in selected)
            {
                if (unit != null) unit.SetSelected(false);
            }
            selected.Clear();
        }

        private void OnGUI()
        {
            if (!dragging || Mouse.current == null) return;
            Vector2 current = Mouse.current.position.ReadValue();
            if (Vector2.Distance(dragStart, current) < dragThreshold) return;

            // GUI space has y pointing down.
            Vector2 p1 = new Vector2(dragStart.x, Screen.height - dragStart.y);
            Vector2 p2 = new Vector2(current.x, Screen.height - current.y);
            Rect r = Rect.MinMaxRect(Mathf.Min(p1.x, p2.x), Mathf.Min(p1.y, p2.y), Mathf.Max(p1.x, p2.x), Mathf.Max(p1.y, p2.y));

            GUI.color = boxFill;
            GUI.DrawTexture(r, whiteTex);
            GUI.color = boxBorder;
            const float t = 1f;
            GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, t), whiteTex);
            GUI.DrawTexture(new Rect(r.xMin, r.yMax - t, r.width, t), whiteTex);
            GUI.DrawTexture(new Rect(r.xMin, r.yMin, t, r.height), whiteTex);
            GUI.DrawTexture(new Rect(r.xMax - t, r.yMin, t, r.height), whiteTex);
            GUI.color = Color.white;
        }
    }
}
