using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DawnOfShapes.Factions;
using DawnOfShapes.Units;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DawnOfShapes.Core
{
    /// <summary>
    /// Writes machine-readable results for headless balance runs.
    /// </summary>
    public static class MatchResults
    {
        public static void Write(string path, MatchManager match)
        {
            var players = new JArray();
            foreach (var s in match.States)
            {
                var stats = s.Stats;
                var p = new JObject
                {
                    ["playerId"] = s.PlayerId,
                    ["team"] = s.TeamId,
                    ["faction"] = s.Faction.FactionName,
                    ["won"] = s.TeamId == match.WinningTeam,
                    ["formEarned"] = Round(stats.FormEarned),
                    ["formSpent"] = Round(stats.FormSpent),
                    ["formBanked"] = Round(s.Form),
                    ["victoryPoints"] = Round(s.VictoryPoints),
                    ["baseHealth"] = s.Base != null && !s.Base.IsDead ? Round(s.Base.Health) : 0,
                    ["unitsBuilt"] = JObject.FromObject(stats.UnitsBuilt),
                    ["unitsLost"] = JObject.FromObject(stats.UnitsLost),
                    ["kills"] = JObject.FromObject(stats.Kills),
                    ["damageDealt"] = JObject.FromObject(stats.DamageDealt.ToDictionary(k => k.Key, k => Round(k.Value))),
                    ["upgrades"] = new JArray(stats.Upgrades),
                    ["pointsHeldSamples"] = new JArray(stats.PointsHeldSamples)
                };

                if (match.Mode == MatchMode.Skirmish) AddSurvivors(p, s.PlayerId);
                players.Add(p);
            }

            var root = new JObject
            {
                ["mode"] = match.Mode == MatchMode.Skirmish ? "skirmish" : "full",
                ["seed"] = match.Seed,
                ["armyA"] = match.Options.SkirmishA,
                ["armyB"] = match.Options.SkirmishB,
                ["winnerTeam"] = match.WinningTeam,
                ["winnerFaction"] = match.States.FirstOrDefault(s => s.TeamId == match.WinningTeam)?.Faction.FactionName,
                ["winCondition"] = match.WinCondition,
                ["duration"] = Round(match.MatchTime),
                ["pointSampleIntervalSeconds"] = 30,
                ["balanceFile"] = match.Options.BalancePath,
                ["players"] = players
            };
            WriteFile(path, root);
        }

        private static void AddSurvivors(JObject p, int playerId)
        {
            int count = 0;
            float health = 0f, maxHealth = 0f, cost = 0f;
            foreach (var u in Unit.AllUnits)
            {
                if (u.IsDead || u.OwnerPlayerId != playerId) continue;
                count++;
                health += u.Health;
                maxHealth += u.MaxHealth;
                cost += u.Definition.cost;
            }
            p["survivors"] = count;
            p["survivorHealth"] = Round(health);
            p["survivorHealthFraction"] = maxHealth > 0f ? Math.Round(health / maxHealth, 3) : 0;
            p["survivorCost"] = Round(cost);
        }

        public static void WriteUnitCatalogue(string path, IReadOnlyList<FactionDefinition> factions)
        {
            var units = new JArray();
            foreach (var f in factions)
            {
                foreach (var u in f.Units)
                {
                    units.Add(new JObject
                    {
                        ["key"] = MatchStats.UnitKey(u.displayName),
                        ["faction"] = f.FactionName,
                        ["cost"] = u.cost,
                        ["producible"] = u.producible,
                        ["maxHealth"] = u.maxHealth,
                        ["damage"] = u.damage,
                        ["range"] = u.range
                    });
                }
            }
            WriteFile(path, new JObject { ["factions"] = new JArray(factions.Select(f => f.FactionName)), ["units"] = units });
        }

        private static void WriteFile(string path, JObject json)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.Log(json.ToString(Formatting.None));
                return;
            }
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, json.ToString(Formatting.Indented));
                Debug.Log($"[DawnOfShapes] Results written to {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DawnOfShapes] Could not write results to {path}: {e.Message}");
            }
        }

        private static double Round(float v) => Math.Round(v, 1);
    }
}
