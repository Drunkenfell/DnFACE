using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using ACE.DatLoader.FileTypes;
using log4net;

namespace ACE.Server.Managers
{
    public static class ContentUnlockManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static List<ulong> originalXpTable = null;

        public static void Initialize()
        {
            try
            {
                log.Info("Initializing ContentUnlockManager...");

                // Snapshot original XP table
                var xpList = DatManager.PortalDat.XpTable.CharacterLevelXPList;
                originalXpTable = xpList.ToList();

                // Default clamp at 275 if dat contains higher levels
                var defaultClamp = 275;
                if (xpList.Count - 1 > defaultClamp)
                {
                    log.Info($"Clamping XP table to level {defaultClamp} by default (original max={xpList.Count - 1}).");
                    // truncate to defaultClamp inclusive (levels 0..defaultClamp)
                    var truncated = xpList.Take(defaultClamp + 1).ToList();
                    xpList.Clear();
                    xpList.AddRange(truncated);
                }
            }
            catch (Exception ex)
            {
                log.Error("ContentUnlockManager.Initialize failed", ex);
            }
        }

        public static void ApplyUnlocks()
        {
            try
            {
                log.Info("Applying ContentUnlockManager unlocks...");

                // Attempt to read content_unlocks table; if absent, no-op
                using (var ctx = new ACE.Database.Models.World.WorldDbContext())
                {
                    // Ensure the table exists by trying to query it; if it doesn't, this will throw and we'll catch
                    var rows = ctx.Set<ACE.Database.Models.World.ContentUnlock>().Where(r => r.Enabled).ToList();

                    if (rows.Count == 0)
                    {
                        log.Info("No enabled content_unlocks rows found.");
                        // still allow projection based on config
                    }

                    var xpList = DatManager.PortalDat.XpTable.CharacterLevelXPList;

                    foreach (var row in rows.OrderBy(r => r.Id))
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(row.Payload)) continue;

                            using var doc = JsonDocument.Parse(row.Payload);
                            if (!doc.RootElement.TryGetProperty("level_cap", out var levelCap))
                                continue;

                            var maxLevel = levelCap.GetProperty("maxLevel").GetInt32();

                            long costPerLevel = 0;
                            if (levelCap.TryGetProperty("costPerLevel", out var costPerLevelElem))
                                costPerLevel = costPerLevelElem.GetInt64();

                            List<long> levelCosts = null;
                            if (levelCap.TryGetProperty("levelCosts", out var levelCostsElem) && levelCostsElem.ValueKind == JsonValueKind.Array)
                            {
                                levelCosts = new List<long>();
                                foreach (var el in levelCostsElem.EnumerateArray())
                                {
                                    if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var v))
                                        levelCosts.Add(v);
                                }
                            }

                            var currentMax = xpList.Count - 1;
                            if (maxLevel <= currentMax)
                                continue; // nothing to do for this row

                            log.Info($"Applying unlock {row.Id} '{row.Name}': extending XP table from {currentMax} to {maxLevel}.");

                            // Append levels from currentMax+1 .. maxLevel
                            var prevTotal = (long)xpList.Last();
                            var levelsToAdd = maxLevel - currentMax;

                            for (int i = 0; i < levelsToAdd; i++)
                            {
                                long nextTotal = prevTotal;

                                // If explicit levelCosts provided, detect whether they are absolute totals or increments
                                if (levelCosts != null && levelCosts.Count > i)
                                {
                                    var candidate = levelCosts[i];
                                    if (candidate > prevTotal)
                                    {
                                        // absolute total
                                        nextTotal = candidate;
                                    }
                                    else
                                    {
                                        // treat as increment
                                        nextTotal = prevTotal + candidate;
                                    }
                                }
                                else if (costPerLevel > 0)
                                {
                                    nextTotal = prevTotal + costPerLevel;
                                }
                                else
                                {
                                    // fallback: increase by previous last value / 10 (heuristic)
                                    var delta = Math.Max(1, prevTotal / 10);
                                    nextTotal = prevTotal + delta;
                                }

                                // ensure monotonic
                                if (nextTotal <= prevTotal)
                                {
                                    nextTotal = prevTotal + 1;
                                }

                                xpList.Add((ulong)nextTotal);
                                prevTotal = nextTotal;
                            }

