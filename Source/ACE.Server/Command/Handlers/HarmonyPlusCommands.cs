using System;
using System.Linq;
using System.Collections.Generic;

using ACE.Entity.Enum;
using ACE.Server.Network;
using ACE.Server.Managers;

namespace ACE.Server.Command.Handlers
{
    public static class HarmonyPlusCommands
    {
        [CommandHandler("harmony", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, -1,
            "Harmony+ management: preview/apply/revert/status")]
        public static void HandleHarmony(Session session, params string[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
            {
                Log("Usage: /harmony preview <targetLevel> | apply | revert | status", session);
                return;
            }

            var verb = parameters[0].ToLowerInvariant();

            try
            {
                switch (verb)
                {
                    case "preview":
                        if (parameters.Length < 2 || !int.TryParse(parameters[1], out var targetLevel))
                        {
                            Log("Usage: /harmony preview <targetLevel>", session);
                            return;
                        }
                        RunPreview(session, targetLevel);
                        return;

                    case "apply":
                        ContentUnlockManager.ApplyUnlocks();
                        Log("Apply requested; ContentUnlockManager.ApplyUnlocks() invoked.", session);
                        return;

                    case "revert":
                        ContentUnlockManager.RevertToSnapshot();
                        Log("Reverted XP table to snapshot.", session);
                        return;

                    case "status":
                        var xp = DatManager.PortalDat.XpTable.CharacterLevelXPList;
                        Log($"Current XP table max level: {xp.Count - 1}", session);
                        return;

                    default:
                        Log("Unknown subcommand. Usage: /harmony preview <targetLevel> | apply | revert | status", session);
                        return;
                }
            }
            catch (Exception ex)
            {
                Log($"Harmony command failed: {ex.Message}", session);
            }
        }

        private static void RunPreview(Session session, int targetLevel)
        {
            var xpList = DatManager.PortalDat.XpTable.CharacterLevelXPList;
            var currentMax = xpList.Count - 1;

            if (targetLevel <= currentMax)
            {
                Log($"Target {targetLevel} <= current max {currentMax}; nothing to preview.", session);
                return;
            }

            // copy current values
            var copy = xpList.ToList();

            // compute deltas
            var deltas = new List<long>();
            for (int i = 1; i < copy.Count; i++)
                deltas.Add((long)copy[i] - (long)copy[i - 1]);

            int ratioStart = 126;
            int ratioEnd = Math.Min(currentMax, 275);
            if (ratioEnd - ratioStart < 6)
            {
                ratioStart = Math.Max(2, copy.Count / 3);
                ratioEnd = Math.Max(ratioStart + 6, copy.Count - 1);
                ratioEnd = Math.Min(ratioEnd, deltas.Count - 1);
            }

            var ratios = new List<double>();
            for (int lvl = ratioStart; lvl < ratioEnd; lvl++)
            {
                var idx = lvl - 1;
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
                var mid = ratios.Count / 2;
                growthFactor = (ratios.Count % 2 == 1) ? ratios[mid] : (ratios[mid - 1] + ratios[mid]) / 2.0;
            }
            if (growthFactor <= 0 || double.IsNaN(growthFactor) || double.IsInfinity(growthFactor))
                growthFactor = 1.1;

            long prevTotal = (long)copy.Last();
            long prevDelta = deltas.Count > 0 ? deltas.Last() : Math.Max(1, prevTotal / 10);

            for (int lvl = currentMax + 1; lvl <= targetLevel; lvl++)
            {
                long nextDelta = Math.Max(1, (long)Math.Round(prevDelta * growthFactor));
                long nextTotal = prevTotal + nextDelta;
                if (nextTotal <= prevTotal) nextTotal = prevTotal + 1;

                copy.Add((ulong)nextTotal);
                prevDelta = nextDelta;
                prevTotal = nextTotal;
            }

            var added = copy.Count - xpList.Count;
            var sample = string.Join(", ", copy.Skip(Math.Max(0, copy.Count - 6)).Select(x => x.ToString()));

            Log($"Preview: currentMax={currentMax}, previewMax={copy.Count - 1}, levelsAdded={added}", session);
            Log($"Last totals sample: {sample}", session);
        }

        private static void Log(string message, Session session)
        {
            if (session?.Player is not null)
                session.Player.SendMessage(message);
            Console.WriteLine(message);
        }
    }
}
/*
 * Custom modifications (Drunkenfell) - GitHub & Crypt
 * Marker: Drunkenfell-Custom
 * Purpose: admin commands for Harmony+ content unlocks
 */

using System;
using log4net;
using ACE.Server.Managers;
using ACE.DatLoader;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using System.Linq;
using ACE.Server.Network;
using ACE.Server.Command.Handlers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Entity.Enum.Properties;

namespace ACE.Server.Command.Handlers
{
    public static class HarmonyPlusCommands
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(HarmonyPlusCommands));
        [CommandHandler("harmony", AccessLevel.Developer, CommandHandlerFlag.None, includeRaw: false, parameterCount: -1, description: "Manage Harmony+ content unlocks (scaffold)")]
        public static void HandleHarmonyCommand(Session session, params string[] parameters)
        {
            if (parameters.Length < 1)
            {
                session?.Player?.SendMessage("Usage: /harmony <apply|revert|status>");
                return;
            }

            var verb = parameters[0].ToLowerInvariant();
            switch (verb)
            {
                case "apply":
                    ContentUnlockManager.ApplyUnlocks();
                    session?.Player?.SendMessage("Harmony+: ApplyUnlocks called.");
                    break;
                case "revert":
                    ContentUnlockManager.RevertUnlocks();
                    session?.Player?.SendMessage("Harmony+: RevertUnlocks called.");
                    break;
                case "list":
                    try
                    {
                        using var ctx = new WorldDbContext();
                        var rows = ctx.ContentUnlock.OrderBy(u => u.Id).ToList();
                        session?.Player?.SendMessage($"Harmony+: {rows.Count} unlock(s) in DB:");
                        foreach (var r in rows)
                        {
                            session?.Player?.SendMessage($"id={r.Id} enabled={r.Enabled} type={r.UnlockType} name={r.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        session?.Player?.SendMessage("Harmony+: Failed to list content_unlocks: " + ex.Message);
                    }
                    break;
                case "enable":
                case "disable":
                    if (parameters.Length < 3 || !int.TryParse(parameters[1], out var id))
                    {
                        session?.Player?.SendMessage("Usage: /harmony enable|disable <id> confirm");
                        break;
                    }
                    // Safety: require explicit 'confirm' token to perform DB writes
                    if (!string.Equals(parameters[2], "confirm", StringComparison.OrdinalIgnoreCase))
                    {
                        session?.Player?.SendMessage("This command modifies the world DB. Append 'confirm' to proceed, e.g. /harmony enable 42 confirm");
                        break;
                    }
                    try
                    {
                        using var ctx = new WorldDbContext();
                        var row = ctx.ContentUnlock.Find(id);
                        if (row == null)
                        {
                            session?.Player?.SendMessage($"Harmony+: Row id={id} not found");
                            break;
                        }
                        row.Enabled = verb == "enable";
                        ctx.SaveChanges();
                        // Persist change then refresh in-memory unlocks
                        session?.Player?.SendMessage($"Harmony+: Set id={id} enabled={row.Enabled}");
                        try
                        {
                            ContentUnlockManager.RevertUnlocks();
                            ContentUnlockManager.ApplyUnlocks();
                            session?.Player?.SendMessage("Harmony+: Applied unlock changes to running server.");
                        }
                        catch (Exception ex)
                        {
                            session?.Player?.SendMessage("Harmony+: Failed to apply unlock changes: " + ex.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        session?.Player?.SendMessage("Harmony+: Failed to update row: " + ex.Message);
                    }
                    break;
                case "status":
                    var max = ContentUnlockManager.GetEffectiveMaxLevel();
                    session?.Player?.SendMessage($"Harmony+: Effective Max Level = {max}");
                    break;
                case "xpinfo":
                    try
                    {
                        var xpTable = DatManager.PortalDat.XpTable;
                        var count = xpTable.CharacterLevelXPList.Count;
                        var maxLevel = count - 1;
                        var maxTotal = xpTable.CharacterLevelXPList.Last();
                        session?.Player?.SendMessage($"Harmony+: XP table entries={count} (max level={maxLevel}), max total XP={maxTotal:N0}");

                        if (parameters.Length >= 2)
                        {
                            var arg = parameters[1].ToLowerInvariant();
                            if (arg == "next" && session?.Player != null)
                            {
                                var nextIndex = (session.Player.Level ?? 0) + 1;
                                if (nextIndex <= maxLevel)
                                {
                                    var threshold = xpTable.CharacterLevelXPList[nextIndex];
                                    session.Player.SendMessage($"Harmony+: Next level ({nextIndex}) threshold = {threshold:N0}");
                                }
                                else
                                    session.Player.SendMessage($"Harmony+: You are at or above max level ({maxLevel}).");
                            }
                            else if (int.TryParse(parameters[1], out var levelQuery))
                            {
                                if (levelQuery < 0 || levelQuery > maxLevel)
                                    session.Player.SendMessage($"Harmony+: Level {levelQuery} out of range (0-{maxLevel}).");
                                else
                                {
                                    var threshold = xpTable.CharacterLevelXPList[levelQuery];
                                    session.Player.SendMessage($"Harmony+: Level {levelQuery} total XP = {threshold:N0}");
                                }
                            }
                            else
                            {
                                session.Player.SendMessage("Harmony+: Unknown xpinfo argument. Use a level number or 'next'.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        session?.Player?.SendMessage("Harmony+: Failed to read XP table: " + ex.Message);
                    }
                    break;
                case "normalize":
                    // normalize player experience to match current XP table
                    if (parameters.Length < 2)
                    {
                        session?.Player?.NormalizeExperience();
                        session?.Player?.SendMessage("Harmony+: Normalized your experience.");
                        log.Info($"Harmony+: {session?.Player?.Name} invoked /harmony normalize on self");
                        PlayerManager.BroadcastToAuditChannel(session.Player, $"Harmony+: Normalized player {session.Player.Name}.");
                    }
                    else
                    {
                        var target = parameters[1].ToLowerInvariant();
                        if (target == "all")
                        {
                            var all = PlayerManager.GetAllOnline();
                            foreach (var pl in all)
                                pl.NormalizeExperience();
                            session?.Player?.SendMessage($"Harmony+: Normalized {all.Count} online player(s).");
                            log.Info($"Harmony+: {session?.Player?.Name} invoked /harmony normalize all ({all.Count} players)");
                            PlayerManager.BroadcastToAuditChannel(session.Player, $"Harmony+: Normalized {all.Count} online player(s). (invoked by {session.Player.Name})");
                        }
                        else
                        {
                            var pl = PlayerManager.GetOnlinePlayer(parameters[1]);
                            if (pl == null)
                                session?.Player?.SendMessage($"Harmony+: Player '{parameters[1]}' not found online.");
                            else
                            {
                                pl.NormalizeExperience();
                                session?.Player?.SendMessage($"Harmony+: Normalized player {pl.Name}.");
                                log.Info($"Harmony+: {session?.Player?.Name} invoked /harmony normalize on {pl.Name}");
                                PlayerManager.BroadcastToAuditChannel(session.Player, $"Harmony+: Normalized player {pl.Name} (invoked by {session.Player.Name}).");
                            }
                        }
                    }
                    break;
                default:
                    session?.Player?.SendMessage("Unknown verb. Usage: /harmony <apply|revert|status>");
                    break;
            }
        }
    }
}
