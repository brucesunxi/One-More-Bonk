using System;
using System.Collections.Generic;

namespace OneMoreBonk.Rebuild
{
    /// <summary>
    /// Independent implementation of the researched five-outcome forge rules. It has no
    /// Unity objects, energy resource, timer hit quota, advertising service, or save IO.
    /// The session owns input recovery, mutation transactions and pending-loss decisions.
    /// </summary>
    public static class RiskForgeRules
    {
        public const int GradeCap = 99;
        public const int ChargeThreshold = 10;
        public const double BaseInterval = .64;
        public const double BaseFrenzySeconds = 4;
        public const double MoneyCap = 1e100;
        const double Epsilon = 1e-9;
        public static readonly int[] NominalFormLevels = { 0, 2, 4, 6, 9, 13, 18, 30, 45, 60, 80, 99 };
        static readonly int[] formMinimum = { 0, 1, 3, 5, 8, 12, 17, 28, 43, 58, 78, 99 };
        static readonly int[] formMaximum = { 0, 3, 5, 7, 10, 14, 19, 32, 47, 62, 82, 99 };
        static readonly string[] ordinaryFamilies = { "citadel", "holy", "sawblade", "dragonfang", "runeblade", "splitblade" };

        public static RiskSword Clone(RiskSword sword) => sword?.Copy();
        public static RiskForgeContext Clone(RiskForgeContext context) => context?.Copy();
        public static string Name(string id) => RiskRelicData.Name(id);
        public static string Description(string id) => RiskRelicData.Description(id);
        public static List<RiskRelicEntry> AvailableCatalogue(int day, IEnumerable<string> unlocks, IEnumerable<string> owned = null)
            => RiskRelicData.AvailableCatalogue(day, unlocks, owned);
        public static bool Has(RiskForgeContext context, string id)
        {
            RiskRelicEntry entry = RiskRelicData.Find(id);
            return context?.Relics != null && entry != null && !entry.Disabled && context.Relics.Contains(id);
        }
        static int Own(RiskForgeContext context, string id) => Has(context, id) ? 1 : 0;

