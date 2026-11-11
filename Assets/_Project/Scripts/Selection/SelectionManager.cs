using System.Collections.Generic;
using DawnOfShapes.Core;
using DawnOfShapes.UI;
using DawnOfShapes.Units;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DawnOfShapes.Selection
{
    public enum CommandMode
    {
        None,
        AttackMove,
        BuildWall
    }

    /// <summary>
    /// Left click / drag-box selects the local player's units (Shift adds). Right click moves, or attacks
    /// when clicking an enemy. A then left click = attack-move. B (or the HUD button) then left click = place wall.
    /// </summary>
    public class SelectionManager : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float dragThreshold = 6f;
        [SerializeField] private Color boxFill = new Color(0.4f, 0.8f, 1f, 0.15f);
        [SerializeField] private Color boxBorder = new Color(0.4f, 0.8f, 1f, 0.9f);

        private readonly List<Unit> selected = new List<Unit>();
        private bool dragging;
        private Vector2 dragStart;

        public IReadOnlyList<Unit> Selected => selected;
        public CommandMode Mode { get; private set; }

        public bool HasBuilderSelected
        {
            get
            {
                foreach (var u in selected)
                {
                    if (u != null && !u.IsDead && u.Definition.canBuildWalls) return true;
                }
                return false;
            }
        }

        private int LocalPlayerId => PlayerRegistry.Instance != null ? PlayerRegistry.Instance.LocalPlayerId : 0;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
        }

        public void Configure(Camera cam) => targetCamera = cam;

        public void BeginWallPlacement()
        {
            if (HasBuilderSelected) Mode = CommandMode.BuildWall;
        }

        public void BeginAttackMove()
        {
            if (selected.Count > 0) Mode = CommandMode.AttackMove;
        }

        private void Update()
        {
            selected.RemoveAll(u => u == null || u.IsDead);

            var match = MatchManager.Instance;
            var mouse = Mouse.current;
            if (mouse == null || targetCamera == null || match == null || match.Phase != MatchPhase.Playing)
            {
                dragging = false;
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (Mode != CommandMode.None) Mode = CommandMode.None;
                    else ClearSelection();
                }
                if (keyboard.aKey.wasPressedThisFrame && keyboard.ctrlKey.isPressed) SelectAllOwnUnits();
                else if (keyboard.fKey.wasPressedThisFrame) BeginAttackMove();
                if (keyboard.bKey.wasPressedThisFrame) BeginWallPlacement();
                if (keyboard.hKey.wasPressedThisFrame)
                {
                    foreach (var u in selected) u.CommandStop();
                }
            }

            Vector2 mousePos = mouse.position.ReadValue();
            bool overUI = HUD.Instance != null && HUD.Instance.IsOverUI(mousePos);
            bool additive = keyboard != null && keyboard.shiftKey.isPressed;

            if (mouse.leftButton.wasPressedThisFrame && !overUI)
            {
                if (Mode != CommandMode.None)
                {
                    IssueModeCommand(mousePos);
                    if (!additive) Mode = CommandMode.None;
                }
                else
                {
                    dragging = true;
                    dragStart = mousePos;
                }
            }

            if (mouse.leftButton.wasReleasedThisFrame && dragging)
            {
                dragging = false;
                if (Vector2.Distance(dragStart, mousePos) < dragThreshold) ClickSelect(mousePos, additive);
                else BoxSelect(dragStart, mousePos, additive);
            }

            if (mouse.rightButton.wasPressedThisFrame && !overUI)
            {
                if (Mode != CommandMode.None) Mode = CommandMode.None;
                else IssueContextCommand(mousePos);
            }
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
            foreach (var unit in Unit.AllUnits)
            {
                if (unit.OwnerPlayerId != LocalPlayerId || unit.IsDead) continue;
                Vector3 sp = targetCamera.WorldToScreenPoint(unit.transform.position);
                if (sp.z > 0f && rect.Contains(sp)) Select(unit);
            }
        }

        private void SelectAllOwnUnits()
        {
            ClearSelection();
            foreach (var unit in Unit.AllUnits)
            {
                if (unit.OwnerPlayerId == LocalPlayerId && !unit.IsDead) Select(unit);
            }
        }

        private void IssueContextCommand(Vector2 screenPos)
        {
            if (selected.Count == 0) return;
            Ray ray = targetCamera.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, 1000f)) return;

            var entity = hit.collider.GetComponentInParent<Entity>();
            if (entity != null && !entity.IsDead && selected[0].IsEnemyOf(entity))
            {
                foreach (var u in selected) u.CommandAttack(entity);
                return;
            }
            IssueFormation(hit.point, false);
        }

        private void IssueModeCommand(Vector2 screenPos)
        {
            Ray ray = targetCamera.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, 1000f)) return;

            if (Mode == CommandMode.AttackMove)
            {
                IssueFormation(hit.point, true);
            }
            else if (Mode == CommandMode.BuildWall)
            {
                Unit builder = null;
                float best = float.MaxValue;
                foreach (var u in selected)
                {
                    if (!u.Definition.canBuildWalls) continue;
                    float d = u.FlatDistanceTo(hit.point);
                    if (d < best)
                    {
                        best = d;
                        builder = u;
                    }
                }
                if (builder != null) builder.CommandBuildWall(hit.point);
            }
        }

        private void IssueFormation(Vector3 point, bool attackMove)
        {
            // Square formation centred on the clicked point, facing away from the camera.
            Formation.Order(selected, point, targetCamera.transform.forward, attackMove);
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
            Mode = CommandMode.None;
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

            var tex = Texture2D.whiteTexture;
            GUI.color = boxFill;
            GUI.DrawTexture(r, tex);
            GUI.color = boxBorder;
            const float t = 1f;
            GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, t), tex);
            GUI.DrawTexture(new Rect(r.xMin, r.yMax - t, r.width, t), tex);
            GUI.DrawTexture(new Rect(r.xMin, r.yMin, t, r.height), tex);
            GUI.DrawTexture(new Rect(r.xMax - t, r.yMin, t, r.height), tex);
            GUI.color = Color.white;
        }
    }
}
