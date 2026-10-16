using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneMoreBonk.Rebuild
{
    /// <summary>Factual reference metadata; the rule implementation is independent C#.</summary>
    public static class RiskRelicData
    {
        [Serializable] sealed class RuleData
        {
            public int ReferenceBuild;
            public double[] BreakCurve;
            public RiskRelicEntry[] Relics;
        }
        static readonly RiskRelicEntry[] all;
        static readonly Dictionary<string, RiskRelicEntry> byId;
        static readonly double[] breakCurve;
        static RiskRelicData()
        {
            TextAsset resource = Resources.Load<TextAsset>("Risk/rule-data");
            if (resource == null) throw new InvalidOperationException("Missing Risk/rule-data reference rules.");
            RuleData data = JsonUtility.FromJson<RuleData>(resource.text);
            if (data == null || data.Relics == null || data.Relics.Length != 98 || data.BreakCurve == null || data.BreakCurve.Length < 31)
                throw new InvalidOperationException("Risk reference catalogue is incomplete.");
            all = data.Relics;
            breakCurve = data.BreakCurve;
            byId = new Dictionary<string, RiskRelicEntry>(StringComparer.Ordinal);
            foreach (RiskRelicEntry entry in all)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Id) || byId.ContainsKey(entry.Id))
                    throw new InvalidOperationException("Invalid or duplicate relic id.");
                byId.Add(entry.Id, entry);
            }
        }
        public static IReadOnlyList<RiskRelicEntry> All => all;
        public static IReadOnlyList<RiskRelicEntry> Catalogue => all;
        public static IReadOnlyList<double> BreakCurve => breakCurve;
        public static RiskRelicEntry Find(string id) => id != null && byId.TryGetValue(id, out RiskRelicEntry entry) ? entry : null;
        public static string Name(string id) => Find(id)?.Name ?? id ?? "";
        public static string Description(string id) => Find(id)?.Description ?? "";
        public static List<RiskRelicEntry> AvailableCatalogue(int day, IEnumerable<string> unlocks, IEnumerable<string> owned = null)
        {
            var unlocked = new HashSet<string>(unlocks ?? Array.Empty<string>(), StringComparer.Ordinal);
            var holding = new HashSet<string>(owned ?? Array.Empty<string>(), StringComparer.Ordinal);
            var available = new List<RiskRelicEntry>();
            foreach (RiskRelicEntry entry in all)
                if (!entry.Disabled && entry.FirstDay <= day && !holding.Contains(entry.Id) &&
                    (string.IsNullOrEmpty(entry.Unlock) || unlocked.Contains(entry.Unlock))) available.Add(entry);
            return available;
        }
    }
}