                            log.Info($"Extended XP table: new max level {xpList.Count - 1}.");
                        }
                        catch (Exception exRow)
                        {
                            log.Error($"Failed applying content_unlock row {row.Id}", exRow);
                        }
                    }
                }

                // After applying explicit DB rows, respect Server.MaxPlayerLevel projection
                try
                {
                    var xpList = DatManager.PortalDat.XpTable.CharacterLevelXPList;
                    var currentMax = xpList.Count - 1;
                    var desiredMax = ACE.Common.ConfigManager.Config?.Server?.MaxPlayerLevel ?? currentMax;

                    if (desiredMax > currentMax)
                    {
                        log.Info($"Projecting XP table from {currentMax} to desired max {desiredMax} per Server.MaxPlayerLevel.");

                        // Build deltas and compute growth ratios over a stable retail-ish window
                        var deltas = new List<long>();
                        for (int i = 1; i < xpList.Count; i++)
                        {
                            deltas.Add((long)xpList[i] - (long)xpList[i - 1]);
                        }

                        // Choose ratio range: prefer 126..min(275,currentMax), fall back to middle of table
                        int ratioStart = 126;
                        int ratioEnd = Math.Min(currentMax, 275);
                        if (ratioEnd - ratioStart < 6)
                        {
                            ratioStart = Math.Max(2, xpList.Count / 3);
                            ratioEnd = Math.Max(ratioStart + 6, xpList.Count - 1);
                            ratioEnd = Math.Min(ratioEnd, deltas.Count - 1);
                        }

                        var ratios = new List<double>();
                        for (int lvl = ratioStart; lvl < ratioEnd; lvl++)
                        {
                            var idx = lvl - 1; // deltas index
                            if (idx - 1 < 0 || idx + 1 >= deltas.Count) continue;
                            var prev = deltas[idx - 1];
                            var next = deltas[idx];
                            if (prev > 0)
                                ratios.Add((double)next / (double)prev);
                        }

                        double growthFactor = 1.0;
                        if (ratios.Count > 0)
                        {
                            ratios.Sort();
                            // median
                            var mid = ratios.Count / 2;
                            growthFactor = (ratios.Count % 2 == 1) ? ratios[mid] : (ratios[mid - 1] + ratios[mid]) / 2.0;
                        }

                        if (growthFactor <= 0 || double.IsNaN(growthFactor) || double.IsInfinity(growthFactor))
                            growthFactor = 1.1; // fallback small growth

                        long prevTotal = (long)xpList.Last();
                        long prevDelta = deltas.Count > 0 ? deltas.Last() : Math.Max(1, prevTotal / 10);

                        for (int level = currentMax + 1; level <= desiredMax; level++)
                        {
                            long nextDelta = Math.Max(1, (long)Math.Round(prevDelta * growthFactor));
                            long nextTotal = prevTotal + nextDelta;

                            // ensure monotonicity and reasonable increase
                            if (nextTotal <= prevTotal)
                                nextTotal = prevTotal + 1;

                            xpList.Add((ulong)nextTotal);
                            prevDelta = nextDelta;
                            prevTotal = nextTotal;
                        }

                        log.Info($"Projection complete: new max level {xpList.Count - 1}.");
                    }
                    else
                    {
                        log.Info($"No projection required; current max {currentMax}, desired {desiredMax}.");
                    }
                }
                catch (Exception exProj)
                {
                    log.Error("ContentUnlockManager projection failed", exProj);
                }
            }
            catch (Exception ex)
            {
                log.Error("ContentUnlockManager.ApplyUnlocks failed", ex);
            }
        }

        public static void RevertToSnapshot()
        {
            try
            {
                if (originalXpTable == null) return;

                var xpList = DatManager.PortalDat.XpTable.CharacterLevelXPList;
                xpList.Clear();
                xpList.AddRange(originalXpTable);

                log.Info($"Reverted XP table to snapshot, max level {xpList.Count - 1}.");
            }
            catch (Exception ex)
            {
                log.Error("ContentUnlockManager.RevertToSnapshot failed", ex);
            }
        }
    }
}
