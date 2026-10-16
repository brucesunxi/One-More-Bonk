using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OneMoreBonk.Rebuild;

// Test-only entry point. Build explicitly with UnityDataShim; never place either in Assets.
public static class GoldenRunner
{
    const int ExpectedCases = 133;
    static readonly string[] OutcomeOrder = { "Break", "Damage", "Hold", "Success", "Great" };
    static readonly string[] StarterIds = { "nail", "seal", "coin", "hold", "shard", "repair", "rage", "spark" };

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    static void SetPublicFields(object target, JObject values, string caseId, string section)
    {
        if (values == null) return;
        foreach (var pair in values)
        {
            FieldInfo field = target.GetType().GetField(pair.Key, BindingFlags.Instance | BindingFlags.Public);
            Require(field != null, caseId + ": unknown " + section + " field " + pair.Key);
            field.SetValue(target, pair.Value.ToObject(field.FieldType));
        }
    }

    static int[] ReadTickets(JToken token, string label)
    {
        var array = token as JArray;
        Require(array != null && array.Count == 5, label + ": expected five integer tickets");
        Require(array.All(x => x.Type == JTokenType.Integer), label + ": non-integer ticket");
        int[] tickets = array.ToObject<int[]>();
        Require(tickets.All(x => x >= 0) && tickets.Sum() == 1000, label + ": tickets must be nonnegative and total 1000");
        return tickets;
    }

    static void CheckCatalogue()
    {
        var all = RiskRelicData.All;
        Require(all.Count == 98, "Catalogue must contain exactly 98 entries");
        Require(all.Count(x => !x.Disabled) == 94, "Catalogue must contain exactly 94 enabled entries");
        Require(all.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == all.Count, "Duplicate catalogue id");
        Require(RiskRelicData.BreakCurve.Count >= 31, "Break curve is incomplete");
        Require(RiskRelicData.BreakCurve.All(x => !double.IsNaN(x) && !double.IsInfinity(x) && x >= 0 && x < 1), "Invalid break curve probability");
        Require(all.Where(x => x.Starter).Select(x => x.Id).OrderBy(x => x, StringComparer.Ordinal)
            .SequenceEqual(StarterIds.OrderBy(x => x, StringComparer.Ordinal)), "Unexpected eight-entry starter pool");
        Require(!RiskRelicData.Find("loop").Starter && RiskRelicData.Find("shard").Starter, "Official starter-pool correction is missing");
    }

    // Small regression checks for the protection/effective-outcome ordering, not an exhaustive effects suite.
    static int CheckOrderedEffectSmoke()
    {
        string[][] shelves = { new string[0], new[] { "cornerstone", "repair" },
            new[] { "insurance", "repair" }, new[] { "spine", "repair" }, new[] { "phoenix", "repair" },
            new[] { "cornerstone", "insurance", "spine", "phoenix", "repair" } };
        string[] protection = { null, "cornerstone", "insurance", "spine", "phoenix", "cornerstone" };
        for (int index = 0; index < shelves.Length; index++)
        {
            var context = new RiskForgeContext { Relics = shelves[index].ToList() };
            var sword = RiskForgeRules.NewSword(1, 1, "ordinary", 10);
            sword.Hits = 1; sword.Effects.Primed = true; sword.Effects.SpineShield = true;
            uint rng = 123;
            RiskStrikeResult result = RiskForgeRules.StrikeForcedForChecks(sword, context, ForgeOutcome.Break, ref rng);
            bool convertedToDamage = index == 1 || index == 2 || index == 3 || index == 5;
            Require(result.Accepted && result.RolledOutcome == ForgeOutcome.Break, "Protection smoke: forced roll not accepted");
            Require(result.ProtectionId == protection[index] && result.Fatal == (index == 0), "Protection smoke: wrong protection/fatal branch " + index);
            Require(result.Outcome == (convertedToDamage ? ForgeOutcome.Damage : ForgeOutcome.Break), "Protection smoke: wrong effective outcome " + index);
            Require(sword.Effects.TemperScars == (convertedToDamage ? 1 : 0) && sword.Effects.SpineDamages == (index == 5 ? 1 : 0),
                "Protection smoke: repair/spine observed the wrong effective outcome " + index);
            if (index == 5) Require(sword.Effects.CornerstoneUses == 1 && !context.InsuranceUsed &&
                sword.Effects.SpineShield && sword.Effects.PhoenixUses == 0, "Protection smoke: multiple protections consumed on one roll");
        }
        return shelves.Length;
    }

