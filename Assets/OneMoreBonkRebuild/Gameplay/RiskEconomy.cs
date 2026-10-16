using System;
using System.Collections.Generic;

namespace OneMoreBonk.Rebuild
{
    [Serializable]
    public sealed class RiskMerchantItemDefinition
    {
        public string Id;
        public int FirstRound;
        public double QuotaFraction;
        public double MinimumGold;
        public double Premium;
        public int Stock;
        public bool EnabledInRebuild;
    }

    /// <summary>Stateless economy calculations. The Session owns validation, mutations and commits.</summary>
    public static class RiskEconomy
    {
        public const int ContractRounds = 21;
        public const int RelicSlotHardMaximum = 8;
        public const int InitialUnlockedRelicSlots = 2;
        public const double MoneyCap = 1e100;
        static readonly double[] Targets = { 1800,2100,2600,3350,4350,5700,7500,9800,12800,16700,21500,27750,35700,45800,58650,74650,94550,119350,149950,186850,232000 };
        static readonly string[] Starters = { "nail","seal","coin","hold","repair","rage","shard","spark" };
        static readonly string[] SpecialFamilies = { "world-tree","fishblade","funerary" };

        public static double[] ContractQuotas() => (double[])Targets.Clone();
        public static string[] StarterPool() => (string[])Starters.Clone();
        public static bool IsMerchantRound(int round) => round >= 3 && round % 3 == 0;

        public static double Money(double value)
        {
            if (double.IsNaN(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (double.IsPositiveInfinity(value) || value >= MoneyCap) return MoneyCap;
            return Math.Round(value, 0, MidpointRounding.AwayFromZero);
        }
        public static double AddMoney(double left, double right)
        {
            if (double.IsNaN(left) || double.IsNaN(right) || left < 0 || right < 0) throw new ArgumentOutOfRangeException();
            return Money(left + right);
        }
        public static double BaseQuotaForRound(int round)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            return round <= ContractRounds ? Targets[round - 1] : Money(232000 * Math.Pow(1.2, round - ContractRounds));
        }
        public static double EffectiveQuota(double baseQuota, bool hasLetter, double royalCredit)
        {
            baseQuota = Money(baseQuota); royalCredit = Money(royalCredit);
            double discounted = hasLetter ? Money(baseQuota * .9) : baseQuota;
            return Money(Math.Max(1, discounted - Math.Min(royalCredit, discounted * .3)));
        }
        public static double RerollPrice(double baseQuota, int paidRerolls)
        {
            if (paidRerolls < 0) throw new ArgumentOutOfRangeException(nameof(paidRerolls));
            return Money(Math.Ceiling(Math.Min(MoneyCap, Math.Max(150, .15 * Money(baseQuota)) * Math.Pow(2.5, paidRerolls))));
        }
        public static double MerchantPrice(double firstPrice, int purchased)
        {
            if (purchased < 0) throw new ArgumentOutOfRangeException(nameof(purchased));
            return Money(Money(firstPrice) * Math.Pow(3, purchased));
        }
        static double CeilTen(double value) => Money(Math.Ceiling(Math.Min(MoneyCap, value) / 10) * 10);
        public static double BasicSwordFirstPrice(double baseQuota) => CeilTen(Math.Max(700, .4 * Money(baseQuota)));
        public static int SpecialSwordGrade(int round)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            return round < 9 ? 0 : round < 15 ? 2 : 4;
        }
        public static double SpecialSwordFirstPrice(double baseQuota, int grade)
        {
            if (grade != 0 && grade != 2 && grade != 4) throw new ArgumentOutOfRangeException(nameof(grade));
            return CeilTen(Math.Max(1100, .65 * Money(baseQuota)) * 1.75 * (grade == 0 ? 1 : grade == 2 ? 1.6 : 2.4));
        }
        public static int BasicSwordStock(int round, bool hasSilverPermit)
        {
            if (!IsMerchantRound(round)) return 0;
            int visit = round / 3;
            return (visit == 1 ? 2 : visit <= 3 ? 3 : visit <= 7 ? 4 : 5) + (hasSilverPermit ? 2 : 0);
        }
        public static int MerchantOfferSlots(int round, bool hasGhostLedger)
        {
            if (!IsMerchantRound(round)) return 0;
            return (round < 6 ? 4 : round < 15 ? 5 : 6) + (hasGhostLedger ? 2 : 0);
        }

