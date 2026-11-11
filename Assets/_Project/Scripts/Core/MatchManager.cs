using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DawnOfShapes.AI;
using DawnOfShapes.Buildings;
using DawnOfShapes.CameraControl;
using DawnOfShapes.Factions;
using DawnOfShapes.Map;
using DawnOfShapes.Units;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DawnOfShapes.Core
{
    public enum MatchPhase
    {
        Setup,
        Playing,
        GameOver
    }

    /// <summary>
    /// Owns the match: sets up players from spawn slots, runs the economy and checks win conditions.
    /// Command line: -autoplay (AI vs AI, quits at game over), -faction N (player 1's faction index),
    /// -timescale N, -maxSeconds N.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class MatchManager : MonoBehaviour
    {
        // Survive scene reloads so Restart keeps the same faction choice.
        private static int rememberedFaction = -1;
        private static bool skipSetupOnLoad;

        [SerializeField] private MatchSettings settings;
        [SerializeField] private GameVisuals visuals;
        [SerializeField] private List<FactionDefinition> factions = new List<FactionDefinition>();
        [SerializeField] private RTSCameraController cameraRig;

        private readonly List<PlayerState> states = new List<PlayerState>();
        private readonly List<CapturePoint> points = new List<CapturePoint>();
        private readonly List<SpawnSlot> slots = new List<SpawnSlot>();

        private int dominationTeam = -1;
        private bool autoplay;
        private float autoplayTimeScale = 1f;
        private int autoplayFaction = -1;
        private float maxRealSeconds;
        private float nextStatusLog;

        public static MatchManager Instance { get; private set; }

        public MatchSettings Settings => settings;
        public GameVisuals Visuals => visuals;
        public IReadOnlyList<FactionDefinition> Factions => factions;
        public IReadOnlyList<PlayerState> States => states;
        public IReadOnlyList<CapturePoint> Points => points;
        public MatchPhase Phase { get; private set; } = MatchPhase.Setup;
        public float MatchTime { get; private set; }
        public int WinningTeam { get; private set; } = -1;
        public string ResultReason { get; private set; }
        public float DominationTimer { get; private set; }
        public int DominationTeam => dominationTeam;
        public int RememberedFaction => rememberedFaction;

        public event Action<MatchPhase> PhaseChanged;

        private void Awake()
        {
            Instance = this;
            points.AddRange(FindObjectsByType<CapturePoint>(FindObjectsSortMode.None).OrderBy(p => p.PointId));
            slots.AddRange(FindObjectsByType<SpawnSlot>(FindObjectsSortMode.None).OrderBy(s => s.SlotIndex));
            ParseCommandLine();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (autoplay) StartMatch(autoplayFaction >= 0 ? autoplayFaction : UnityEngine.Random.Range(0, factions.Count));
            else if (skipSetupOnLoad && rememberedFaction >= 0) StartMatch(rememberedFaction);
        }

        private void ParseCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-autoplay":
                        autoplay = true;
                        break;
                    case "-timescale" when i + 1 < args.Length:
                        float.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out autoplayTimeScale);
                        break;
                    case "-faction" when i + 1 < args.Length:
                        int.TryParse(args[++i], out autoplayFaction);
                        break;
                    case "-maxSeconds" when i + 1 < args.Length:
                        float.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out maxRealSeconds);
                        break;
                }
            }
        }

        // ---- Setup ----

        /// <summary>
        /// Starts a match with the local player on the given faction. Every other spawn slot gets an AI
        /// on a different faction (cycling if there are more slots than factions).
        /// </summary>
        public void StartMatch(int localFactionIndex)
        {
            if (Phase != MatchPhase.Setup || factions.Count == 0 || slots.Count == 0) return;
            localFactionIndex = Mathf.Clamp(localFactionIndex, 0, factions.Count - 1);
            rememberedFaction = localFactionIndex;

            var others = Enumerable.Range(0, factions.Count).Where(i => i != localFactionIndex)
                .OrderBy(_ => UnityEngine.Random.value).ToList();
            if (others.Count == 0) others.Add(localFactionIndex);

            var players = new List<Player>();
            for (int i = 0; i < slots.Count; i++)
            {
                var faction = factions[i == 0 ? localFactionIndex : others[(i - 1) % others.Count]];
                bool human = i == 0 && !autoplay;
                string label = human ? "You" : $"AI {i}";
                players.Add(new Player(i, i, $"{label} ({faction.FactionName})", faction, slots[i].SlotIndex, human));
            }
            PlayerRegistry.Instance.SetPlayers(players, 0);

            Vector3 centre = MapCentre();
            foreach (var player in players)
            {
                var state = new PlayerState(player, settings.startingForm, settings.startingVictoryPoints);
                states.Add(state);

                Vector3 spawn = slots[player.SpawnSlot].transform.position;
                Vector3 toCentre = centre - spawn;
                toCentre.y = 0f;
                Quaternion facing = Quaternion.LookRotation(toCentre.sqrMagnitude > 0.01f ? toCentre.normalized : Vector3.forward);
                state.Base = BuildingFactory.SpawnBase(state, settings, spawn, facing);

                var starting = player.Faction.StartingUnits;
                for (int u = 0; u < starting.Count; u++)
                {
                    Vector3 pos = state.Base.RallyPoint + facing * new Vector3((u - (starting.Count - 1) * 0.5f) * 2.5f, 0f, 0f);
                    UnitFactory.Spawn(starting[u], player.PlayerId, pos, facing);
                }

                if (!player.IsHuman)
                {
                    gameObject.AddComponent<AIController>().Initialise(player.PlayerId);
                }
            }

            if (cameraRig != null)
            {
                var local = states[0];
                cameraRig.JumpTo(local.Base.transform.position + local.Base.transform.forward * 12f, local.Base.transform.eulerAngles.y);
            }

            Time.timeScale = autoplay ? autoplayTimeScale : 1f;
            MatchTime = 0f;
            SetPhase(MatchPhase.Playing);
            Debug.Log($"[DawnOfShapes] Match started: {string.Join(" vs ", players.Select(p => p.DisplayName))}");
        }

        private Vector3 MapCentre()
        {
            if (points.Count == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            foreach (var p in points) sum += p.transform.position;
            return sum / points.Count;
        }

        // ---- Queries ----

        public PlayerState GetState(int playerId)
        {
            foreach (var s in states)
            {
                if (s.PlayerId == playerId) return s;
            }
            return null;
        }

        public PlayerState LocalState => GetState(PlayerRegistry.Instance != null ? PlayerRegistry.Instance.LocalPlayerId : 0);

        public int PointsHeldBy(int playerId) => points.Count(p => p.OwnerPlayerId == playerId);

        public int UnitCap(int playerId) => settings.baseUnitCap + settings.unitCapPerPoint * PointsHeldBy(playerId);

        public int Population(int playerId)
        {
            int pop = 0;
            foreach (var u in Unit.AllUnits)
            {
                if (!u.IsDead && u.OwnerPlayerId == playerId) pop += u.Definition.popCost;
            }
            return pop;
        }

        public bool HasPopRoom(int playerId, int popCost) => Population(playerId) + popCost <= UnitCap(playerId);

        // ---- Actions ----

        public bool CanBuildResearch(int playerId)
        {
            var s = GetState(playerId);
            return s != null && Phase == MatchPhase.Playing && (s.Research == null || s.Research.IsDead)
                   && s.Base != null && !s.Base.IsDead && s.Form >= s.Faction.ResearchBuildingCost;
        }

        public bool TryBuildResearch(int playerId)
        {
            if (!CanBuildResearch(playerId)) return false;
            var s = GetState(playerId);
            s.Form -= s.Faction.ResearchBuildingCost;
            Transform b = s.Base.transform;
            Vector3 pos = b.position + b.right * (s.Base.Radius + settings.researchBuildingSize.x * 0.5f + 5f) - b.forward * 2f;
            s.Research = BuildingFactory.SpawnResearch(s, settings, pos, b.rotation);
            return true;
        }

        public bool TryBuildWall(Unit builder, Vector3 point)
        {
            var s = GetState(builder.OwnerPlayerId);
            if (s == null || s.Form < settings.wallCost) return false;
            s.Form -= settings.wallCost;
            Vector3 dir = point - builder.transform.position;
            dir.y = 0f;
            Quaternion rot = Quaternion.LookRotation(dir.sqrMagnitude > 0.01f ? dir.normalized : builder.transform.forward);
            BuildingFactory.SpawnWall(s, settings, visuals.boxMesh, point, rot);
            return true;
        }

        public void Restart()
        {
            skipSetupOnLoad = true;
            ReloadScene();
        }

        public void ReturnToFactionSelect()
        {
            skipSetupOnLoad = false;
            ReloadScene();
        }

        private static void ReloadScene()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ---- Simulation ----

        private void Update()
        {
            if (autoplay && maxRealSeconds > 0f && Time.realtimeSinceStartup > maxRealSeconds)
            {
                Debug.Log($"[DawnOfShapes] Autoplay time limit reached at match time {MatchTime:F0}s with no winner.");
                LogStatus();
                Application.Quit(0);
                return;
            }
            if (Phase != MatchPhase.Playing) return;

            float dt = Time.deltaTime;
            MatchTime += dt;

            foreach (var point in points) point.Tick(dt, settings.captureTime);

            foreach (var s in states)
            {
                if (s.Eliminated) continue;
                s.FormPerSecond = settings.baseFormPerSecond + settings.formPerPointPerSecond * PointsHeldBy(s.PlayerId);
                s.Form += s.FormPerSecond * dt;
            }

            // Victory points: every held point drains each enemy player.
            foreach (var point in points)
            {
                if (point.OwnerPlayerId < 0) continue;
                foreach (var s in states)
                {
                    if (!s.Eliminated && s.TeamId != point.OwnerTeamId)
                    {
                        s.VictoryPoints = Mathf.Max(0f, s.VictoryPoints - settings.victoryPointDrainPerPoint * dt);
                    }
                }
            }
            foreach (var s in states)
            {
                if (!s.Eliminated && s.VictoryPoints <= 0f) Eliminate(s, "ran out of victory points");
            }

            UpdateDomination(dt);
            CheckLastTeamStanding();

            if (autoplay && MatchTime >= nextStatusLog)
            {
                nextStatusLog += 60f;
                LogStatus();
            }
        }

        private void UpdateDomination(float dt)
        {
            if (Phase != MatchPhase.Playing || points.Count == 0) return;
            int team = points[0].OwnerPlayerId >= 0 ? points[0].OwnerTeamId : -1;
            foreach (var p in points)
            {
                if (p.OwnerPlayerId < 0 || p.OwnerTeamId != team)
                {
                    team = -1;
                    break;
                }
            }

            if (team < 0)
            {
                dominationTeam = -1;
                DominationTimer = 0f;
                return;
            }
            if (team != dominationTeam)
            {
                dominationTeam = team;
                DominationTimer = 0f;
            }
            DominationTimer += dt;
            if (DominationTimer >= settings.dominationHoldTime) EndMatch(team, "Domination: held every capture point");
        }

        public void OnEntityDied(Entity entity)
        {
            if (entity is BaseBuilding building)
            {
                var s = GetState(building.OwnerPlayerId);
                if (s != null && !s.Eliminated) Eliminate(s, "base destroyed");
            }
        }

        private void Eliminate(PlayerState s, string reason)
        {
            s.Eliminated = true;
            s.EliminationReason = reason;
            Debug.Log($"[DawnOfShapes] {s.Player.DisplayName} eliminated at {MatchTime:F0}s: {reason}.");
            CheckLastTeamStanding();
        }

        private void CheckLastTeamStanding()
        {
            if (Phase != MatchPhase.Playing) return;
            var teams = states.Where(s => !s.Eliminated).Select(s => s.TeamId).Distinct().ToList();
            if (teams.Count > 1) return;

            string reason = "last team standing";
            var loser = states.FirstOrDefault(s => s.Eliminated && s.EliminationReason != null);
            if (loser != null)
            {
                reason = loser.EliminationReason == "base destroyed" ? "Annihilation: enemy base destroyed" : "Victory points: enemy score reached zero";
            }
            EndMatch(teams.Count == 1 ? teams[0] : -1, reason);
        }

        private void EndMatch(int winningTeam, string reason)
        {
            if (Phase == MatchPhase.GameOver) return;
            WinningTeam = winningTeam;
            ResultReason = reason;
            SetPhase(MatchPhase.GameOver);
            string winners = string.Join(", ", states.Where(s => s.TeamId == winningTeam).Select(s => s.Player.DisplayName));
            Debug.Log($"[DawnOfShapes] Match over at {MatchTime:F0}s. Winner: {(winners.Length > 0 ? winners : "none")} ({reason}).");
            LogStatus();

            if (autoplay) Application.Quit(0);
            else Time.timeScale = 0f;
        }

        private void LogStatus()
        {
            foreach (var s in states)
            {
                Debug.Log($"[DawnOfShapes] t={MatchTime:F0}s {s.Player.DisplayName}: form={s.Form:F0} pop={Population(s.PlayerId)}/{UnitCap(s.PlayerId)} " +
                          $"points={PointsHeldBy(s.PlayerId)} vp={s.VictoryPoints:F0} baseHp={(s.Base != null ? s.Base.Health : 0):F0} " +
                          $"research={(s.Research != null ? "yes" : "no")} eliminated={s.Eliminated}");
            }
        }

        private void SetPhase(MatchPhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }
}
