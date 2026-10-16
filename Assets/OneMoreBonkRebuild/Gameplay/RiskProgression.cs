using System;
using System.Collections.Generic;

namespace OneMoreBonk.Rebuild
{
    [Serializable]
    public sealed class RiskAchievementDefinition
    {
        public string Id;
        public string Condition;
        public int Threshold;
        public string Family;
        public string UnlockedRelicId;
    }

    [Serializable]
    public sealed class RiskSwordFamilyDefinition
    {
        public string Id;
        public bool Special;
        public int Lane;
        public int Branch;
        public string AchievementId;
        public string UnlockedRelicId;
    }

    /// <summary>Original permanent facts only. Observe calls belong inside the Session's transaction.</summary>
    public static class RiskProgression
    {
        static readonly string[] FamilyIds = { "citadel","holy","sawblade","dragonfang","runeblade","splitblade","world-tree","fishblade","funerary" };
        static readonly string[] FamilyAchievements = { "FORGE20_CITADEL","FORGE20_HOLY","FORGE20_SAW","FORGE20_DRAGON","FORGE20_RUNE","FORGE20_SPLIT","FORGE20_WORLDTREE","FORGE20_FISH","FORGE20_FUNERARY" };
        static readonly string[] FamilyRelics = { "cornerstone","dawnsheath","endlessaxle","dragonheart","perfectcore","twinseal","eternalseed","deeppearl","funeralledger" };