        // Original active catalogue contains rewind; its behavior is explicitly deferred in this rebuild.
        public static RiskMerchantItemDefinition FindMerchantItem(string id)
        {
            var item = new RiskMerchantItemDefinition { Id = id, EnabledInRebuild = true };
            switch (id)
            {
                case "ward": item.FirstRound=3; item.QuotaFraction=.3; item.MinimumGold=500; item.Premium=1.4; item.Stock=3; break;
                case "rewind": item.FirstRound=3; item.QuotaFraction=.85; item.MinimumGold=1500; item.Premium=1.8; item.Stock=1; item.EnabledInRebuild=false; break;
                case "chalk": item.FirstRound=6; item.QuotaFraction=.4; item.MinimumGold=700; item.Premium=1.4; item.Stock=3; break;
                case "powder": item.FirstRound=6; item.QuotaFraction=.5; item.MinimumGold=800; item.Premium=1.8; item.Stock=1; break;
                case "starshard": item.FirstRound=6; item.QuotaFraction=.5; item.MinimumGold=800; item.Premium=1.4; item.Stock=3; break;
                case "delivery": item.FirstRound=6; item.QuotaFraction=.65; item.MinimumGold=1000; item.Premium=1.4; item.Stock=3; break;
                case "oblivion": item.FirstRound=9; item.QuotaFraction=1.2; item.MinimumGold=3500; item.Premium=1.8; item.Stock=1; break;
                default: return null;
            }
            return item;
        }
        public static RiskMerchantItemDefinition[] OriginalMerchantItems()
        {
            string[] ids = { "ward","rewind","chalk","powder","starshard","delivery","oblivion" };
            var result = new RiskMerchantItemDefinition[ids.Length];
            for (int i = 0; i < ids.Length; i++) result[i] = FindMerchantItem(ids[i]);
            return result;
        }
        public static bool IsSupportedMerchantItem(string id)
        {
            RiskMerchantItemDefinition item = FindMerchantItem(id);
            return item != null && item.EnabledInRebuild;
        }
        public static double MerchantItemFirstPrice(string id, double baseQuota)
        {
            RiskMerchantItemDefinition item = FindMerchantItem(id);
            if (item == null) throw new ArgumentException("Unknown original merchant item.", nameof(id));
            return CeilTen(Math.Max(item.MinimumGold, item.QuotaFraction * Money(baseQuota)) * item.Premium);
        }
        public static double[] ShopRarityWeights(int round)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            double rare = round < 4 ? .15 : round < 7 ? .25 : round < 13 ? .35 : .42;
            double legendary = round < 6 ? 0 : round < 9 ? .05 : round < 13 ? .12 : round < 17 ? .22 : .30;
            return new[] { 1 - rare - legendary, rare, legendary };
        }
        public static double RelicWeight(string id, int round)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            if (round >= 2 && round <= 6 && Array.IndexOf(new[] { "seal","rivet","shard","holy","repair","insurance" }, id) >= 0) return 1.75;
            if (round >= 8 && Array.IndexOf(new[] { "coin","mold","apron","wax","firstseal","coal","token" }, id) >= 0) return .45;
            return 1;
        }
        public static uint NextRandom(uint value)
        {
            value ^= value << 13; value ^= value >> 17; value ^= value << 5;
            return value == 0 ? 0xD1B54A35u : value;
        }
        static double Draw(ref uint rng) { rng = NextRandom(rng); return rng / 4294967296.0; }
        public static List<string> GenerateRelicOffers(int round, IEnumerable<string> achievements, IEnumerable<string> owned, ref uint shopRng)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            var offers = new List<string>();
            if (round == 1)
            {
                var pool = new List<string>(Starters);
                for (int i = 0; i < 3; i++)
                {
                    int index = Math.Min(pool.Count - 1, (int)(Draw(ref shopRng) * pool.Count));
                    offers.Add(pool[index]); pool.RemoveAt(index);
                }
                return offers;
            }
            List<RiskRelicEntry> available = RiskRelicData.AvailableCatalogue(round, achievements, owned);
            double[] weights = ShopRarityWeights(round);
            while (offers.Count < 3 && available.Count > 0)
            {
                double rarityDraw = Draw(ref shopRng), total = 0; int rarity = 2;
                for (int i = 0; i < 3; i++) { total += weights[i]; if (rarityDraw < total) { rarity = i; break; } }
                List<RiskRelicEntry> pool = available.FindAll(e => e.Rarity == rarity);
                if (pool.Count == 0)
                {
                    int lowest = 2; foreach (RiskRelicEntry entry in available) lowest = Math.Min(lowest, entry.Rarity);
                    pool = available.FindAll(e => e.Rarity == lowest);
                }
                double weightSum = 0; foreach (RiskRelicEntry entry in pool) weightSum += RelicWeight(entry.Id, round);
                double chosen = Draw(ref shopRng) * weightSum;
                RiskRelicEntry result = pool[pool.Count - 1];
                foreach (RiskRelicEntry entry in pool) { chosen -= RelicWeight(entry.Id, round); if (chosen < 0) { result = entry; break; } }
                offers.Add(result.Id); available.Remove(result);
            }
            return offers;
        }
        public static List<RiskMerchantOffer> GenerateMerchantOffers(int round, int anger, bool oblivionBought, RiskForgeContext context, ref uint shopRng)
        {
            if (round < 1 || anger < 0) throw new ArgumentOutOfRangeException();
            var offers = new List<RiskMerchantOffer>();
            if (!IsMerchantRound(round)) return offers;
            double quota = BaseQuotaForRound(round);
            int grade = SpecialSwordGrade(round);
            // Physical hand delivery is adapted to direct cabinet insertion by the mobile Session.
            offers.Add(new RiskMerchantOffer { Id="basic-sword", Kind="sword", Family="ordinary", Stock=BasicSwordStock(round, Has(context,"silverpermit")), FirstPrice=BasicSwordFirstPrice(quota), RequiresEmptyHands=true });
            string family = SpecialFamilies[Math.Min(2, (int)(Draw(ref shopRng) * 3))];
            offers.Add(new RiskMerchantOffer { Id="special-sword", Kind="sword", Family=family, Grade=grade, Stock=1, FirstPrice=SpecialSwordFirstPrice(quota,grade), RequiresEmptyHands=true });
            int slots = MerchantOfferSlots(round, Has(context,"ghostledger"));
            if (round == 3) AddMerchantItem(offers,"ward",quota);
            var pool = new List<string> { "ward" };
            if (round >= 6) pool.AddRange(new[] { "chalk","powder","starshard","delivery" });
            if (round >= 9 && anger > 0 && !oblivionBought) pool.Add("oblivion");
            pool.RemoveAll(id => offers.Exists(o => o.Id == id));
            while (offers.Count < slots && pool.Count > 0)
            {
                int index = Math.Min(pool.Count - 1, (int)(Draw(ref shopRng) * pool.Count));
                string id = pool[index]; pool.RemoveAt(index);
                if (id == "oblivion" && Draw(ref shopRng) >= .22) continue;
                AddMerchantItem(offers,id,quota);
            }
            return offers;
        }
        static void AddMerchantItem(List<RiskMerchantOffer> offers, string id, double quota)
        {
            RiskMerchantItemDefinition item = FindMerchantItem(id);
            if (item == null || !item.EnabledInRebuild) throw new ArgumentException("Unsupported rebuild merchant item.");
            offers.Add(new RiskMerchantOffer { Id=id, Kind="item", Stock=item.Stock, FirstPrice=MerchantItemFirstPrice(id,quota) });
        }
        static bool Has(RiskForgeContext context, string id) => RiskForgeRules.Has(context,id);
        public static int SwordCreditsForTrade(RiskSword sword, RiskForgeContext context, bool anyReroll, bool hookUsed)
        {
            if (sword == null) throw new ArgumentNullException(nameof(sword));
            return Has(context,"hook") && anyReroll && !hookUsed && sword.Grade == 0 && sword.Hits == 0 ? 2 : 1;
        }
        public static RiskSupplyPlan SupplyForNextRound(RiskForgeContext context, int minimumGrade = 0, int firstGrade = 0)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (minimumGrade < 0 || minimumGrade > 99 || firstGrade < 0 || firstGrade > 99 || context.NextSupply < 0) throw new ArgumentOutOfRangeException();
            minimumGrade = Math.Max(minimumGrade,Has(context,"lifecontract") ? 7 : 0);
            return new RiskSupplyPlan { Count=checked(3 + (Has(context,"supply") ? 1 : 0) + context.NextSupply), MinimumGrade=minimumGrade, FirstGrade=Math.Max(minimumGrade,firstGrade) };
        }
        public static RiskTributeOutcome EvaluateTribute(int round, int anger, double value, double quota, int bestDeliveredGrade, RiskForgeContext context)
        {
            if (round < 1 || anger < 0 || anger > 2 || bestDeliveredGrade < 0 || bestDeliveredGrade > 99 || context == null) throw new ArgumentOutOfRangeException();
            value = Money(value); quota = Money(quota);
            if (quota < 1) throw new ArgumentOutOfRangeException(nameof(quota));
            bool passed = value >= quota;
            int angerAfter = anger + (passed ? 0 : 1);
            bool pardon = !passed && angerAfter >= 3 && Has(context,"dragonpardon");
            if (pardon) angerAfter = 2;
            return new RiskTributeOutcome
            {
                Passed=passed, GoldPaid=value, AngerAfter=angerAfter, PardonConsumed=pardon,
                SwordCreditsGain=passed && Has(context,"collector") ? 1 : 0,
                MourningGreatTicketsGain=!passed && Has(context,"mourning") ? 1 : 0,
                ExtendedPaymentsGain=round > ContractRounds && passed ? 1 : 0,
                NextRoyalCredit=Has(context,"royalchange") ? Money(Math.Max(0,value-quota)*.3) : 0,
                NextMinimumGrade=Has(context,"lifecontract") ? 7 : passed && Has(context,"badge") ? 2 : 0,
                NextFirstGrade=passed && Has(context,"testament") && bestDeliveredGrade >= 10 ? (int)Math.Floor(bestDeliveredGrade*.3) : 0,
                Failed=angerAfter >= 3,
                // A survivable underpayment on day 21 also completes the contract.
                ContractCleared=round == ContractRounds && angerAfter < 3
            };
        }
    }
}