        /// <summary>Cosmetic birth choices use their own seed, never the outcome RNG.</summary>
        public static RiskSword NewSword(int id, uint appearanceSeed, string sourceLine = "ordinary", int grade = 0)
        {
            uint state = Mix(appearanceSeed ^ unchecked((uint)id * 0x9e3779b9u));
            int lane = (int)(NextUInt(ref state) % 3u), branch = (int)(NextUInt(ref state) % 2u);
            string line = NormalizeLine(sourceLine);
            var sword = new RiskSword
            {
                Id = id, Grade = Clamp(grade, 0, GradeCap), PeakGrade = Clamp(grade, 0, GradeCap),
                SourceLine = line, SourceFamily = line == "ordinary" ? ordinaryFamilies[lane * 2 + branch] : line == "worldtree" ? "world-tree" : line == "seafood" ? "fishblade" : "funerary",
                Lane = line == "ordinary" ? lane : -1, Branch = line == "ordinary" ? branch : -1,
                MaterialRoute = (ForgeMaterialRoute)(NextUInt(ref state) % 3u),
                FormMilestones = new int[12]
            };
            for (int i = 0; i < 12; i++)
            {
                int minimum = i == 0 ? 0 : Math.Max(formMinimum[i], sword.FormMilestones[i - 1] + 1);
                int maximum = Math.Max(minimum, formMaximum[i]);
                sword.FormMilestones[i] = minimum + (int)(NextUInt(ref state) % (uint)(maximum - minimum + 1));
            }
            sword.PeakValue = Value(sword, new RiskForgeContext());
            return sword;
        }
        static string NormalizeLine(string line)
        {
            switch ((line ?? "ordinary").ToLowerInvariant())
            {
                case "worldtree": case "world-tree": case "world_tree": return "worldtree";
                case "seafood": case "fish": case "fishblade": return "seafood";
                case "funerary": return "funerary";
                default: return "ordinary";
            }
        }
        public static int Stage(RiskSword sword)
        {
            if (sword == null) return 0;
            int[] milestones = sword.FormMilestones != null && sword.FormMilestones.Length == 12 ? sword.FormMilestones : NominalFormLevels;
            int stage = 0;
            for (int i = 1; i < milestones.Length; i++) if (sword.PeakGrade >= milestones[i]) stage = i;
            return stage;
        }
        public static bool MaterialAwakened(RiskSword sword) => sword != null && sword.PeakGrade >= 80;

        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static double Money(double value)
        {
            if (double.IsNaN(value) || value <= 0) return 0;
            if (double.IsPositiveInfinity(value) || value >= MoneyCap) return MoneyCap;
            return Math.Floor(value + .5);
        }
        static double Positive(double value) => double.IsNaN(value) || value <= 0 ? 0 : Math.Min(MoneyCap, value);
        static double Multiply(double a, double b)
        {
            a = Positive(a); b = Positive(b);
            if (a == 0 || b == 0) return 0;
            return a >= MoneyCap / b ? MoneyCap : a * b;
        }
        static int AddCount(int value, int amount = 1) => (int)Math.Min(int.MaxValue, Math.Max(0L, value) + Math.Max(0L, amount));
        static int Clamp(int value, int low, int high) => value < low ? low : value > high ? high : value;
        static RiskSwordEffects Effects(RiskSword sword)
        {
            if (sword.Effects == null) sword.Effects = new RiskSwordEffects();
            return sword.Effects;
        }
        static bool Safe(RiskSword sword) => sword != null &&
            ((sword.FrenzyActive && sword.FrenzyRemaining > Epsilon) || sword.Charge >= ChargeThreshold);
        public static double Speed(RiskSword sword, RiskForgeContext context)
        {
            bool safe = Safe(sword);
            return 1 + .15 * Own(context, "speed") + (safe ? .3 * Own(context, "bellows") : .2 * Own(context, "loop"));
        }
        public static double Interval(RiskSword sword, RiskForgeContext context) => BaseInterval / Speed(sword, context);
        static double Scale(RiskSword sword, RiskForgeContext context)
        {
            return (Has(context, "thorn") ? 1.1 : 1) * (Has(context, "token") ? 1.15 : 1) *
                (sword.Grade >= 8 && Has(context, "blackstone") ? 1.3 : 1) *
                (sword.Grade >= 12 && Has(context, "dragonseal") ? 1.5 : 1);
        }
        static double LevelValue(RiskSword sword, RiskForgeContext context, int grade)
        {
            RiskSwordEffects effects = Effects(sword);
            bool special = sword.SourceLine != "ordinary" && NormalizeLine(sword.SourceLine) != "ordinary";
            double baseValue = (special ? 640 : 320) + 80 * Own(context, "coal") +
                20.0 * Math.Max(0, context.SuccessfulTributes) * Own(context, "hearth");
            baseValue = Positive(baseValue + Positive(effects.SeedBase) + Positive(effects.GrowthBase));
            return Multiply(baseValue, Math.Pow(1.5, Clamp(grade, 0, GradeCap)));
        }
        static double TotalMultiplier(RiskSword sword, RiskForgeContext context, bool duringStrike = false)
        {
            RiskSwordEffects effects = Effects(sword);
            return Multiply(Multiply(effects.Multiplier, effects.GreatMultiplier), duringStrike ? effects.ValueScale : Scale(sword, context));
        }
        static double Measure(RiskSword sword, RiskForgeContext context, bool duringStrike)
        {
            if (sword == null || context == null) return 0;
            if (sword.Broken) return Money(sword.ScrapValue);
            double unscaled = Positive(LevelValue(sword, context, sword.Grade) + Positive(Effects(sword).Flat));
            return Money(Multiply(unscaled, TotalMultiplier(sword, context, duringStrike)));
        }
        public static double Value(RiskSword sword, RiskForgeContext context) => Measure(sword, context, false);
        // Threshold value scales refresh at the end of a strike; within the ordered chain,
        // flat increments are normalized against the pre-strike scale, as researched.
        static double CurrentValue(RiskSword sword, RiskForgeContext context) => Measure(sword, context, true);
        static void UpdatePeak(RiskSword sword, RiskForgeContext context)
        {
            sword.PeakGrade = Math.Max(sword.PeakGrade, sword.Grade);
            if (!sword.Broken) sword.PeakValue = Math.Max(Positive(sword.PeakValue), Value(sword, context));
        }

        /// <summary>
        /// Commit the ownership-dependent state changes on an actual shelf transaction.
        /// This does not prime an untouched sword or consume reserved inheritance. Earned
        /// scars, held levels, lifetime tickets and value layers retain their own scopes.
        /// Read-only value/odds queries must never call this on the player's actual sword.
        /// </summary>
        public static void RefreshForShelfChanges(RiskSword sword, RiskForgeContext context)
        {
            if (sword == null || context == null || sword.Broken) return;
            RiskSwordEffects effects = Effects(sword);
            if (!Has(context, "spine")) { effects.SpineDamages = 0; effects.SpineShield = false; }
            if (!Has(context, "endlessaxle")) effects.AxleSuccesses = 0;
            if (!Has(context, "star")) effects.StarReady = false;
            if (!Has(context, "dice")) effects.DiceReady = false;
            effects.ValueScale = Scale(sword, context);
            UpdatePeak(sword, context);
        }

