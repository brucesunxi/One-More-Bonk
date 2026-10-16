using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OneMoreBonk.Rebuild;

// Test-only pure-core regression. No Session, persistence, recovery, UI, SDK, or Career is compiled.
public static class ReplayChecks
{
    const string EngineSha = "2b919717745ff673bfe838356563fedde74fd74a9a1c9eecd85a0af19db7cbe9";
    static readonly Dictionary<string, string> FixtureHashes = new Dictionary<string, string> {
        {"effect-branches.json", "268df0d78a144fd9df1cdde724b7db5bb7ef9e3b986c1a69fc300a60b059295c"},
        {"effect-dynamic-shelf.json", "e628cc0114459bf20bcee54797a3e660283d0b7e2f2484a5e393c90ea5a811b4"},
        {"effect-events.json", "a880d4c762e4d2694c759f3652b802f4ce4b1c6cf2ad440b946d38185bb7f9e6"},
        {"effect-event-seeds.json", "2d621d7ca7a2f95de55661aad8ed1a5d65c8dfc867051f7bc50b47cdb8418c42"}
    };
    static readonly string[] SourceEffectMap = {
        "successStreak:SuccessStreak","holdStreak:HoldStreak","successes:Successes","brace:BraceStacks",
        "phoenix:PhoenixUses","feverActivations:FrenzyActivations","axleSuccesses:AxleSuccesses",
        "temperScars:TemperScars","cornerstoneUses:CornerstoneUses","pearlUses:PearlUses",
        "heldGreatLevels:HeldGreatLevels","kingTickets:KingGreatTickets","inheritedSuccess:InheritedSuccessTickets",
        "royalLevels:RoyalGreatLevels","chimeLevels:ChimeLevels","bronzeTickets:BronzeSuccessTickets",
        "hasPreviousOutcome:HasPreviousOutcome","threadReady:ThreadReady","starReady:StarReady",
        "diceReady:DiceReady","spineShield:SpineShield","wardArmed:WardArmed","chalkArmed:ChalkArmed",
        "powderArmed:PowderArmed","primed:Primed","seedBase:SeedBase","growthBase:GrowthBase",
        "flat:Flat","multiplier:Multiplier","greatMultiplier:GreatMultiplier","valueScale:ValueScale",
        "normalHits:NormalHits","normalSuccesses:NormalSuccesses","damageCount:DamageCount",
        "spineLosses:SpineDamages","holdsTotal:HoldsTotal","milestones:MilestoneMask"
    };
    static readonly Dictionary<string, string> SourceContextMap = Map(
        "grudgeTickets:GrudgeTickets,mourningGreatTickets:MourningGreatTickets,successfulTributes:SuccessfulTributes," +
        "inheritGrade:InheritedGrade,inheritanceChargeGrant:InheritanceChargeGrant,inheritSuccess:InheritedSuccessTickets," +
        "insured:InsuranceUsed,brokenSwords:BrokenSwords,funeralClaims:FuneralClaims,funeralPending:NextSupply," +
        "wallet:BonusGold,greatEvents:GreatEvents,damageEvents:DamageEvents,holdEvents:HoldEvents,feverEvents:FrenzyEvents");
    static readonly Dictionary<string, string> ExpectedContextMap = Map(
        "ContextGrudge:GrudgeTickets,ContextMourning:MourningGreatTickets,ContextInheritedGrade:InheritedGrade," +
        "ContextInheritanceCharge:InheritanceChargeGrant,ContextInheritedSuccess:InheritedSuccessTickets," +
        "ContextInsurance:InsuranceUsed,ContextBroken:BrokenSwords,ContextFuneralClaims:FuneralClaims," +
        "ContextNextSupply:NextSupply,ContextGold:BonusGold,ContextScrap:ScrapTribute," +
        "ContextGreatEvents:GreatEvents,ContextDamageEvents:DamageEvents,ContextHoldEvents:HoldEvents,ContextFrenzyEvents:FrenzyEvents");
    static Dictionary<string, string> Map(string text) => text.Split(',').Select(x => x.Split(':')).ToDictionary(x => x[0], x => x[1]);
    static int assertions, failures, branchCases, branchSteps, dynamicCases, eventCases;
    static readonly List<string> failureSamples = new List<string>();
    static readonly Dictionary<string, int> sections = new Dictionary<string, int>();
    static readonly HashSet<string> comparedBranchFields = new HashSet<string>();
    static readonly Dictionary<string, int> relicCases = new Dictionary<string, int>();
    static readonly Dictionary<string, int[]> relicOutcomeCounts = new Dictionary<string, int[]>();
    static readonly Dictionary<string, int> expectedProcCounts = new Dictionary<string, int>();
    static readonly int[] rawOutcomes = new int[5];
    static JArray seedMappings;
    static JArray guardBoundaries;