    public static int Main(string[] args)
    {
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Require(args.Length == 2, "Usage: golden.exe <resources-root> <vectors.json>");
            Require(Directory.Exists(args[0]), "Resources root is missing");
            Require(File.Exists(args[1]), "Golden-vector fixture is missing");
            UnityEngine.Resources.Root = args[0];
            JObject fixture = JObject.Parse(File.ReadAllText(args[1]));
            Require((string)fixture["schema_version"] == "1.0", "Unsupported vector schema");
            Require(fixture["outcome_order"].ToObject<string[]>().SequenceEqual(OutcomeOrder), "Fixture outcome order changed");
            Require(Enum.GetNames(typeof(ForgeOutcome)).SequenceEqual(OutcomeOrder), "ForgeOutcome numeric order changed");
            Require((int)fixture["named_count"] == 33 && (int)fixture["random_count"] == 100, "Fixture count metadata changed");
            var vectors = fixture["cases"] as JArray;
            Require(vectors != null && vectors.Count == ExpectedCases, "Exactly 133 golden cases are required");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            CheckCatalogue();
            int orderedEffectSmokeCases = CheckOrderedEffectSmoke();
            int mismatches = 0;
            int randomCases = 0;
            foreach (JObject vector in vectors)
            {
                string id = (string)vector["id"];
                Require(!string.IsNullOrEmpty(id) && ids.Add(id), "Missing or duplicate vector id: " + id);
                if (id.StartsWith("random-", StringComparison.Ordinal)) randomCases++;
                int[] expected = ReadTickets(vector["expected"], id + " expected");
                int[] independent = ReadTickets(vector["independent_expected"], id + " independent_expected");
                Require(expected.SequenceEqual(independent), id + ": stored independent expectation differs");
                var relicIds = vector["ids"].ToObject<List<string>>();
                Require(relicIds != null && relicIds.All(x => RiskRelicData.Find(x) != null), id + ": unknown relic id");

                // Match fixture mapping: these are constructed, already-primed states, not a Prime lifecycle test.
                var sword = RiskForgeRules.NewSword(1, 17, "ordinary", (int)vector["grade"]);
                sword.Hits = 1;
                sword.Effects.Primed = true;
                sword.Charge = (int?)vector["charge"] ?? 0;
                sword.FrenzyActive = (bool?)vector["frenzy"] ?? false;
                sword.FrenzyRemaining = sword.FrenzyActive ? 1 : 0;
                SetPublicFields(sword.Effects, vector["effects"] as JObject, id, "effects");
                var context = new RiskForgeContext { Relics = relicIds };
                SetPublicFields(context, vector["ctx"] as JObject, id, "ctx");
                int[] actual = RiskForgeRules.Tickets(sword, context, false);
                Require(actual.Length == 5 && actual.All(x => x >= 0) && actual.Sum() == 1000, id + ": invalid actual tickets");
                if (!actual.SequenceEqual(expected))
                {
                    mismatches++;
                    Console.Error.WriteLine(JsonConvert.SerializeObject(new
                    {
                        case_id = id, grade = sword.Grade, relics = relicIds, expected, actual
                    }));
                }
            }
            Require(randomCases == 100, "Exactly 100 named random fixture cases are required");
            Console.WriteLine(JsonConvert.SerializeObject(new
            {
                status = mismatches == 0 ? "PASS" : "FAIL",
                kind = "probability-golden",
                vectors = vectors.Count,
                mismatches,
                ordered_effect_smoke_cases = orderedEffectSmokeCases,
                catalogue = RiskRelicData.All.Count,
                active = RiskRelicData.All.Count(x => !x.Disabled),
                scope = "Supplied-state Tickets vectors and six protection-order smoke cases with test-only Unity JSON/Resources adapter; not exhaustive effects, Steam parity, Session, ads, device, or balance verification."
            }));
            return mismatches == 0 ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Golden verification error: " + exception.Message);
            return 2;
        }
    }
}
