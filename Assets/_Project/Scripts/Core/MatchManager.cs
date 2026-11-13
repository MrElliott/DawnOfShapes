using System;
using System.Collections.Generic;
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

    public enum MatchMode
    {
        Full,
        Skirmish
    }

    /// <summary>
    /// Owns the match: sets up players from spawn slots, runs the economy and checks win conditions.
    /// Also drives headless test runs; see <see cref="LaunchOptions"/> for the command line.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class MatchManager : MonoBehaviour
    {
        // Survive scene reloads so Restart keeps the same faction choice.
        private static int rememberedFaction = -1;
        private static bool skipSetupOnLoad;

        private const float PointSampleInterval = 30f;
        private static readonly Vector3 SkirmishCentre = new Vector3(0f, 0f, -25f);

        [SerializeField] private MatchSettings settings;
        [SerializeField] private GameVisuals visuals;
        [SerializeField] private List<FactionDefinition> factions = new List<FactionDefinition>();
        [SerializeField] private RTSCameraController cameraRig;

        private readonly List<PlayerState> states = new List<PlayerState>();
        private readonly List<CapturePoint> points = new List<CapturePoint>();
        private readonly List<SpawnSlot> slots = new List<SpawnSlot>();

        private LaunchOptions options;
        private int dominationTeam = -1;
        private float nextStatusLog;
        private float nextPointSample;

        public static MatchManager Instance { get; private set; }

        public MatchSettings Settings => settings;
        public GameVisuals Visuals => visuals;
        /// <summary>Runtime copy of the counter matrix, with any balance overrides applied.</summary>
        public CounterMatrix Counters { get; private set; }
        public IReadOnlyList<FactionDefinition> Factions => factions;
        public IReadOnlyList<PlayerState> States => states;
        public IReadOnlyList<CapturePoint> Points => points;
        public MatchPhase Phase { get; private set; } = MatchPhase.Setup;
        public MatchMode Mode { get; private set; } = MatchMode.Full;
        public float MatchTime { get; private set; }
        public int WinningTeam { get; private set; } = -1;
        public string WinCondition { get; private set; }
        public string ResultReason { get; private set; }
        public float DominationTimer { get; private set; }
        public int DominationTeam => dominationTeam;
        public int RememberedFaction => rememberedFaction;
        public int Seed { get; private set; }
        public LaunchOptions Options => options;

        public event Action<MatchPhase> PhaseChanged;

        private void Awake()
        {
            Instance = this;
            points.AddRange(FindObjectsByType<CapturePoint>(FindObjectsSortMode.None).OrderBy(p => p.PointId));
            slots.AddRange(FindObjectsByType<SpawnSlot>(FindObjectsSortMode.None).OrderBy(s => s.SlotIndex));

            options = LaunchOptions.FromEnvironment();
            Seed = options.Seed ?? Environment.TickCount;
            UnityEngine.Random.InitState(Seed);
            Counters = settings.counters != null ? Instantiate(settings.counters) : ScriptableObject.CreateInstance<CounterMatrix>();
            if (options.BalancePath != null) BalanceOverrides.Apply(options.BalancePath, factions, settings, Counters);
            ResolveUnitShapes();
            Debug.Log($"[DawnOfShapes] Counter matrix {Counters}");
            if (options.IsHeadlessRun)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
            }
        }

        /// <summary>Gives every unit its effective counter shape (its own class, or its faction's shape).</summary>
        private void ResolveUnitShapes()
        {
            foreach (var f in factions)
            {
                foreach (var u in f.Units)
                {
                    if (u == null) continue;
                    u.ResolveShape(f.Shape);
                    if (u.splitInto != null) u.splitInto.ResolveShape(f.Shape);
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (options.ListUnits)
            {
                MatchResults.WriteUnitCatalogue(options.ResultsPath, factions);
                Application.Quit(0);
            }
            else if (options.IsSkirmish)
            {
                StartSkirmish(options.SkirmishA, options.SkirmishB);
            }
            else if (options.Autoplay)
            {
                int local = FindFaction(options.Faction);
                StartMatch(local >= 0 ? local : UnityEngine.Random.Range(0, factions.Count), FindFaction(options.Opponent));
            }
            else if (skipSetupOnLoad && rememberedFaction >= 0)
            {
                StartMatch(rememberedFaction);
            }
        }

        // ---- Setup ----

        public int FindFaction(string nameOrIndex)
        {
            if (string.IsNullOrEmpty(nameOrIndex)) return -1;
            if (int.TryParse(nameOrIndex, out int index)) return index >= 0 && index < factions.Count ? index : -1;
            return factions.FindIndex(f => BalanceOverrides.Matches(nameOrIndex, f.FactionName, f.name));
        }

        /// <summary>
        /// Starts a match with the local player on the given faction. Every other spawn slot gets an AI on a
        /// different faction (the first one on <paramref name="opponentFactionIndex"/> when given).
        /// </summary>
        public void StartMatch(int localFactionIndex, int opponentFactionIndex = -1)
        {
            if (Phase != MatchPhase.Setup || factions.Count == 0 || slots.Count == 0) return;
            localFactionIndex = Mathf.Clamp(localFactionIndex, 0, factions.Count - 1);
            rememberedFaction = localFactionIndex;

            var others = Enumerable.Range(0, factions.Count).Where(i => i != localFactionIndex)
                .OrderBy(_ => UnityEngine.Random.value).ToList();
            if (opponentFactionIndex >= 0)
            {
                others.Remove(opponentFactionIndex);
                others.Insert(0, opponentFactionIndex);
            }
            if (others.Count == 0) others.Add(localFactionIndex);

            var players = new List<Player>();
            for (int i = 0; i < slots.Count; i++)
            {
                var faction = factions[i == 0 ? localFactionIndex : others[(i - 1) % others.Count]];
                bool human = i == 0 && !options.Autoplay;
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

            Mode = MatchMode.Full;
            BeginPlaying();
            Debug.Log($"[DawnOfShapes] Match started (seed {Seed}): {string.Join(" vs ", players.Select(p => p.DisplayName))}");
        }

        /// <summary>
        /// Two fixed armies on open ground with no economy, fighting until one side has no units left.
        /// </summary>
        public void StartSkirmish(string armyA, string armyB)
        {
            if (Phase != MatchPhase.Setup) return;
            var armies = new[] { ParseArmy(armyA), ParseArmy(armyB) };
            if (armies[0].Count == 0 || armies[1].Count == 0)
            {
                Debug.LogError($"[DawnOfShapes] Skirmish armies could not be parsed: '{armyA}' vs '{armyB}'");
                Application.Quit(2);
                return;
            }

            var players = new List<Player>();
            for (int i = 0; i < 2; i++)
            {
                var faction = factions.FirstOrDefault(f => f.Units.Contains(armies[i][0])) ?? factions[0];
                players.Add(new Player(i, i, $"Army {(char)('A' + i)} ({(i == 0 ? armyA : armyB)})", faction, 0, false));
            }
            PlayerRegistry.Instance.SetPlayers(players, 0);

            for (int i = 0; i < 2; i++)
            {
                var state = new PlayerState(players[i], 0f, settings.startingVictoryPoints);
                states.Add(state);
                float side = i == 0 ? -1f : 1f;
                Quaternion facing = Quaternion.LookRotation(new Vector3(-side, 0f, 0f));
                var army = armies[i];
                int columns = Mathf.CeilToInt(Mathf.Sqrt(army.Count));
                for (int u = 0; u < army.Count; u++)
                {
                    float spacing = Mathf.Max(Formation.MinimumSpacing, army[u].Radius * Formation.SpacingPerRadius);
                    Vector3 pos = SkirmishCentre + new Vector3(side * (14f + (u / columns) * spacing), 0f,
                        (u % columns - (columns - 1) * 0.5f) * spacing);
                    UnitFactory.Spawn(army[u], i, pos, facing);
                    state.Stats.Add(state.Stats.UnitsBuilt, MatchStats.UnitKey(army[u].displayName));
                    state.Stats.FormSpent += army[u].cost;
                }
            }

            gameObject.AddComponent<SkirmishController>();
            Mode = MatchMode.Skirmish;
            BeginPlaying();
            Debug.Log($"[DawnOfShapes] Skirmish started (seed {Seed}): {armyA} vs {armyB}");
        }

        /// <summary>Parses "Unit:N,Unit:@Form" into a flat list of unit definitions.</summary>
        public List<UnitDefinition> ParseArmy(string spec)
        {
            var result = new List<UnitDefinition>();
            var catalogue = BalanceOverrides.AllUnits(factions);
            foreach (var part in spec.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var bits = part.Split(':');
                var def = catalogue.Find(u => BalanceOverrides.Matches(bits[0].Trim(), u.displayName, u.name));
                if (def == null)
                {
                    Debug.LogError($"[DawnOfShapes] Unknown unit '{bits[0]}' in army '{spec}'");
                    continue;
                }
                int count = 1;
                if (bits.Length > 1)
                {
                    string n = bits[1].Trim();
                    if (n.StartsWith("@") && float.TryParse(n.Substring(1), out float budget))
                        count = Mathf.Max(1, Mathf.RoundToInt(budget / Mathf.Max(1, def.cost)));
                    else int.TryParse(n, out count);
                }
                for (int i = 0; i < count; i++) result.Add(def);
            }
            return result;
        }

        private void BeginPlaying()
        {
            Time.timeScale = options.IsHeadlessRun ? options.TimeScale : 1f;
            MatchTime = 0f;
            nextPointSample = 0f;
            SetPhase(MatchPhase.Playing);
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
            s.TrySpend(s.Faction.ResearchBuildingCost);
            Transform b = s.Base.transform;
            Vector3 pos = b.position + b.right * (s.Base.Radius + settings.researchBuildingSize.x * 0.5f + 5f) - b.forward * 2f;
            s.Research = BuildingFactory.SpawnResearch(s, settings, pos, b.rotation);
            return true;
        }

        public bool TryBuildWall(Unit builder, Vector3 point)
        {
            var s = GetState(builder.OwnerPlayerId);
            if (s == null || !s.TrySpend(settings.wallCost)) return false;
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
            if (options.MaxRealSeconds > 0f && options.IsHeadlessRun && Phase == MatchPhase.Playing
                && Time.realtimeSinceStartup > options.MaxRealSeconds)
            {
                EndMatch(-1, "timeout", "Real-time limit reached");
                return;
            }
            if (Phase != MatchPhase.Playing) return;

            float dt = Time.deltaTime;
            MatchTime += dt;
            if (options.MaxMatchTime > 0f && MatchTime >= options.MaxMatchTime)
            {
                EndMatch(-1, "timeout", "Match time limit reached");
                return;
            }
            if (Mode == MatchMode.Skirmish) return;

            foreach (var point in points) point.Tick(dt, settings.captureTime);

            foreach (var s in states)
            {
                if (s.Eliminated) continue;
                s.FormPerSecond = settings.baseFormPerSecond + settings.formPerPointPerSecond * PointsHeldBy(s.PlayerId);
                s.Form += s.FormPerSecond * dt;
                s.Stats.FormEarned += s.FormPerSecond * dt;
            }

            if (MatchTime >= nextPointSample)
            {
                nextPointSample += PointSampleInterval;
                foreach (var s in states) s.Stats.PointsHeldSamples.Add(PointsHeldBy(s.PlayerId));
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
                if (!s.Eliminated && s.VictoryPoints <= 0f) Eliminate(s, "victory_points");
            }

            UpdateDomination(dt);
            CheckLastTeamStanding();

            if (options.Autoplay && MatchTime >= nextStatusLog)
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
            if (DominationTimer >= settings.dominationHoldTime) EndMatch(team, "domination", "Domination: held every capture point");
        }

        public void OnEntityDied(Entity entity)
        {
            if (Mode == MatchMode.Full && entity is BaseBuilding building)
            {
                var s = GetState(building.OwnerPlayerId);
                if (s != null && !s.Eliminated) Eliminate(s, "annihilation");
            }
        }

        private void Eliminate(PlayerState s, string condition)
        {
            s.Eliminated = true;
            s.EliminationReason = condition;
            Debug.Log($"[DawnOfShapes] {s.Player.DisplayName} eliminated at {MatchTime:F0}s: {condition}.");
            CheckLastTeamStanding();
        }

        private void CheckLastTeamStanding()
        {
            if (Phase != MatchPhase.Playing) return;
            var teams = states.Where(s => !s.Eliminated).Select(s => s.TeamId).Distinct().ToList();
            if (teams.Count > 1) return;

            var loser = states.FirstOrDefault(s => s.Eliminated && s.EliminationReason != null);
            string condition = loser != null ? loser.EliminationReason : "last_team_standing";
            string reason = condition == "annihilation" ? "Annihilation: enemy base destroyed"
                : condition == "victory_points" ? "Victory points: enemy score reached zero" : "Last team standing";
            EndMatch(teams.Count == 1 ? teams[0] : -1, condition, reason);
        }

        /// <summary>Called by SkirmishController when one side has no units left.</summary>
        public void EndSkirmish(int winningTeam)
        {
            EndMatch(winningTeam, winningTeam >= 0 ? "elimination" : "draw", winningTeam >= 0 ? "Army destroyed" : "Both armies destroyed");
        }

        private void EndMatch(int winningTeam, string condition, string reason)
        {
            if (Phase == MatchPhase.GameOver) return;
            WinningTeam = winningTeam;
            WinCondition = condition;
            ResultReason = reason;
            SetPhase(MatchPhase.GameOver);
            string winners = string.Join(", ", states.Where(s => s.TeamId == winningTeam).Select(s => s.Player.DisplayName));
            Debug.Log($"[DawnOfShapes] Match over at {MatchTime:F0}s. Winner: {(winners.Length > 0 ? winners : "none")} ({reason}).");
            if (Mode == MatchMode.Full) LogStatus();

            if (options.ResultsPath != null) MatchResults.Write(options.ResultsPath, this);
            if (options.IsHeadlessRun) Application.Quit(0);
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