    static void Check(bool condition, string label)
    {
        assertions++;
        if (!condition) { failures++; if (failureSamples.Count < 80) failureSamples.Add(label); }
    }
    static void Equal(object actual, JToken expected, string label)
    {
        if (actual is double value)
        {
            // Oracle JSON can spell >Int64 values as integer literals; Newtonsoft stores BigInteger.
            double target = expected.ToObject<double>();
            Check(RiskForgeRules.Finite(value) && Math.Abs(value - target) <= Math.Max(1e-7, Math.Abs(target) * 3e-13),
                label + " expected=" + target.ToString("R") + " actual=" + value.ToString("R"));
        }
        else if (actual is ForgeOutcome outcome) Check((int)outcome == expected.Value<int>(), label + " enum");
        else Check(JToken.DeepEquals(JToken.FromObject(actual), expected), label + " expected=" + expected + " actual=" + actual);
    }
    static void Tickets(int[] actual, JToken expected, string label, bool total1000 = true)
    {
        int[] desired = expected.ToObject<int[]>();
        Check(actual != null && desired != null && actual.Length == 5 && desired.Length == 5 &&
            actual.SequenceEqual(desired) && actual.All(x => x >= 0) && (!total1000 || actual.Sum() == 1000), label);
    }
    static void Stage(string name, Action action)
    {
        int before = assertions; action(); sections[name] = assertions - before;
    }
    static string Hash(string path)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }
    static JObject Load(string directory, string name)
    {
        string path = Path.Combine(directory, name);
        if (!File.Exists(path) || Hash(path) != FixtureHashes[name]) throw new InvalidDataException("Fixture fingerprint mismatch: " + name);
        return JObject.Parse(File.ReadAllText(path));
    }
    static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public)
        ?? throw new InvalidDataException("Unmapped public field: " + name);
    static void Assign(object target, string field, JToken value) => Field(target, field).SetValue(target, value.ToObject(Field(target, field).FieldType));
    static RiskForgeContext Context(IEnumerable<string> ids) => new RiskForgeContext { Relics = ids.ToList() };

    static void CheckBranches(JObject fixture)
    {
        Check((string)fixture["SourceSha256"] == EngineSha && (int)fixture["SourceReportedBuild"] == 25755699, "Archived engine identity");
        var cases = (JArray)fixture["BranchOracle"];
        Check(cases.Count == 949, "All 949 independent branch cases retained");
        foreach (JObject vector in cases)
        {
            string label = (string)vector["Label"];
            string[] ids = vector["Relics"].ToObject<string[]>();
            var context = Context(ids);
            var sword = RiskForgeRules.NewSword(1, 777u, "ordinary", (int)vector["Grade"]);
            RiskForgeRules.Prime(sword, context);
            if ((bool)vector["Frenzy"]) { sword.FrenzyActive = true; sword.FrenzyRemaining = 4; }
            uint rng = (uint)vector["Seed"];
            branchCases++;
            foreach (string id in ids)
            {
                relicCases[id] = relicCases.TryGetValue(id, out int count) ? count + 1 : 1;
                if (!relicOutcomeCounts.ContainsKey(id)) relicOutcomeCounts[id] = new int[5];
            }
            int index = 0;
            foreach (JObject step in vector["Steps"])
            {
                int outcome = (int)step["Outcome"];
                rawOutcomes[outcome]++;
                foreach (string id in ids) relicOutcomeCounts[id][outcome]++;
                var result = RiskForgeRules.StrikeForcedForChecks(sword, context, (ForgeOutcome)outcome, ref rng);
                string stepLabel = label + " step " + index++;
                Check(result.Accepted && (int)result.RolledOutcome == outcome, stepLabel + " accepted original roll");
                if (!result.Accepted) break;
                branchSteps++;
                ComparePostState(sword, context, result, rng, (JObject)step["Expected"], stepLabel);
                foreach (string proc in step["Expected"]["ProcIds"].ToObject<string[]>())
                    expectedProcCounts[proc] = expectedProcCounts.TryGetValue(proc, out int n) ? n + 1 : 1;
            }
        }
        Check(branchSteps == 7994, "All 7994 source post-states replayed");
        Check(RiskRelicData.All.Where(x => !x.Disabled).Select(x => x.Id).All(relicCases.ContainsKey), "All 94 enabled relics present in independent branches");
    }

    static void ComparePostState(RiskSword sword, RiskForgeContext context, RiskStrikeResult result, uint rng, JObject expected, string label)
    {
        foreach (var property in expected)
        {
            string key = property.Key; JToken value = property.Value; comparedBranchFields.Add(key);
            if (key == "Value") { Equal(RiskForgeRules.Value(sword, context), value, label + " Value"); continue; }
            if (key == "Rng") { Equal(rng, value, label + " RNG"); continue; }
            if (key == "Duplicated") { Equal(result.Duplicated, value, label + " duplicate flag"); continue; }
            if (key == "NextTickets") { Tickets(RiskForgeRules.Tickets(sword, context), value, label + " next odds"); continue; }
            if (key == "OssuaryTickets") { Tickets(context.OssuaryTickets, value, label + " ossuary", false); continue; }
            if (key == "ProcIds")
            {
                string[] actual = result.Procs.Where(p => !string.IsNullOrEmpty(p.Id)).Select(p => p.Id).ToArray();
                Check(actual.SequenceEqual(value.ToObject<string[]>()), label + " ordered proc ids"); continue;
            }
            if (ExpectedContextMap.TryGetValue(key, out string contextField)) { Equal(Field(context, contextField).GetValue(context), value, label + " " + key); continue; }
            FieldInfo field = typeof(RiskSword).GetField(key);
            if (field != null) { Equal(field.GetValue(sword), value, label + " " + key); continue; }
            field = typeof(RiskSwordEffects).GetField(key);
            if (field != null) { Equal(field.GetValue(sword.Effects), value, label + " " + key); continue; }
            throw new InvalidDataException("Uncompared Expected field: " + key);
        }
    }

    static RiskSword SourceSword(JObject source)
    {
        string line = (bool)source["worldTree"] ? "worldtree" : (bool)source["seafood"] ? "seafood" : (bool)source["funerary"] ? "funerary" : "ordinary";
        var sword = RiskForgeRules.NewSword(1, 777, line, (int)source["grade"]);
        var effects = (JObject)source["effects"];
        sword.Hits = (int)source["strikes"]; sword.Charge = (int)effects["charge"];
        sword.Broken = (bool)source["broken"]; sword.ScrapValue = sword.Broken ? (double)source["value"] : 0;
        sword.PeakValue = (double)effects["peak"];
        sword.FrenzyRemaining = (double)effects["feverUntil"]; sword.FrenzyActive = sword.FrenzyRemaining > 0;
        foreach (string map in SourceEffectMap) { string[] pair = map.Split(':'); Assign(sword.Effects, pair[1], effects[pair[0]]); }
        sword.Effects.PreviousOutcome = (ForgeOutcome)(int)effects["previousOutcome"];
        sword.Effects.SameResultStreak = (int)source["resultStreak"];
        return sword;
    }
    static RiskForgeContext SourceContext(JObject source)
    {
        var context = Context(source["relics"].ToObject<string[]>());
        context.OssuaryTickets = source["ossuaryTickets"].ToObject<int[]>();
        foreach (var pair in SourceContextMap) Assign(context, pair.Value, source[pair.Key]);
        return context;
    }
    static void CompareSourceState(RiskSword sword, RiskForgeContext context, JObject sourceSword, JObject sourceContext, string label, bool compareFrenzy = true)
    {
        Equal(sword.Grade, sourceSword["grade"], label + " grade"); Equal(sword.Hits, sourceSword["strikes"], label + " strikes");
        Equal(sword.Charge, sourceSword["effects"]["charge"], label + " charge"); Equal(sword.Broken, sourceSword["broken"], label + " broken");
        Equal(RiskForgeRules.Value(sword, context), sourceSword["value"], label + " value");
        Equal(sword.PeakValue, sourceSword["effects"]["peak"], label + " peak");
        Equal(sword.Effects.PreviousOutcome, sourceSword["effects"]["previousOutcome"], label + " effective previous outcome");
        Equal(sword.Effects.SameResultStreak, sourceSword["resultStreak"], label + " result streak");
        foreach (string map in SourceEffectMap)
        {
            string[] pair = map.Split(':'); Equal(Field(sword.Effects, pair[1]).GetValue(sword.Effects), sourceSword["effects"][pair[0]], label + " " + pair[1]);
        }
        foreach (var pair in SourceContextMap) Equal(Field(context, pair.Value).GetValue(context), sourceContext[pair.Key], label + " context " + pair.Value);
        Tickets(context.OssuaryTickets, sourceContext["ossuaryTickets"], label + " ossuary", false);
        if (compareFrenzy) Equal(sword.FrenzyActive, JToken.FromObject((double)sourceSword["effects"]["feverUntil"] > 0), label + " frenzy active");
    }
    static void CompareDynamic(RiskSword sword, RiskForgeContext context, JObject state, string label)
    {
        CompareSourceState(sword, context, (JObject)state["sword"], (JObject)state["context"], label);
        Tickets(RiskForgeRules.Tickets(sword, context, false), state["tickets"], label + " raw next odds");
        string before = UnityEngine.JsonUtility.ToJson(sword) + UnityEngine.JsonUtility.ToJson(context);
        RiskForgeRules.Tickets(sword, context, true); RiskForgeRules.Value(sword, context);
        Check(before == UnityEngine.JsonUtility.ToJson(sword) + UnityEngine.JsonUtility.ToJson(context), label + " read-only preview");
    }
    static void CheckDynamicShelf(JObject fixture)
    {
        Check((string)fixture["sha256"] == EngineSha && (int)fixture["count"] == 15, "Dynamic source identity/count");
        foreach (JObject vector in fixture["cases"])
        {
            string id = (string)vector["id"]; var sword = SourceSword((JObject)vector["before"]["sword"]);
            var context = SourceContext((JObject)vector["before"]["context"]); dynamicCases++;
            if (vector["remove"] is JArray remove && remove.Count > 0)
            {
                context.Relics = vector["removed"]["context"]["relics"].ToObject<List<string>>();
                RiskForgeRules.RefreshForShelfChanges(sword, context); CompareDynamic(sword, context, (JObject)vector["removed"], id + " remove");
                context.Relics = vector["reequipped"]["context"]["relics"].ToObject<List<string>>();
                RiskForgeRules.RefreshForShelfChanges(sword, context); CompareDynamic(sword, context, (JObject)vector["reequipped"], id + " re-equip");
            }
            else
            {
                context.Relics.Clear(); RiskForgeRules.RefreshForShelfChanges(sword, context);
                context.Relics = vector["beforeStrike"]["context"]["relics"].ToObject<List<string>>();
                RiskForgeRules.RefreshForShelfChanges(sword, context); CompareDynamic(sword, context, (JObject)vector["beforeStrike"], id + " before strike");
                uint rng = 777; var result = RiskForgeRules.StrikeForcedForChecks(sword, context, (ForgeOutcome)(int)vector["outcome"], ref rng);
                Check(result.Accepted, id + " accepted"); CompareDynamic(sword, context, (JObject)vector["afterStrike"], id + " after strike");
            }
        }
    }

    static uint IndependentNext(uint x) { unchecked { x ^= x << 13; x ^= x >> 17; x ^= x << 5; return x == 0 ? 0xD1B54A35u : x; } }
    static void CheckEvents(JObject fixture, JObject mapping)
    {
        Check((string)fixture["source_sha256"] == EngineSha && fixture["cases"].Count() == 13, "Thirteen source event snapshots");
        seedMappings = (JArray)mapping["cases"];
        foreach (JObject vector in fixture["cases"])
        {
            string id = (string)vector["id"]; var map = seedMappings.Cast<JObject>().Single(x => (string)x["id"] == id);
            var sword = SourceSword((JObject)vector["before"]["sw"]); var context = SourceContext((JObject)vector["before"]["run"]);
            bool injectedFrenzy = (bool)vector["input"]["frenzy"];
            if (injectedFrenzy) { sword.FrenzyActive = true; sword.FrenzyRemaining = 4; }
            uint rng = (uint)map["seed"], independent = IndependentNext(rng);
            int drawCount = 0;
            foreach (JObject decision in map["decision_tests"])
            {
                independent = IndependentNext(independent); drawCount++;
                Check((independent / 4294967296.0 < (double)decision["threshold"]) == (bool)decision["less_than"], id + " mapped random branch " + drawCount);
            }
            Check(drawCount == (int)vector["used_dice"] && independent == (uint)map["expected_rng"], id + " independent seed draw count");
            var result = RiskForgeRules.StrikeForcedForChecks(sword, context, (ForgeOutcome)(int)vector["raw_outcome"], ref rng);
            Check(result.Accepted && rng == independent, id + " accepted and consumes one outcome plus source-used proc draws");
            CompareSourceState(sword, context, (JObject)vector["after"]["sw"], (JObject)vector["after"]["run"], id, !injectedFrenzy);
            string[] expectedProcs = vector["after"]["sw"]["effects"]["procs"].Select(x => (string)x["item"]).Where(x => !string.IsNullOrEmpty(x)).ToArray();
            Check(result.Procs.Where(x => !string.IsNullOrEmpty(x.Id)).Select(x => x.Id).SequenceEqual(expectedProcs), id + " source proc order");
            Equal(result.Duplicated, vector["after"]["sw"]["effects"]["duplicateRequested"], id + " duplicate flag");
            int effective = id == "cornerstone-before-insurance-spine-phoenix" || id == "insurance-before-spine-phoenix" || id == "spine-before-phoenix" ? 1 : (int)vector["raw_outcome"];
            Check((int)result.Outcome == effective, id + " raw-to-effective outcome");
            if (!injectedFrenzy) Tickets(RiskForgeRules.Tickets(sword, context), vector["next_chances"], id + " next odds");
            eventCases++;
        }
    }

    static void CheckLifecycle()
    {
        // Explicit local invariants below supplement, and are distinguished from, source-oracle states.
        for (int outcome = 0; outcome < 5; outcome++)
        {
            var c = Context(new[] { "cornerstone", "insurance", "spine", "phoenix", "repair" });
            var s = RiskForgeRules.NewSword(1, 777, "ordinary", 10); s.Effects.Primed = true;
            s.Effects.WardArmed = true; s.Effects.SpineShield = true; uint rng = 777;
            var r = RiskForgeRules.StrikeForcedForChecks(s, c, (ForgeOutcome)outcome, ref rng);
            Check(r.Accepted && !s.Effects.WardArmed, "ward is consumed by raw outcome " + outcome);
            if (outcome == 0) Check(!r.Fatal && r.ProtectionId == "ward" && s.Grade == 10 && s.Effects.CornerstoneUses == 0 && !c.InsuranceUsed && s.Effects.SpineShield && s.Effects.PhoenixUses == 0 && s.Effects.TemperScars == 0, "ward precedes all persistent protections and Damage observers");
        }
        var context = Context(new[] { "idol" }); var sword = RiskForgeRules.NewSword(1, 777, "ordinary", 3); uint seed = 777;
        RiskForgeRules.StrikeForcedForChecks(sword, context, ForgeOutcome.Success, ref seed);
        Check(sword.Grade == 4 && sword.Effects.MilestoneMask == 1 && Math.Abs(sword.Effects.Multiplier - 1.15) < 1e-12, "first +4 milestone pays once");
        RiskForgeRules.StrikeForcedForChecks(sword, context, ForgeOutcome.Damage, ref seed);
        var recross = RiskForgeRules.StrikeForcedForChecks(sword, context, ForgeOutcome.Success, ref seed);
        Check(sword.Grade == 4 && sword.Effects.MilestoneMask == 1 && Math.Abs(sword.Effects.Multiplier - 1.15) < 1e-12 && recross.Procs.All(x => x.Id != "idol"), "damage and re-cross cannot repay milestone");

        context = Context(new[] { "holy", "dawnsheath", "tooth" }); context.InheritedGrade = 7; context.InheritanceChargeGrant = 7; context.InheritedSuccessTickets = 4;
        sword = RiskForgeRules.NewSword(1, 777); string before = UnityEngine.JsonUtility.ToJson(sword) + UnityEngine.JsonUtility.ToJson(context);
        RiskForgeRules.Tickets(sword, context); RiskForgeRules.Value(sword, context);
        Check(before == UnityEngine.JsonUtility.ToJson(sword) + UnityEngine.JsonUtility.ToJson(context), "inheritance odds/value preview does not consume reservation");
        RiskForgeRules.Prime(sword, context);
        Check(sword.Grade == 7 && sword.Charge == 7 && sword.Effects.InheritedSuccessTickets == 4 && context.InheritedGrade == 0 && context.InheritanceChargeGrant == 0 && context.InheritedSuccessTickets == 0, "one actual Prime claims all reserved inheritance");
        var second = RiskForgeRules.NewSword(2, 777); RiskForgeRules.Prime(second, context);
        Check(second.Grade == 0 && second.Charge == 0 && second.Effects.InheritedSuccessTickets == 0, "reserved inheritance cannot be claimed twice");

        context = Context(Array.Empty<string>()); sword = RiskForgeRules.NewSword(1, 777); seed = 777;
        bool ordinary = true;
        for (int i = 0; i < 10; i++) { var r = RiskForgeRules.StrikeForcedForChecks(sword, context, ForgeOutcome.Hold, ref seed); ordinary &= r.Accepted && !r.WasFrenzy; }
        Check(ordinary && sword.Charge == 10 && !sword.FrenzyActive, "ten normal hits only fill charge; do not start Frenzy early");
        Tickets(RiskForgeRules.Tickets(sword, context), new JArray(0, 95, 238, 572, 95), "full-charge preview is already safe");
        var trigger = RiskForgeRules.StrikeForcedForChecks(sword, context, ForgeOutcome.Hold, ref seed);
        Check(trigger.StartedFrenzy && trigger.WasFrenzy && sword.FrenzyRemaining == 4 && sword.Effects.FrenzyActivations == 1, "next legal hit starts four-second Frenzy");
        bool unlimited = true;
        for (int i = 0; i < 60; i++) unlimited &= RiskForgeRules.StrikeForcedForChecks(sword, context, ForgeOutcome.Hold, ref seed).Accepted;
        Check(unlimited && sword.FrenzyActive && sword.Hits == 71, "Frenzy has no arbitrary hit/energy cap");
        RiskForgeRules.Advance(sword, context, 3.999); Check(sword.FrenzyActive, "Frenzy remains before deadline");
        RiskForgeRules.Advance(sword, context, .0011); Check(!sword.FrenzyActive && sword.FrenzyRemaining == 0, "Frenzy expires by elapsed forge time");
        context = Context(new[] { "speed", "loop", "bellows", "redglass", "everwick", "headless" }); sword = RiskForgeRules.NewSword(2, 777, "ordinary", 10); sword.Charge = 10;
        Equal(RiskForgeRules.Interval(sword, context), JToken.FromObject(.64 / 1.45), "Frenzy speed bonuses add before interval division");
        RiskForgeRules.StrikeForcedForChecks(sword, context, ForgeOutcome.Hold, ref seed);
        Equal(sword.FrenzyRemaining, JToken.FromObject(7.0), "Frenzy duration four+one+two");
        RiskForgeRules.Advance(sword, context, 7); Check(!sword.FrenzyActive && sword.Grade == 11 && sword.Effects.FrenzyEndBonus == 1, "headless end bonus occurs once");
        RiskForgeRules.Advance(sword, context, 10); Check(sword.Grade == 11, "headless expiration cannot pay twice");
    }

    static void CheckGuardBoundaries(JObject mapping)
    {
        guardBoundaries = (JArray)mapping["single_guard_boundaries"];
        Check(guardBoundaries.Count == 10, "ten fixed near/equal strict-guard seeds");
        foreach (JObject vector in guardBoundaries)
        {
            string id = (string)vector["id"]; uint rng = (uint)vector["seed"];
            uint procDraw = IndependentNext(IndependentNext(rng));
            Check(procDraw == (uint)vector["proc_draw_uint"] && (procDraw / 4294967296.0 < (double)vector["threshold"]) == (bool)vector["trigger"], id + " independent seed lattice");
            var context = Context(new[] { (string)vector["relic"] });
            var sword = RiskForgeRules.NewSword(1, 777, "ordinary", 10);
            var result = RiskForgeRules.StrikeForcedForChecks(sword, context, (ForgeOutcome)(int)vector["raw_outcome"], ref rng);
            Check(result.Accepted && rng == procDraw && sword.Grade == (int)vector["expected_grade"] &&
                sword.Charge == (int)vector["expected_charge"] && result.Duplicated == (bool)vector["expected_duplicated"], id + " strict-threshold post-state");
        }
    }

    static void CheckShapeAndMoney()
    {
        int[] low = { 0,1,3,5,8,12,17,28,43,58,78,99 }, high = { 0,3,5,7,10,14,19,32,47,62,82,99 };
        bool bounds = true, peaks = true;
        for (uint seed = 1; seed <= 100; seed++)
        {
            var sword = RiskForgeRules.NewSword(1, seed);
            bounds &= sword.FormMilestones.Length == 12;
            for (int i = 0; i < 12; i++) bounds &= sword.FormMilestones[i] >= low[i] && sword.FormMilestones[i] <= high[i] && (i == 0 || sword.FormMilestones[i] > sword.FormMilestones[i - 1]);
            sword.Grade = 79; sword.PeakGrade = 80; int stage = RiskForgeRules.Stage(sword);
            peaks &= RiskForgeRules.MaterialAwakened(sword); sword.Grade = 0; peaks &= RiskForgeRules.Stage(sword) == stage;
        }
        Check(bounds, "100 cosmetic seeds remain in twelve ordered birth windows"); Check(peaks, "visual/material peak persists after grade loss");
        var context = Context(Array.Empty<string>()); var deep = RiskForgeRules.NewSword(1, 777, "worldtree", 99);
        Check(RiskForgeRules.Value(deep, context) > long.MaxValue && RiskForgeRules.Finite(RiskForgeRules.Value(deep, context)), "special +99 value safely exceeds Int64");
        Equal(RiskForgeRules.Money(double.PositiveInfinity), JToken.FromObject(1e100), "Money finite cap");
        Equal(RiskForgeRules.Money(1.5), JToken.FromObject(2.0), "Money positive half-up");
        uint rng = 777; var great = RiskForgeRules.NewSword(1, 777); var ordinary = RiskForgeRules.NewSword(2, 777);
        RiskForgeRules.StrikeForcedForChecks(great, context, ForgeOutcome.Great, ref rng);
        RiskForgeRules.StrikeForcedForChecks(ordinary, context, ForgeOutcome.Success, ref rng); RiskForgeRules.StrikeForcedForChecks(ordinary, context, ForgeOutcome.Success, ref rng);
        Check(great.Grade == 2 && RiskForgeRules.Value(great, context) == 800 && RiskForgeRules.Value(ordinary, context) == 720, "Great has persistent 10/9 value layer versus two Successes");
    }

    public static int Run(string fixtureDirectory = "Tests/Fixtures")
    {
        assertions = failures = branchCases = branchSteps = dynamicCases = eventCases = 0;
        failureSamples.Clear(); sections.Clear(); comparedBranchFields.Clear(); relicCases.Clear(); relicOutcomeCounts.Clear(); expectedProcCounts.Clear(); Array.Clear(rawOutcomes, 0, 5);
        JObject branches = Load(fixtureDirectory, "effect-branches.json"), dynamic = Load(fixtureDirectory, "effect-dynamic-shelf.json");
        JObject events = Load(fixtureDirectory, "effect-events.json"), mapping = Load(fixtureDirectory, "effect-event-seeds.json");
        Stage("independent_branch_post_states", () => CheckBranches(branches));
        Stage("independent_dynamic_shelf", () => CheckDynamicShelf(dynamic));
        Stage("independent_event_snapshots_with_seed_mapping", () => CheckEvents(events, mapping));
        Stage("local_exact_or_adjacent_seed_guard_boundaries", () => CheckGuardBoundaries(mapping));
        Stage("local_lifecycle_invariants", CheckLifecycle); Stage("local_shape_money_invariants", CheckShapeAndMoney);
        if (failures > 0) throw new InvalidOperationException("Effect regression mismatches: " + failures);
        return assertions;
    }

    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        if (args.Length != 3) { Console.Error.WriteLine("Usage: effects.exe <resources-root> <fixture-directory> <report-path>"); return 2; }
        string error = null; int exit = 0;
        try
        {
            UnityEngine.Resources.Root = args[0]; Run(args[1]);
        }
        catch (Exception exception) { error = exception.Message; Console.Error.WriteLine(exception); exit = failures > 0 ? 1 : 2; }
        var enabled = RiskRelicData.All.Where(x => !x.Disabled).Select(x => x.Id).ToArray();
        var report = new {
            schema_version = "1.0", date = DateTime.UtcNow.ToString("yyyy-MM-dd"), status = exit == 0 ? "PASS" : "FAIL", exit_code = exit,
            kind = "pure-core-ordered-effects-and-lifecycle", assertions, failed_assertions = failures, error, failure_samples = failureSamples, sections,
            independent_cases = new { branch_cases = branchCases, post_states = branchSteps, dynamic_shelf_cases = dynamicCases, event_snapshots = eventCases },
            source = new { url = "https://www.forgetax.tools/_next/static/chunks/0vzwcxk1vv8wc.js", sha256 = EngineSha, reported_build = 25755699,
                archived_oracle = "development/qa/unlimited-forge-v070/source-golden-oracle.json", archived_oracle_sha256 = "12fd7c14f701d1c9cbc0ebeca71b6cb2f9a10fb3aea28855d3aa2e17038ad641",
                legacy_check_source = "development/OneMoreBonk/Assets/Editor/RiskRulesChecks.cs", legacy_check_sha256 = "38628c6188f58c250bd9014a440deb0724f15c33bcde95db0f64fb34f7f3b0d8",
                expected_origin = "Archived public fan-site JS VM exports. All 949 BranchOracle Expected objects copied without recalculation; 15 dynamic and 13 event fixtures copied byte-for-byte. Not the original Steam executable." },
            fixture_sha256 = FixtureHashes, compared_branch_fields = comparedBranchFields.OrderBy(x => x).ToArray(), source_effect_field_mapping = SourceEffectMap,
            source_context_field_mapping = SourceContextMap, coverage = new {
                enabled_relics = enabled.Length, relics_present = relicCases.Count, missing_enabled_relics = enabled.Where(x => !relicCases.ContainsKey(x)).ToArray(),
                raw_outcome_order = new[] { "Break", "Damage", "Hold", "Success", "Great" }, raw_outcome_counts = rawOutcomes,
                per_relic_case_counts = relicCases, per_relic_raw_outcome_counts = relicOutcomeCounts, expected_ordered_proc_counts = expectedProcCounts,
                enabled_without_direct_proc_in_selected_paths = enabled.Where(x => !expectedProcCounts.ContainsKey(x)).ToArray(),
                trigger_limit = "Presence and direct ProcIds are reported separately. Passive weights/value modifiers and Session-only economic/progression effects can have no proc ID; this is not proof of every trigger/state combination." },
            event_seed_mapping = seedMappings,
            local_guard_boundary_vectors = guardBoundaries,
            coverage_interpretation = new[] {
                new { ids = new[] { "nail","file","seal","pin","rivet","tongs","thorn","obsidian","mallet","patience","spark","royalnail","oracledie","perfectcore","butterfingers","sicklehammer" }, category = "passive_weights_or_risk", evidence = "No dedicated ProcId is expected; fixed source NextTickets and all ordered post-states are compared." },
                new { ids = new[] { "coal","token","blackstone","hearth","dragonseal" }, category = "passive_value_or_threshold", evidence = "No dedicated ProcId is expected; fixed Value/PeakValue and source shelf refresh cases validate represented value layers. No all-round tribute history coverage is implied." },
                new { ids = new[] { "star","brace","holy","dice","hourglass","thread","locket","watch","thirdeye","tooth","dawnsheath" }, category = "state_mutations_without_dedicated_proc", evidence = "State banks/marks/charge and next tickets are compared, including actual-break event and shelf fixtures. Fresh inheritance claim also has separate local purity/one-time invariants." },
                new { ids = new[] { "firstseal" }, category = "missing_trigger_in_949_but_present_in_event_fixture", evidence = "All 949 sequences start Hold, so they do not exercise its first-hit Success/Great trigger. The independent great-frenzy-chain event has Hits=0 and Great, and compares that ordered chain." },
                new { ids = new[] { "speed","bellows","headless","loop","redglass","everwick" }, category = "timing_scope_beyond_post_roll_snapshots", evidence = "Raw branch snapshots alone do not prove timed start/end/interval behavior. Added local timing assertions check interval, duration, no hit cap and one-time headless expiry; these timing assertions are not Steam/JS end-to-end oracle captures." },
                new { ids = new[] { "supply","hook","testament","voucher","collector","badge","letter","mourning","silverpermit","royalchange","dragonpardon","ghostledger","lifecontract" }, category = "session_or_economy_trigger_not_executed_here", evidence = "Present in all-relic branch baselines, but supply, trades, shop rerolls, tribute, pardon and merchant transitions need the separate Session/economy suite. This pure-core runner makes no independent trigger-completeness claim for these effects." }
            },
            verification_artifacts = new[] { "Tests/Headless/ReplayChecks.cs", "Tools/verify-effects.sh", "Assets/OneMoreBonkRebuild/Gameplay/RiskForgeRules.cs", "Assets/OneMoreBonkRebuild/Gameplay/RiskForgeModels.cs", "Assets/Resources/Risk/rule-data.json" }
                .Where(File.Exists).ToDictionary(path => path, Hash),
            limits = new[] {
                "Forced outcomes still consume one C# outcome RNG draw; source BranchOracle generator intentionally did the same. Source fixed arbitrary proc dice in 13 event snapshots are mapped to equal strict-threshold decisions, not identical floats.",
                "0.7 and 0.02 cannot equal uint/2^32 draws; adjacent lower/upper draws are tested. 0.5 and 0.25 are representable and tested below/equal/above. One event mapping uses exact 0.25. No arbitrary multi-dice injection capability is claimed.",
                "great-frenzy-chain helper passed safe/frenzy into apply without persisting feverUntil. Its FrenzyActive/FrenzyRemaining and NextTickets are excluded, while ordered effects, value, counters, proc IDs and RNG draw count are checked.",
                "Source legacy fields absent from RiskSwordEffects are intentionally not mapped: old alternate banks/counters, fever UI timestamps/extensions and retired-item state. Full comparison field lists are included.",
                "Lifecycle/shape/money checks are explicit local invariants ported from old pure checks; they supplement rather than masquerade as independent oracle output.",
                "Only ForgeTypes, RiskForgeModels, RiskForgeRules, RiskRelicData and test-only Resources/JSON shim compile. No Session, original rewind/recovery, persistence, Career, UI, ads, device, native JsonUtility, Steam parity or whole-run balance is verified."
            }
        };
        string json = JsonConvert.SerializeObject(report, Formatting.Indented);
        if (args.Length == 3) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))); File.WriteAllText(args[2], json + "\n"); }
        Console.WriteLine(JsonConvert.SerializeObject(new { report.status, assertions, failures, branchCases, branchSteps, dynamicCases, eventCases, error }));
        return exit;
    }
}