        /// <summary>Claim next untouched +0 inheritance once; callers may prime on selection.</summary>
        public static void Prime(RiskSword sword, RiskForgeContext context)
        {
            if (sword == null || context == null || sword.Broken) return;
            RiskSwordEffects effects = Effects(sword);
            if (!effects.Primed && sword.Hits == 0 && sword.Grade == 0)
            {
                effects.Primed = true;
                bool inherit = Has(context, "holy") || Has(context, "dawnsheath");
                if (inherit && context.InheritedGrade > 0)
                {
                    sword.Grade = Math.Min(GradeCap, context.InheritedGrade);
                    context.InheritedGrade = 0;
                }
                if (inherit && context.InheritanceChargeGrant > 0)
                {
                    sword.Charge = Math.Max(sword.Charge, Math.Min(ChargeThreshold, context.InheritanceChargeGrant));
                    context.InheritanceChargeGrant = 0;
                }
                if (Has(context, "tooth") && context.InheritedSuccessTickets > 0)
                {
                    effects.InheritedSuccessTickets = Clamp(context.InheritedSuccessTickets, 0, 4);
                    context.InheritedSuccessTickets = 0;
                }
            }
            RefreshForShelfChanges(sword, context);
        }
        static double Risk(RiskSword sword, RiskForgeContext context)
        {
            RiskSwordEffects effects = Effects(sword);
            double scars = Has(context, "repair") ? Math.Max(.4, 1 - .15 * Clamp(effects.TemperScars, 0, 4)) : 1;
            double charm = Has(context, "brace") ? Math.Max(.82, 1 - .03 * Clamp(effects.BraceStacks, 0, 6)) : 1;
            return Math.Max(0, Math.Min(1, scars * charm));
        }
        static double[] BaseWeights(RiskForgeContext context)
        {
            bool replace = Has(context, "sicklehammer");
            var weights = new double[5];
            weights[0] = Math.Max(1, (replace ? 5 : 9) - 2 * Own(context, "rivet") + 3 * Own(context, "butterfingers"));
            weights[1] = Math.Max(0, (replace ? 5 : 2) - Own(context, "mallet") + 2 * Own(context, "thorn") + 3 * Own(context, "deeppearl"));
            weights[2] = Math.Max(0, 5 - 2 * Own(context, "file") + 2 * Own(context, "hold") + 3 * Own(context, "patience") - 2 * Own(context, "thorn") + 6 * Own(context, "eternalseed"));
            weights[3] = (replace ? 5 : 12) + 4 * Own(context, "nail") + 2 * Own(context, "file") + Own(context, "mallet") + 6 * Own(context, "royalnail");
            weights[4] = (replace ? 5 : 2) + 5 * Own(context, "butterfingers") + 2 * Own(context, "pin") + Own(context, "spark") + 4 * Own(context, "oracledie") +
                (Has(context, "grudge") ? Math.Max(0, context.GrudgeTickets) : 0) + Math.Max(0, context.MourningGreatTickets);
            if (context.OssuaryTickets != null && context.OssuaryTickets.Length == 5)
                for (int i = 0; i < 5; i++) weights[i] += Math.Max(0, context.OssuaryTickets[i]);
            return weights;
        }
        /// <summary>Exact HUD/roll distribution. Preview inheritance operates on copies only.</summary>
        public static int[] Tickets(RiskSword sword, RiskForgeContext context, bool preview = true)
        {
            if (sword == null || context == null) return new[] { 0, 0, 1000, 0, 0 };
            if (preview && !Effects(sword).Primed && sword.Hits == 0 && sword.Grade == 0)
            {
                RiskSword copy = Clone(sword); RiskForgeContext runCopy = Clone(context);
                Prime(copy, runCopy);
                return Tickets(copy, runCopy, false);
            }
            RiskSwordEffects effects = Effects(sword);
            double[] weights = BaseWeights(context);
            bool safe = Safe(sword);
            if (safe) weights[4] += 6 * Own(context, "perfectcore");
            if (sword.Grade >= 8 && Has(context, "obsidian"))
            {
                double moved = Math.Min(2, weights[3]); weights[3] -= moved; weights[4] += moved;
            }
            weights[3] += Clamp(effects.InheritedSuccessTickets, 0, 4);
            if (Has(context, "kingcup")) weights[4] += Math.Max(0, effects.KingGreatTickets);
            if (Has(context, "gear")) weights[3] += Math.Max(0, effects.BronzeSuccessTickets);
            if (effects.OilHits > 0)
            {
                double moved = Math.Min(2, weights[1]); weights[1] -= moved; weights[3] += moved;
            }
            if (effects.DiceReady && Has(context, "dice")) weights[4] += 3;
            if (effects.ChalkArmed) weights[4] *= 2;
            if (effects.HasPreviousOutcome && effects.PreviousOutcome == ForgeOutcome.Hold && Has(context, "thirdeye")) weights[3] += 4;
            double risk = Risk(sword, context);
            if (effects.StarReady && Has(context, "star")) risk *= .6;
            if (effects.ThreadReady && Has(context, "thread")) risk *= .8;
            double curve = RiskRelicData.BreakCurve[Clamp(sword.Grade, 0, RiskRelicData.BreakCurve.Count - 1)];
            weights[0] *= curve / (1 - curve) * 21.0 / 9.0;
            double surviving = weights[1] + weights[2] + weights[3] + weights[4];
            double breakProbability = weights[0] / (weights[0] + surviving) * risk *
                (Has(context, "seal") ? .9 : 1) * (sword.Grade >= 6 && Has(context, "tongs") ? .75 : 1);
            weights[0] = breakProbability <= 0 ? 0 : surviving * breakProbability / Math.Max(1e-12, 1 - breakProbability);
            if (safe) weights[0] = 0;
            else if (effects.CandleArmed) weights = new double[] { 0, 0, 1, 0, 0 };
            else if (effects.PowderArmed) weights = new[] { weights[0] + weights[1] + weights[2], 0, 0, 0, weights[3] + weights[4] };
            double total = 0; foreach (double weight in weights) total += weight;
            int[] tickets = new int[5]; double[] remainder = new double[5]; int assigned = 0;
            for (int i = 0; i < 5; i++)
            {
                double exact = weights[i] / total * 1000;
                tickets[i] = (int)Math.Floor(exact); remainder[i] = exact - tickets[i]; assigned += tickets[i];
            }
            var order = new List<int> { 0, 1, 2, 3, 4 };
            order.Sort((a, b) => { int compare = remainder[b].CompareTo(remainder[a]); return compare == 0 ? a.CompareTo(b) : compare; });
            for (int i = 0; i < 1000 - assigned; i++) tickets[order[i]]++;
            return tickets;
        }
        public static void Advance(RiskSword sword, RiskForgeContext context, double dt)
        {
            if (!Finite(dt) || dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
            if (sword == null || context == null || !sword.FrenzyActive) return;
            sword.FrenzyRemaining = Math.Max(0, sword.FrenzyRemaining - dt);
            if (sword.FrenzyRemaining > Epsilon) return;
            sword.FrenzyRemaining = 0; sword.FrenzyActive = false;
            Effects(sword).FrenzyEndBonus = 0;
            if (!sword.Broken && Has(context, "headless"))
            {
                int before = sword.Grade;
                sword.Grade = Math.Min(GradeCap, sword.Grade + 1);
                Effects(sword).FrenzyEndBonus = sword.Grade - before;
                RefreshForShelfChanges(sword, context);
            }
        }
        public static RiskStrikeResult Strike(RiskSword sword, RiskForgeContext context, ref uint rng)
            => StrikeCore(sword, context, null, ref rng);
        public static RiskStrikeResult StrikeForcedForChecks(RiskSword sword, RiskForgeContext context, ForgeOutcome outcome, ref uint rng)
            => StrikeCore(sword, context, outcome, ref rng);
        static RiskStrikeResult StrikeCore(RiskSword sword, RiskForgeContext context, ForgeOutcome? forced, ref uint rng)
        {
            var result = new RiskStrikeResult();
            if (sword == null || context == null) { result.Reason = "No active sword."; return result; }
            if (sword.Broken) { result.Reason = "The sword is broken."; return result; }
            if (sword.Locked || sword.InTribute) { result.Reason = "The sword is secured."; return result; }
            if (sword.Grade >= GradeCap) { result.Reason = "Maximum grade reached."; return result; }
            Prime(sword, context);
            RiskSwordEffects effects = Effects(sword);
            if (sword.FrenzyActive && sword.FrenzyRemaining <= Epsilon) Advance(sword, context, 0);
            result.ValueBefore = Value(sword, context); result.PreviousGrade = sword.Grade; result.PreviousCharge = sword.Charge;
            if (!sword.FrenzyActive && sword.Charge >= ChargeThreshold)
            {
                sword.Charge = 0; sword.FrenzyActive = true;
                sword.FrenzyRemaining = BaseFrenzySeconds + Own(context, "redglass") + 2 * Own(context, "everwick");
                effects.FrenzyStartGrade = sword.Grade; effects.FrenzyActivations = AddCount(effects.FrenzyActivations);
                context.FrenzyEvents = AddCount(context.FrenzyEvents); result.StartedFrenzy = true;
            }
            result.WasFrenzy = sword.FrenzyActive && sword.FrenzyRemaining > Epsilon;
            result.IntervalSeconds = Interval(sword, context);
            result.Tickets = Tickets(sword, context, false);
            int roll = (int)(Unit(ref rng) * 1000); ForgeOutcome outcome = ForgeOutcome.Great;
            int sum = 0;
            for (int i = 0; i < 5; i++) if (roll < (sum += result.Tickets[i])) { outcome = (ForgeOutcome)i; break; }
            if (forced.HasValue) outcome = forced.Value;
            result.RolledOutcome = outcome; result.Outcome = outcome; result.Accepted = true;
            Apply(sword, context, outcome, result, ref rng);
            result.Grade = sword.Grade; result.Charge = sword.Charge; result.StageIndex = Stage(sword);
            result.ValueAfter = Value(sword, context); result.Fatal = sword.Broken;
            return result;
        }

        static void Proc(RiskStrikeResult result, string id, string text, double before, double after, bool valueBonus = false)
        {
            result?.Procs.Add(new RiskProc { Id = id, Text = text, Before = before, After = after, ValueBonus = valueBonus && after > before });
        }
        static void Levels(RiskSword sword, RiskForgeContext context, RiskStrikeResult result, string id, int count)
        {
            int old = sword.Grade; double before = CurrentValue(sword, context);
            sword.Grade = (int)Math.Min(GradeCap, (long)sword.Grade + Math.Max(0L, count));
            if (sword.Grade > old) Proc(result, id, "+" + (sword.Grade - old), before, CurrentValue(sword, context));
        }
        static void FlatGain(RiskSword sword, RiskForgeContext context, RiskStrikeResult result, string id, double amount)
        {
            double before = CurrentValue(sword, context), multiplier = TotalMultiplier(sword, context, true);
            if (multiplier <= 0) return;
            Effects(sword).Flat = Positive(Effects(sword).Flat + Positive(amount) / multiplier);
            Proc(result, id, "+" + Money(amount) + " G", before, CurrentValue(sword, context), true);
        }
        static void MultiplierGain(RiskSword sword, RiskForgeContext context, RiskStrikeResult result, string id, double ratio)
        {
            double before = CurrentValue(sword, context);
            Effects(sword).Multiplier = Multiply(Effects(sword).Multiplier, ratio);
            Proc(result, id, "×" + ratio, before, CurrentValue(sword, context), true);
        }
        static void BaseGain(RiskSword sword, RiskForgeContext context, RiskStrikeResult result, string id, double amount)
        {
            double before = CurrentValue(sword, context);
            Effects(sword).GrowthBase = Positive(Effects(sword).GrowthBase + amount);
            Proc(result, id, "+" + amount + " G", before, CurrentValue(sword, context), true);
        }
        static void ApplyMilestones(RiskSword sword, RiskForgeContext context, RiskStrikeResult result)
        {
            RiskSwordEffects effects = Effects(sword);
            for (int i = 0; i < GradeCap / 4; i++)
            {
                uint bit = 1u << i;
                if (sword.Grade < 4 * (i + 1) || (effects.MilestoneMask & bit) != 0) continue;
                effects.MilestoneMask |= bit;
                if (Has(context, "idol")) MultiplierGain(sword, context, result, "idol", 1.15);
            }
        }
        static void Duplicate(RiskSword sword, RiskForgeContext context, RiskStrikeResult result, ref uint rng)
        {
            if (!sword.Broken && Has(context, "twinseal") && Unit(ref rng) < .02)
            {
                result.Duplicated = true; double value = CurrentValue(sword, context);
                Proc(result, "twinseal", "DUPLICATED", value, value);
            }
        }
        static void Apply(RiskSword sword, RiskForgeContext context, ForgeOutcome rolled, RiskStrikeResult result, ref uint rng)
        {
            RiskSwordEffects effects = Effects(sword);
            int beforeGrade = sword.Grade; double beforeValue = CurrentValue(sword, context);
            bool hadPrevious = effects.HasPreviousOutcome; ForgeOutcome previous = effects.PreviousOutcome;
            bool ward = effects.WardArmed, dust = effects.DustArmed;
            effects.WardArmed = effects.DustArmed = effects.StarReady = effects.ThreadReady = effects.DiceReady = effects.ChalkArmed = false;
            if (!result.WasFrenzy)
            {
                effects.CandleArmed = effects.PowderArmed = false;
                effects.NormalHits = AddCount(effects.NormalHits);
                sword.Charge = Math.Min(ChargeThreshold, sword.Charge + 1 + (sword.Hits == 0 ? 2 * Own(context, "locket") : 0));
            }
            if (effects.OilHits > 0) effects.OilHits--;
            sword.Hits = AddCount(sword.Hits);
            ForgeOutcome actual = rolled; bool spineConverted = false;
            if (rolled == ForgeOutcome.Break)
            {
                effects.SuccessStreak = effects.HoldStreak = 0;
                if (ward)
                {
                    result.ProtectionId = "ward";
                    Proc(result, "ward", "BREAK BLOCKED", beforeValue, beforeValue);
                    Duplicate(sword, context, result, ref rng); UpdatePeak(sword, context); return;
                }
                if (Has(context, "cornerstone") && effects.CornerstoneUses == 0)
                {
                    effects.CornerstoneUses++; actual = ForgeOutcome.Damage; result.ProtectionId = "cornerstone";
                }
                else if (Has(context, "insurance") && !context.InsuranceUsed)
                {
                    context.InsuranceUsed = true; actual = ForgeOutcome.Damage; result.ProtectionId = "insurance";
                }
                else if (Has(context, "spine") && effects.SpineShield)
                {
                    effects.SpineShield = false; actual = ForgeOutcome.Damage; spineConverted = true; result.ProtectionId = "spine";
                }
                else if (Has(context, "phoenix") && effects.PhoenixUses == 0)
                {
                    effects.PhoenixUses++; result.ProtectionId = "phoenix";
                    MultiplierGain(sword, context, result, "phoenix", .6);
                    Duplicate(sword, context, result, ref rng); UpdatePeak(sword, context); return;
                }
                else { Destroy(sword, context, result, ref rng); return; }
                Proc(result, result.ProtectionId, "BREAK BLOCKED / -1", beforeValue, beforeValue);
            }
            result.Outcome = actual;
            effects.SameResultStreak = hadPrevious && previous == actual ? AddCount(effects.SameResultStreak) : 1;
            effects.HasPreviousOutcome = true; effects.PreviousOutcome = actual;
            bool success = actual == ForgeOutcome.Success || actual == ForgeOutcome.Great;
            effects.SuccessStreak = success ? AddCount(effects.SuccessStreak) : 0;
            effects.HoldStreak = actual == ForgeOutcome.Hold ? AddCount(effects.HoldStreak) : 0;
            if (success)
            {
                effects.Successes = AddCount(effects.Successes);
                if (!result.WasFrenzy) effects.NormalSuccesses = AddCount(effects.NormalSuccesses);
            }
            int gradeDelta = actual == ForgeOutcome.Great ? 2 : actual == ForgeOutcome.Success ? 1 : actual == ForgeOutcome.Damage ? -1 : 0;
            bool pearl = actual == ForgeOutcome.Damage && Has(context, "deeppearl") && effects.PearlUses < 2;
            if (pearl) { effects.PearlUses++; gradeDelta = 0; Proc(result, "deeppearl", "DAMAGE GUARDED", beforeValue, beforeValue); }
            double beforeUnscaled = Positive(LevelValue(sword, context, sword.Grade) + effects.Flat);
            sword.Grade = Clamp(sword.Grade + gradeDelta, 0, GradeCap);
            if (actual == ForgeOutcome.Great)
            {
                double afterUnscaled = Positive(LevelValue(sword, context, sword.Grade) + effects.Flat);
                if (afterUnscaled > 0) effects.GreatMultiplier = Multiply(effects.GreatMultiplier, 2.5 * beforeUnscaled / afterUnscaled);
                context.GreatEvents = AddCount(context.GreatEvents); context.GrudgeTickets = 0;
            }
            Proc(result, "", actual.ToString(), beforeValue, CurrentValue(sword, context));
            if (actual == ForgeOutcome.Damage)
            {
                effects.DamageCount = AddCount(effects.DamageCount); context.DamageEvents = AddCount(context.DamageEvents);
                if (Has(context, "repair") && effects.TemperScars < 4)
                {
                    effects.TemperScars++; Proc(result, "repair", "RISK -" + effects.TemperScars * 15 + "%", beforeValue, beforeValue);
                }
                if (Has(context, "crown"))
                {
                    double before = CurrentValue(sword, context);
                    int loss = pearl ? Math.Min(1, beforeGrade) : beforeGrade - sword.Grade;
                    effects.Flat = Positive(effects.Flat + .5 * (LevelValue(sword, context, beforeGrade) - LevelValue(sword, context, beforeGrade - loss)));
                    Proc(result, "crown", "SCAR", before, CurrentValue(sword, context), true);
                }
                if (Has(context, "ironcomb"))
                {
                    double before = CurrentValue(sword, context);
                    effects.Flat = Positive(effects.Flat + .05 * LevelValue(sword, context, beforeGrade));
                    Proc(result, "ironcomb", "SCAR", before, CurrentValue(sword, context), true);
                }
                if (Has(context, "spine") && !spineConverted)
                {
                    effects.SpineDamages = AddCount(effects.SpineDamages);
                    if (effects.SpineDamages >= 2) { effects.SpineDamages %= 2; effects.SpineShield = true; }
                }
                if (Has(context, "royalseal"))
                {
                    effects.RoyalGreatLevels = AddCount(effects.RoyalGreatLevels);
                    Proc(result, "royalseal", effects.RoyalGreatLevels + " levels banked", beforeValue, beforeValue);
                }
                if (Has(context, "apron")) FlatGain(sword, context, result, "apron", 140);
                if (Has(context, "oath") && Unit(ref rng) < .7) Levels(sword, context, result, "oath", 1);
                if (!result.WasFrenzy && Has(context, "watch")) sword.Charge = Math.Min(ChargeThreshold, sword.Charge + 1);
            }
            if (actual == ForgeOutcome.Hold)
            {
                effects.HoldsTotal = AddCount(effects.HoldsTotal); context.HoldEvents = AddCount(context.HoldEvents);
                if (Has(context, "eternalseed"))
                {
                    double before = CurrentValue(sword, context); effects.SeedBase = Positive(effects.SeedBase + 40);
                    Proc(result, "eternalseed", "+40 G", before, CurrentValue(sword, context), true);
                }
                if (Has(context, "rage") && effects.HeldGreatLevels < 4)
                {
                    effects.HeldGreatLevels++;
                    Proc(result, "rage", effects.HeldGreatLevels + " levels banked", beforeValue, beforeValue);
                }
                if (Has(context, "hold") && effects.HoldStreak % 2 == 0) Levels(sword, context, result, "hold", 1);
                if (Has(context, "still") && Unit(ref rng) < .7) Levels(sword, context, result, "still", 1);
                if (Has(context, "mold")) FlatGain(sword, context, result, "mold", 180);
                if (Has(context, "cup")) MultiplierGain(sword, context, result, "cup", 1.06);
                if (Has(context, "kingcup"))
                {
                    effects.KingGreatTickets = AddCount(effects.KingGreatTickets);
                    Proc(result, "kingcup", "GREAT TICKETS +" + effects.KingGreatTickets, beforeValue, beforeValue);
                }
                if (Has(context, "chime") && effects.ChimeLevels < 2)
                {
                    effects.ChimeLevels++;
                    Proc(result, "chime", effects.ChimeLevels + " levels banked", beforeValue, beforeValue);
                }
                effects.ThreadReady = Has(context, "thread");
                if (!result.WasFrenzy && Has(context, "hourglass") && Unit(ref rng) < .5) sword.Charge = Math.Min(ChargeThreshold, sword.Charge + 1);
            }
            if (success)
            {
                if (actual == ForgeOutcome.Success && Has(context, "endlessaxle"))
                {
                    effects.AxleSuccesses = AddCount(effects.AxleSuccesses);
                    if (effects.AxleSuccesses % 2 == 0) Levels(sword, context, result, "endlessaxle", 1);
                }
                if (beforeGrade >= 7 && Has(context, "dragonheart")) MultiplierGain(sword, context, result, "dragonheart", 1.15);
                if (Has(context, "glove") && effects.SuccessStreak % 2 == 0) Levels(sword, context, result, "glove", 1);
                if (Has(context, "echo") && Unit(ref rng) < .25) Levels(sword, context, result, "echo", 1);
                if (Has(context, "coin")) FlatGain(sword, context, result, "coin", 100);
                if (Has(context, "firstseal") && sword.Hits == 1) Levels(sword, context, result, "firstseal", 2);
                if (actual == ForgeOutcome.Success)
                {
                    if (Has(context, "gear"))
                    {
                        effects.BronzeSuccessTickets = AddCount(effects.BronzeSuccessTickets);
                        Proc(result, "gear", "THIS SWORD: SUCCESS TICKETS +1", beforeValue, beforeValue);
                    }
                    if (Has(context, "goldgear")) BaseGain(sword, context, result, "goldgear", 30);
                    if (Has(context, "chime") && effects.ChimeLevels > 0)
                    {
                        Levels(sword, context, result, "chime", effects.ChimeLevels); effects.ChimeLevels = 0;
                    }
                }
                if (hadPrevious && previous == ForgeOutcome.Damage && Has(context, "scales")) Levels(sword, context, result, "scales", 2);
                if (Has(context, "knot") && effects.SuccessStreak % 2 == 0) MultiplierGain(sword, context, result, "knot", 1.1);
                if (!result.WasFrenzy && Has(context, "brace") && effects.BraceStacks < 6) effects.BraceStacks++;
            }
            if (actual == ForgeOutcome.Great)
            {
                if (Has(context, "rage") && effects.HeldGreatLevels > 0)
                {
                    Levels(sword, context, result, "rage", effects.HeldGreatLevels); effects.HeldGreatLevels = 0;
                }
                if (Has(context, "royalseal") && effects.RoyalGreatLevels > 0)
                {
                    Levels(sword, context, result, "royalseal", effects.RoyalGreatLevels); effects.RoyalGreatLevels = 0;
                }
                if (result.WasFrenzy && Has(context, "glass")) Levels(sword, context, result, "glass", 1);
                if (Has(context, "bell")) MultiplierGain(sword, context, result, "bell", 1.2);
                if (!result.WasFrenzy && Has(context, "fork")) Levels(sword, context, result, "fork", 1);
                effects.StarReady = !result.WasFrenzy && Has(context, "star");
            }
            effects.DiceReady = (actual == ForgeOutcome.Damage || actual == ForgeOutcome.Hold) && Has(context, "dice");
            if (hadPrevious && previous != actual && Has(context, "incense")) BaseGain(sword, context, result, "incense", 20);
            if (effects.SameResultStreak % 2 == 0 && Has(context, "wax")) FlatGain(sword, context, result, "wax", 150);
            if (result.WasFrenzy && Has(context, "collar")) MultiplierGain(sword, context, result, "collar", 1.05);
            ApplyMilestones(sword, context, result);
            RiskProc firstValue = null, lastValue = null;
            foreach (RiskProc proc in result.Procs) if (proc.ValueBonus) { if (firstValue == null) firstValue = proc; lastValue = proc; }
            if (dust && firstValue != null) FlatGain(sword, context, result, "echodust", firstValue.After - firstValue.Before);
            if (effects.SameResultStreak % 2 == 0 && Has(context, "mirror") && lastValue != null)
                FlatGain(sword, context, result, "mirror", lastValue.After - lastValue.Before);
            RefreshForShelfChanges(sword, context);
            Duplicate(sword, context, result, ref rng);
        }
        static void Destroy(RiskSword sword, RiskForgeContext context, RiskStrikeResult result, ref uint rng)
        {
            RiskSwordEffects effects = Effects(sword); int grade = sword.Grade;
            context.BrokenSwords = AddCount(context.BrokenSwords);
            if (grade >= 7 && Has(context, "funeralledger") && context.FuneralClaims < 2)
            {
                context.FuneralClaims++; context.NextSupply = AddCount(context.NextSupply);
                Proc(result, "funeralledger", "NEXT DAY +1 SWORD", result.ValueBefore, result.ValueBefore);
            }
            if (grade > 0)
            {
                if (Has(context, "grudge")) context.GrudgeTickets = AddCount(context.GrudgeTickets, grade);
                int inherit = (int)Math.Floor(grade * (30 * Own(context, "holy") + 40 * Own(context, "dawnsheath")) / 100.0);
                context.InheritedGrade = Math.Max(context.InheritedGrade, inherit);
                if (inherit > 0) context.InheritanceChargeGrant = Math.Max(context.InheritanceChargeGrant, 3 * Own(context, "holy") + 4 * Own(context, "dawnsheath"));
                if (grade >= 4 && Has(context, "tooth")) context.InheritedSuccessTickets = 4;
            }
            if (grade >= 7 && Has(context, "ossuary") && effects.HasPreviousOutcome)
            {
                if (context.OssuaryTickets == null || context.OssuaryTickets.Length != 5) context.OssuaryTickets = new int[5];
                int previous = (int)effects.PreviousOutcome;
                if (previous >= 0 && previous < 5) context.OssuaryTickets[previous] = AddCount(context.OssuaryTickets[previous]);
                Proc(result, "ossuary", "RUN TICKET +1", result.ValueBefore, result.ValueBefore);
            }
            if (Has(context, "sieve"))
            {
                context.BonusGold = Money(context.BonusGold + 500);
                Proc(result, "sieve", "+500 G", result.ValueBefore, result.ValueBefore);
            }
            sword.Charge = 0; sword.FrenzyRemaining = 0; sword.FrenzyActive = false;
            effects.HeldGreatLevels = effects.RoyalGreatLevels = effects.ChimeLevels = effects.KingGreatTickets = effects.BronzeSuccessTickets = 0;
            sword.Broken = true;
            effects.SameResultStreak = 0;
            sword.ScrapValue = Has(context, "shard") ? Money(.3 * Positive(sword.PeakValue)) : 0;
            context.ScrapTribute = Money(context.ScrapTribute + sword.ScrapValue);
            if (Has(context, "grudge") && grade > 0) Proc(result, "grudge", "+" + grade, result.ValueBefore, result.ValueBefore);
            if (sword.ScrapValue > 0) Proc(result, "shard", "+" + sword.ScrapValue + " G TRIBUTE", 0, sword.ScrapValue);
        }
        static uint Mix(uint seed)
        {
            seed ^= seed >> 16; seed *= 0x7feb352du; seed ^= seed >> 15; seed *= 0x846ca68bu; seed ^= seed >> 16;
            return seed == 0 ? 0xA7C31E25u : seed;
        }
        public static uint NextUInt(ref uint state)
        {
            if (state == 0) state = 0xA7C31E25u;
            uint value = state; value ^= value << 13; value ^= value >> 17; value ^= value << 5; state = value; return value;
        }
        static double Unit(ref uint state) => NextUInt(ref state) / 4294967296.0;
    }
}
