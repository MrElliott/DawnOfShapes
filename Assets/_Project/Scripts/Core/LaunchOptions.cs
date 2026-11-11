using System;
using System.Globalization;

namespace DawnOfShapes.Core
{
    /// <summary>
    /// Command-line options for headless testing.
    /// <list type="bullet">
    /// <item>-autoplay: AI vs AI full match, quits when it ends.</item>
    /// <item>-faction X / -opponent X: faction name or index for player 1 / player 2.</item>
    /// <item>-skirmish "BlockTrooper:10" "Roller:@600": two fixed armies fight on open ground, no economy.
    /// "Unit:N" is a count, "Unit:@F" spends F Form on that unit. Combine with commas.</item>
    /// <item>-seed N: seeds all random choices.</item>
    /// <item>-timescale N, -maxMatchTime N (game seconds, then a draw), -maxSeconds N (real seconds).</item>
    /// <item>-balance path.json: stat overrides (see BalanceOverrides).</item>
    /// <item>-results path.json: write a result file. -listUnits: write the unit catalogue there and quit.</item>
    /// </list>
    /// </summary>
    public class LaunchOptions
    {
        public bool Autoplay;
        public string Faction;
        public string Opponent;
        public string SkirmishA;
        public string SkirmishB;
        public int? Seed;
        public float TimeScale = 1f;
        public float MaxMatchTime;
        public float MaxRealSeconds;
        public string BalancePath;
        public string ResultsPath;
        public bool ListUnits;

        public bool IsSkirmish => SkirmishA != null && SkirmishB != null;
        public bool IsHeadlessRun => Autoplay || IsSkirmish || ListUnits;

        public static LaunchOptions Parse(string[] args)
        {
            var o = new LaunchOptions();
            for (int i = 0; i < args.Length; i++)
            {
                bool hasNext = i + 1 < args.Length;
                switch (args[i])
                {
                    case "-autoplay": o.Autoplay = true; break;
                    case "-listUnits": o.ListUnits = true; break;
                    case "-faction" when hasNext: o.Faction = args[++i]; break;
                    case "-opponent" when hasNext: o.Opponent = args[++i]; break;
                    case "-balance" when hasNext: o.BalancePath = args[++i]; break;
                    case "-results" when hasNext: o.ResultsPath = args[++i]; break;
                    case "-seed" when hasNext:
                        if (int.TryParse(args[++i], out int seed)) o.Seed = seed;
                        break;
                    case "-timescale" when hasNext: o.TimeScale = ParseFloat(args[++i], 1f); break;
                    case "-maxMatchTime" when hasNext: o.MaxMatchTime = ParseFloat(args[++i], 0f); break;
                    case "-maxSeconds" when hasNext: o.MaxRealSeconds = ParseFloat(args[++i], 0f); break;
                    case "-skirmish" when i + 2 < args.Length:
                        o.SkirmishA = args[++i];
                        o.SkirmishB = args[++i];
                        break;
                }
            }
            return o;
        }

        public static LaunchOptions FromEnvironment() => Parse(Environment.GetCommandLineArgs());

        private static float ParseFloat(string s, float fallback)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
        }
    }
}
