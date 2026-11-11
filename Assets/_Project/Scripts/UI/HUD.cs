using System.Collections.Generic;
using System.Linq;
using DawnOfShapes.Core;
using DawnOfShapes.Selection;
using DawnOfShapes.Units;
using UnityEngine;

namespace DawnOfShapes.UI
{
    /// <summary>
    /// Prototype IMGUI HUD: faction select, resources, scores, selection info, production, research,
    /// health bars and the end-of-match screen.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        private const float ReferenceHeight = 900f;

        [SerializeField] private SelectionManager selection;
        [SerializeField] private Camera targetCamera;

        private readonly List<Rect> blockingRects = new List<Rect>();
        private GUIStyle panel, title, label, small, button, centred, big;
        private float scale = 1f;

        public static HUD Instance { get; private set; }

        private MatchManager Match => MatchManager.Instance;

        public void Configure(SelectionManager selectionManager, Camera cam)
        {
            selection = selectionManager;
            targetCamera = cam;
        }

        private void Awake()
        {
            Instance = this;
            if (targetCamera == null) targetCamera = Camera.main;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>True when the screen point (Input System coordinates, y up) is over a HUD panel.</summary>
        public bool IsOverUI(Vector2 screenPos)
        {
            if (Match != null && Match.Phase != MatchPhase.Playing) return true;
            Vector2 gui = new Vector2(screenPos.x, Screen.height - screenPos.y) / scale;
            foreach (var r in blockingRects)
            {
                if (r.Contains(gui)) return true;
            }
            return false;
        }

        // Panel layout in scaled GUI units, shared by input blocking and drawing.
        private float W => Screen.width / scale;
        private float H => Screen.height / scale;
        private Rect TopBar => new Rect(0f, 0f, W, 64f);
        private Rect SelectionPanel => new Rect(10f, H - 170f, 330f, 160f);
        private Rect CommandPanel => new Rect(W - 450f, H - 300f, 440f, 290f);

        private void LateUpdate()
        {
            scale = Mathf.Max(1f, Screen.height / ReferenceHeight);
            blockingRects.Clear();
            blockingRects.Add(TopBar);
            blockingRects.Add(SelectionPanel);
            blockingRects.Add(CommandPanel);
        }

        private void OnGUI()
        {
            if (Match == null) return;
            EnsureStyles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            switch (Match.Phase)
            {
                case MatchPhase.Setup:
                    DrawFactionSelect();
                    break;
                case MatchPhase.Playing:
                    DrawHealthBars();
                    DrawTopBar();
                    DrawSelectionPanel();
                    DrawCommandPanel();
                    DrawModeHint();
                    break;
                case MatchPhase.GameOver:
                    DrawTopBar();
                    DrawGameOver();
                    break;
            }
            GUI.matrix = Matrix4x4.identity;
        }

        // ---- Screens ----

        private void DrawFactionSelect()
        {
            Fill(new Rect(0, 0, W, H), new Color(0.05f, 0.06f, 0.08f, 0.85f));
            Rect box = new Rect(W * 0.5f - 420f, H * 0.5f - 210f, 840f, 420f);
            GUI.Box(box, GUIContent.none, panel);
            GUI.Label(new Rect(box.x, box.y + 20f, box.width, 50f), "DAWN OF SHAPES", big);
            GUI.Label(new Rect(box.x, box.y + 70f, box.width, 24f), "Choose your faction. The AI takes a different one.", centred);

            var factions = Match.Factions;
            float cardW = (box.width - 40f - (factions.Count - 1) * 15f) / Mathf.Max(1, factions.Count);
            for (int i = 0; i < factions.Count; i++)
            {
                var f = factions[i];
                Rect card = new Rect(box.x + 20f + i * (cardW + 15f), box.y + 110f, cardW, 280f);
                Fill(card, new Color(0.12f, 0.13f, 0.16f, 1f));
                Fill(new Rect(card.x, card.y, card.width, 8f), f.Color);
                GUI.Label(new Rect(card.x + 12f, card.y + 18f, card.width - 24f, 30f), f.FactionName, title);
                GUI.Label(new Rect(card.x + 12f, card.y + 52f, card.width - 24f, 90f), f.Description, small);
                string units = string.Join("\n", f.Units.Where(u => u.producible).Select(u => "• " + u.displayName));
                GUI.Label(new Rect(card.x + 12f, card.y + 140f, card.width - 24f, 70f), units, small);
                if (Match.RememberedFaction == i)
                {
                    GUI.Label(new Rect(card.x + 12f, card.y + 205f, card.width - 24f, 20f), "(last played)", small);
                }
                if (GUI.Button(new Rect(card.x + 12f, card.yMax - 52f, card.width - 24f, 40f), "Play " + f.FactionName, button))
                {
                    Match.StartMatch(i);
                }
            }
        }

        private void DrawGameOver()
        {
            Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.55f));
            var local = Match.LocalState;
            bool won = local != null && Match.WinningTeam == local.TeamId;
            Rect box = new Rect(W * 0.5f - 260f, H * 0.5f - 130f, 520f, 260f);
            GUI.Box(box, GUIContent.none, panel);
            GUI.Label(new Rect(box.x, box.y + 25f, box.width, 50f), won ? "VICTORY" : "DEFEAT", big);
            GUI.Label(new Rect(box.x + 20f, box.y + 85f, box.width - 40f, 50f), Match.ResultReason, centred);
            GUI.Label(new Rect(box.x + 20f, box.y + 125f, box.width - 40f, 24f), $"Match time {FormatTime(Match.MatchTime)}", centred);
            if (GUI.Button(new Rect(box.x + 40f, box.yMax - 70f, 200f, 44f), "Restart", button)) Match.Restart();
            if (GUI.Button(new Rect(box.xMax - 240f, box.yMax - 70f, 200f, 44f), "Change faction", button)) Match.ReturnToFactionSelect();
        }

        // ---- In-game panels ----

        private void DrawTopBar()
        {
            Rect bar = TopBar;
            Fill(bar, new Color(0.06f, 0.07f, 0.09f, 0.85f));
            var local = Match.LocalState;
            if (local != null)
            {
                int pop = Match.Population(local.PlayerId);
                int cap = Match.UnitCap(local.PlayerId);
                GUI.Label(new Rect(14f, 8f, 260f, 24f), $"Form  {local.Form:F0}  (+{local.FormPerSecond:F1}/s)", title);
                GUI.Label(new Rect(14f, 36f, 260f, 22f), $"Units  {pop} / {cap}     Points  {Match.PointsHeldBy(local.PlayerId)} / {Match.Points.Count}", label);
            }

            GUI.Label(new Rect(W * 0.5f - 120f, 8f, 240f, 24f), FormatTime(Match.MatchTime), centred);
            if (Match.DominationTeam >= 0 && Match.Phase == MatchPhase.Playing)
            {
                float left = Match.Settings.dominationHoldTime - Match.DominationTimer;
                string who = local != null && Match.DominationTeam == local.TeamId ? "You hold" : "Enemy holds";
                GUI.Label(new Rect(W * 0.5f - 200f, 34f, 400f, 24f), $"{who} every point: domination in {left:F0}s", centred);
            }
            else
            {
                GUI.Label(new Rect(W * 0.5f - 260f, 34f, 520f, 24f),
                    "F attack-move · B wall · H stop · Ctrl+A select all · Esc cancel", centred);
            }

            // Victory point bars, one per player.
            var states = Match.States;
            float barW = 200f;
            for (int i = 0; i < states.Count; i++)
            {
                var s = states[i];
                float x = W - (states.Count - i) * (barW + 12f);
                float frac = Mathf.Clamp01(s.VictoryPoints / Match.Settings.startingVictoryPoints);
                GUI.Label(new Rect(x, 6f, barW, 20f), s.Player.DisplayName + (s.Eliminated ? " (out)" : ""), small);
                Fill(new Rect(x, 28f, barW, 14f), new Color(0.2f, 0.2f, 0.22f));
                Fill(new Rect(x, 28f, barW * frac, 14f), s.Player.Color);
                GUI.Label(new Rect(x, 43f, barW, 18f), $"{s.VictoryPoints:F0} VP   base {HealthPercent(s.Base)}", small);
            }
        }

        private void DrawSelectionPanel()
        {
            Rect r = SelectionPanel;
            GUI.Box(r, GUIContent.none, panel);
            var sel = selection != null ? selection.Selected : null;
            if (sel == null || sel.Count == 0)
            {
                GUI.Label(new Rect(r.x + 12f, r.y + 10f, r.width - 24f, r.height - 20f),
                    "Nothing selected.\nLeft-click or drag to select units.\nRight-click ground to move, an enemy to attack.", small);
                return;
            }

            if (sel.Count == 1)
            {
                var u = sel[0];
                var d = u.Definition;
                GUI.Label(new Rect(r.x + 12f, r.y + 8f, r.width - 24f, 26f), d.displayName, title);
                GUI.Label(new Rect(r.x + 12f, r.y + 36f, r.width - 24f, 110f),
                    $"HP {u.Health:F0} / {u.MaxHealth:F0}    Armour {u.Armour:F0}\n" +
                    $"Damage {u.Damage:F0} every {d.attackCooldown:F1}s    Range {d.range:F0} m\n" +
                    $"Speed {u.Speed:F1}{(u.IsSlowed ? " (slowed)" : "")}    Order {u.Order}\n" +
                    d.description, small);
                return;
            }

            GUI.Label(new Rect(r.x + 12f, r.y + 8f, r.width - 24f, 26f), $"{sel.Count} units selected", title);
            string groups = string.Join("\n", sel.GroupBy(u => u.Definition.displayName)
                .Select(g => $"{g.Count()} × {g.Key}   ({g.Sum(u => u.Health):F0} HP)"));
            GUI.Label(new Rect(r.x + 12f, r.y + 38f, r.width - 24f, 110f), groups, small);
        }

        private void DrawCommandPanel()
        {
            Rect r = CommandPanel;
            GUI.Box(r, GUIContent.none, panel);
            var local = Match.LocalState;
            if (local == null || local.Base == null || local.Base.IsDead) return;
            var baseB = local.Base;
            float y = r.y + 8f;

            // Production
            GUI.Label(new Rect(r.x + 12f, y, 300f, 24f), "Base: train units", title);
            y += 28f;
            var producible = local.Faction.Units.Where(u => u.producible).ToList();
            float bw = (r.width - 24f - (producible.Count - 1) * 8f) / Mathf.Max(1, producible.Count);
            for (int i = 0; i < producible.Count; i++)
            {
                var def = producible[i];
                GUI.enabled = baseB.CanQueue(def);
                if (GUI.Button(new Rect(r.x + 12f + i * (bw + 8f), y, bw, 42f),
                        $"{def.displayName}\n{baseB.CostOf(def)} Form · {baseB.BuildTimeOf(def):F0}s", button))
                {
                    baseB.TryQueue(def);
                }
            }
            GUI.enabled = true;
            y += 48f;

            string queueText;
            if (baseB.Queue.Count == 0) queueText = "Queue empty";
            else
            {
                queueText = $"Building {baseB.Queue[0].displayName} {baseB.Progress01 * 100f:F0}%";
                if (baseB.IsBlockedByCap) queueText += " (unit cap reached: hold more points)";
                if (baseB.Queue.Count > 1) queueText += $"   +{baseB.Queue.Count - 1} queued";
            }
            Fill(new Rect(r.x + 12f, y, r.width - 24f, 6f), new Color(0.2f, 0.2f, 0.22f));
            Fill(new Rect(r.x + 12f, y, (r.width - 24f) * baseB.Progress01, 6f), local.Player.Color);
            GUI.Label(new Rect(r.x + 12f, y + 8f, r.width - 24f, 22f), queueText, small);
            y += 36f;

            // Research
            var research = local.Research != null && !local.Research.IsDead ? local.Research : null;
            GUI.Label(new Rect(r.x + 12f, y, 400f, 24f), "Research: " + local.Faction.ResearchBuildingName, title);
            y += 28f;
            if (research == null)
            {
                GUI.enabled = Match.CanBuildResearch(local.PlayerId);
                if (GUI.Button(new Rect(r.x + 12f, y, r.width - 24f, 40f),
                        $"Build {local.Faction.ResearchBuildingName} ({local.Faction.ResearchBuildingCost} Form)", button))
                {
                    Match.TryBuildResearch(local.PlayerId);
                }
                GUI.enabled = true;
                y += 46f;
            }
            else
            {
                var upgrades = local.Faction.Upgrades;
                float uw = (r.width - 24f - (upgrades.Count - 1) * 8f) / Mathf.Max(1, upgrades.Count);
                for (int i = 0; i < upgrades.Count; i++)
                {
                    var up = upgrades[i];
                    string text;
                    if (local.HasUpgrade(up)) text = $"{up.displayName}\nDone";
                    else if (research.Current == up) text = $"{up.displayName}\n{research.Progress01 * 100f:F0}%";
                    else text = $"{up.displayName}\n{up.cost} Form · {up.researchTime:F0}s";
                    GUI.enabled = research.CanResearch(up);
                    if (GUI.Button(new Rect(r.x + 12f + i * (uw + 8f), y, uw, 42f), new GUIContent(text, up.description), button))
                    {
                        research.TryResearch(up);
                    }
                }
                GUI.enabled = true;
                y += 48f;
                if (!string.IsNullOrEmpty(GUI.tooltip)) GUI.Label(new Rect(r.x + 12f, y, r.width - 24f, 20f), GUI.tooltip, small);
                y += 4f;
            }

            // Builder ability
            if (selection != null && selection.HasBuilderSelected)
            {
                GUI.enabled = local.Form >= Match.Settings.wallCost;
                if (GUI.Button(new Rect(r.x + 12f, r.yMax - 50f, r.width - 24f, 40f),
                        $"Build wall block ({Match.Settings.wallCost} Form)  [B]", button))
                {
                    selection.BeginWallPlacement();
                }
                GUI.enabled = true;
            }
        }

        private void DrawModeHint()
        {
            if (selection == null || selection.Mode == CommandMode.None) return;
            string text = selection.Mode == CommandMode.AttackMove
                ? "Attack-move: left-click a destination (right-click to cancel)"
                : "Place wall: left-click where the wall should go (right-click to cancel)";
            GUI.Label(new Rect(W * 0.5f - 300f, 74f, 600f, 26f), text, centred);
        }

        private void DrawHealthBars()
        {
            if (targetCamera == null) return;
            foreach (var e in Entity.All)
            {
                if (e.IsDead) continue;
                bool selectedUnit = e is Unit u && u.IsSelected;
                if (e.Health >= e.MaxHealth && !selectedUnit) continue;

                float height = e is Unit unit ? unit.Definition.scale.y + 0.6f : e.Radius * 1.6f + 1.5f;
                Vector3 sp = targetCamera.WorldToScreenPoint(e.transform.position + Vector3.up * height);
                if (sp.z <= 0f) continue;
                Vector2 gui = new Vector2(sp.x, Screen.height - sp.y) / scale;
                float w = e is Unit ? 36f : 80f;
                var player = PlayerRegistry.Instance.GetPlayer(e.OwnerPlayerId);
                Fill(new Rect(gui.x - w * 0.5f - 1f, gui.y - 1f, w + 2f, 6f), new Color(0f, 0f, 0f, 0.7f));
                Fill(new Rect(gui.x - w * 0.5f, gui.y, w * (e.Health / e.MaxHealth), 4f), player != null ? player.Color : Color.green);
            }
        }

        // ---- Helpers ----

        private static string HealthPercent(Entity e) => e != null && !e.IsDead ? $"{e.Health / e.MaxHealth * 100f:F0}%" : "destroyed";

        private static string FormatTime(float seconds) => $"{(int)(seconds / 60f):00}:{(int)(seconds % 60f):00}";

        private static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void EnsureStyles()
        {
            if (panel != null) return;
            var bg = new Texture2D(1, 1);
            bg.SetPixel(0, 0, new Color(0.08f, 0.09f, 0.11f, 0.88f));
            bg.Apply();
            panel = new GUIStyle(GUI.skin.box) { normal = { background = bg } };
            label = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = new Color(0.9f, 0.9f, 0.92f) } };
            small = new GUIStyle(label) { fontSize = 13, wordWrap = true };
            title = new GUIStyle(label) { fontSize = 18, fontStyle = FontStyle.Bold };
            centred = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            big = new GUIStyle(title) { fontSize = 36, alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(GUI.skin.button) { fontSize = 13, wordWrap = true };
        }
    }
}