        public static RiskSwordFamilyDefinition[] Families()
        {
            var result = new RiskSwordFamilyDefinition[FamilyIds.Length];
            for (int i = 0; i < result.Length; i++) result[i] = new RiskSwordFamilyDefinition
            {
                Id=FamilyIds[i], Special=i>=6, Lane=i<6 ? i/2 : -1, Branch=i<6 ? i%2 : -1,
                AchievementId=FamilyAchievements[i], UnlockedRelicId=FamilyRelics[i]
            };
            return result;
        }
        public static string OrdinaryFamily(int lane, int branch)
        {
            if (lane < 0 || lane > 2 || branch < 0 || branch > 1) throw new ArgumentOutOfRangeException();
            return FamilyIds[lane*2+branch];
        }
        public static RiskAchievementDefinition[] Achievements()
        {
            var result = new List<RiskAchievementDefinition>();
            for (int i = 0; i < FamilyIds.Length; i++) result.Add(new RiskAchievementDefinition
            {
                Id=FamilyAchievements[i], Condition="surviving-family-grade", Threshold=20, Family=FamilyIds[i], UnlockedRelicId=FamilyRelics[i]
            });
            result.Add(Definition("FORGE_CLEAR","survive-contract-round",21,"sicklehammer"));
            result.Add(Definition("FORGE_BROKEN10","actual-breaks-in-contract",10,"butterfingers"));
            result.Add(Definition("FORGE_FRENZY5","frenzy-starts-in-contract",5,"everwick"));
            result.Add(Definition("FORGE_TRIPLETRIBUTE","payment-times-effective-quota",3,"royalchange"));
            result.Add(Definition("FORGE_LASTCHANCE","full-payment-at-anger",2,"dragonpardon"));
            result.Add(Definition("FORGE_SOLDOUT","purchase-all-stock-in-one-visit",1,"ghostledger"));
            result.Add(Definition("FORGE_OVERTIME3","full-extended-payments-in-contract",3,"lifecontract"));
            result.Add(Definition("FORGE_FIRST10","surviving-grade",10,null));
            result.Add(Definition("FORGE_FIRSTDEATH","third-unprotected-underpayment",3,null));
            result.Add(Definition("FORGE_FIRSTFRENZY","first-nontutorial-frenzy",1,null));
            return result.ToArray();
        }
        static RiskAchievementDefinition Definition(string id, string condition, int threshold, string relic)
            => new RiskAchievementDefinition { Id=id, Condition=condition, Threshold=threshold, UnlockedRelicId=relic };
        public static RiskAchievementDefinition FindAchievement(string id)
        {
            foreach (RiskAchievementDefinition definition in Achievements()) if (definition.Id == id) return definition;
            return null;
        }
        public static void Award(RiskProfile profile, string id)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.Achievements == null || profile.UnlockedRelics == null) throw new ArgumentException("Missing permanent fact collections.",nameof(profile));
            RiskAchievementDefinition definition = FindAchievement(id);
            if (definition == null) throw new ArgumentException("Unknown original achievement.",nameof(id));
            if (!profile.Achievements.Contains(id)) profile.Achievements.Add(id);
            if (!string.IsNullOrEmpty(definition.UnlockedRelicId) && !profile.UnlockedRelics.Contains(definition.UnlockedRelicId)) profile.UnlockedRelics.Add(definition.UnlockedRelicId);
        }
        public static string DiscoveryKey(RiskSword sword)
        {
            if (sword == null) throw new ArgumentNullException(nameof(sword));
            return (sword.ArtRevision == "merchant-concept-v1-12" ? "merchant:"+sword.SourceFamily+":" : "redwarm:") +
                RiskForgeRules.Stage(sword) + ":" + sword.MaterialRoute + ":" + (sword.PeakGrade>=80 ? "awake" : "dormant");
        }
        public static void ObserveSword(RiskProfile profile, RiskRun run, RiskSword sword)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (sword == null || sword.Broken || sword.IsFixture) return;
            profile.Best = Math.Max(profile.Best,sword.PeakGrade);
            if (run != null) run.Best = Math.Max(run.Best,sword.PeakGrade);
            string discovery = DiscoveryKey(sword);
            if (!profile.Discoveries.Contains(discovery)) profile.Discoveries.Add(discovery);
            if (sword.Grade >= 10) Award(profile,"FORGE_FIRST10");
            if (sword.Grade >= 20)
            {
                int family = Array.IndexOf(FamilyIds,sword.SourceFamily);
                if (family >= 0) Award(profile,FamilyAchievements[family]);
            }
        }
        // Caller has already incremented the actual counter exactly once; these never increment it again.
        public static void ObserveBreak(RiskProfile profile, RiskRun run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (run.RealBrokenSwords >= 10) Award(profile,"FORGE_BROKEN10");
        }
        public static void ObserveFrenzy(RiskProfile profile, RiskRun run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (run.RealFrenzies >= 1) Award(profile,"FORGE_FIRSTFRENZY");
            if (run.RealFrenzies >= 5) Award(profile,"FORGE_FRENZY5");
        }
        public static void ObserveTribute(RiskProfile profile, RiskRun run, RiskTributeOutcome outcome, double value, double quota, int angerBefore)
        {
            if (run == null || outcome == null) throw new ArgumentNullException();
            if (outcome.Passed)
            {
                if (angerBefore == 2) Award(profile,"FORGE_LASTCHANCE");
                if (value >= Math.Min(RiskEconomy.MoneyCap,quota*3)) Award(profile,"FORGE_TRIPLETRIBUTE");
                if (run.Round > RiskEconomy.ContractRounds && run.ExtendedPayments >= 3) Award(profile,"FORGE_OVERTIME3");
            }
            if (outcome.ContractCleared) ObserveClear(profile);
            if (outcome.Failed) ObserveDeath(profile);
        }
        public static bool IsMerchantSoldOut(IEnumerable<RiskMerchantOffer> offers)
        {
            if (offers == null) return false;
            bool any = false;
            foreach (RiskMerchantOffer offer in offers)
            {
                if (offer == null || offer.Stock < 1 || offer.Purchased < offer.Stock) return false;
                any = true;
            }
            return any;
        }
        public static void ObserveSoldOut(RiskProfile profile) => Award(profile,"FORGE_SOLDOUT");
        public static void ObserveClear(RiskProfile profile) => Award(profile,"FORGE_CLEAR");
        public static void ObserveDeath(RiskProfile profile) => Award(profile,"FORGE_FIRSTDEATH");
    }
}
