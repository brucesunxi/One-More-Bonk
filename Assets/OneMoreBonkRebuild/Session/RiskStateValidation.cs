using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OneMoreBonk.Rebuild
{
    public static class RiskStateValidation
    {
        static void Require(bool condition, string reason) { if (!condition) throw new InvalidDataException(reason); }
        static bool Money(double value) => RiskForgeRules.Finite(value) && value >= 0 && value <= RiskEconomy.MoneyCap;
        static bool Unique<T>(IEnumerable<T> values) => values != null && values.Distinct().Count() == values.Count();

        public static void Validate(RiskState state)
        {
            Require(state != null && state.Version == 1 && state.RuleId == "minimal-v1" && state.Revision >= 0 &&
                state.NextEventId > 0 && state.NextSwordId > 0 && state.Seed != 0, "Unsupported or invalid player-state header.");
            var profile = state.Profile;
            Require(profile != null && !string.IsNullOrEmpty(profile.Id) && profile.UnlockedRelicSlots >= 2 && profile.UnlockedRelicSlots <= 8 &&
                profile.Best >= 0 && profile.Best <= 99 && profile.Contracts >= 0 && profile.Completed >= 0 && profile.Completed <= (long)profile.Contracts + 1 &&
                Unique(profile.Achievements) && Unique(profile.UnlockedRelics) && Unique(profile.Discoveries), "Invalid permanent profile.");
            foreach (string id in profile.Achievements) Require(RiskProgression.FindAchievement(id) != null, "Unknown achievement.");
            foreach (string id in profile.UnlockedRelics) Require(RiskRelicData.Find(id) != null &&
                RiskProgression.Achievements().Any(x => x.UnlockedRelicId == id && profile.Achievements.Contains(x.Id)), "Relic unlock lacks its original achievement.");
            Require(state.RewardCommits != null && state.RewardCommits.Count <= 1024 &&
                Unique(state.RewardCommits.Select(x => x?.RequestId)) &&
                Unique(state.RewardCommits.Select(x => x == null ? null : x.Provider + ":" + x.ReceiptId)), "Invalid reward receipts.");
            foreach (var receipt in state.RewardCommits)
                Require(receipt != null && !string.IsNullOrEmpty(receipt.RequestId) && !string.IsNullOrEmpty(receipt.Provider) &&
                    !string.IsNullOrEmpty(receipt.ReceiptId) && IsPlacement(receipt.Placement), "Invalid committed reward.");
            if (state.LastResult != null)
            {
                var result = state.LastResult;
                Require(!string.IsNullOrEmpty(result.Id) && (result.Kind == "failed" || result.Kind == "complete" || result.Kind == "abandoned") &&
                    Money(result.Gold) && result.Rounds >= 0 && result.PassedOrders >= 0 && result.Anger >= 0 && result.Anger <= 3 &&
                    result.Best >= 0 && result.Best <= 99 && result.TotalHits >= 0 && result.BrokenSwords >= 0 && result.Frenzies >= 0 &&
                    RiskForgeRules.Finite(result.ActiveSeconds) && result.ActiveSeconds >= 0, "Invalid terminal result.");
            }
            if (state.Run == null)
            {
                Require(state.PendingReward == null, "An ad request cannot outlive its bound contract.");
                return;
            }
            var run = state.Run;
            Require(!string.IsNullOrEmpty(run.Id) && (run.Status == "active" || run.Status == "clear_offer") && run.Round >= 1 && run.Anger >= 0 && run.Anger < 3 &&
                Money(run.Gold) && Money(run.BaseQuota) && run.BaseQuota == RiskEconomy.BaseQuotaForRound(run.Round) && Money(run.Quota) && run.Quota >= 1 &&
                Money(run.RoyalCredit) && Money(run.NextRoyalCredit) && Money(run.ScrapValue) && run.SwordCredits >= 0 && run.PassCount >= 0 && run.TotalHits >= 0 &&
                run.RealBrokenSwords >= 0 && run.RealFrenzies >= 0 && run.ExtendedPayments >= 0 && run.PaidRerolls >= 0 &&
                run.ResultRngState != 0 && run.ShopRngState != 0 && run.CosmeticSeed != 0 &&
                RiskForgeRules.Finite(run.ActiveSeconds) && run.ActiveSeconds >= 0 && RiskForgeRules.Finite(run.ForgeClock) && run.ForgeClock >= 0 &&
                RiskForgeRules.Finite(run.NextAllowedForgeTime) && run.NextAllowedForgeTime >= 0 &&
                RiskForgeRules.Finite(run.LastAcceptedInterval) && run.LastAcceptedInterval > 0 &&
                run.NextMinimumGrade >= 0 && run.NextMinimumGrade <= 99 && run.NextFirstGrade >= 0 && run.NextFirstGrade <= 99,
                "Invalid contract counters, clock or economy.");
            Require(run.Status != "clear_offer" || (run.Cleared && run.CompletionCounted && run.Round == 21), "Invalid completion decision.");
            ValidateContext(run.Context, profile.UnlockedRelicSlots);
            Require(run.Context.Round == run.Round && run.Context.BonusGold == 0 && run.Context.ScrapTribute == 0 &&
                run.Quota == RiskEconomy.EffectiveQuota(run.BaseQuota, run.Relics.Contains("letter"), run.RoyalCredit), "Contract context or effective quota mismatch.");
            Require(run.Swords != null && Unique(run.OrderIds) && run.ShopOffers != null && Unique(run.ShopOffers) && run.ShopOffers.Count <= 3 &&
                run.MerchantOffers != null && run.Consumables != null && run.History != null && run.History.Count <= 1024, "Missing or duplicate contract collections.");
            var swordIds = new HashSet<int>();
            foreach (var sword in run.Swords)
            {
                ValidateSword(sword, state.NextSwordId);
                Require(!sword.Broken && swordIds.Add(sword.Id) && sword.InTribute == run.OrderIds.Contains(sword.Id) &&
                    (!sword.InTribute || !sword.Locked), "Invalid live sword ownership or tribute marking.");
            }
            Require(run.OrderIds.All(swordIds.Contains) && (run.Swords.Count == 0 ? run.SelectedId == 0 : swordIds.Contains(run.SelectedId)), "Missing selected or tribute sword.");
            Require(!run.StarterGift || run.Round == 1, "Starter gift leaked into a later day.");
            foreach (string id in run.ShopOffers)
            {
                var entry = RiskRelicData.Find(id);
                Require(entry != null && !entry.Disabled && !run.Relics.Contains(id) &&
                    (run.Round == 1 ? run.StarterGift && RiskEconomy.StarterPool().Contains(id) :
                    entry.FirstDay <= run.Round && (string.IsNullOrEmpty(entry.Unlock) || profile.Achievements.Contains(entry.Unlock))), "Illegal relic offer.");
            }
            Require(run.Round % 3 == 0 || run.MerchantOffers.Count == 0, "Merchant stock exists outside a visit.");
            Require(Unique(run.MerchantOffers.Select(x => x?.Id)), "Duplicate merchant offers.");
            foreach (var offer in run.MerchantOffers)
                Require(offer != null && (offer.Id == "basic-sword" || offer.Id == "special-sword" || RiskEconomy.IsSupportedMerchantItem(offer.Id)) &&
                    offer.Stock > 0 && offer.Purchased >= 0 && offer.Purchased <= offer.Stock && Money(offer.FirstPrice) && offer.FirstPrice >= 1 &&
                    (offer.Kind == "sword" || offer.Kind == "item"), "Invalid merchant stock or price.");
            Require(Unique(run.Consumables.Select(x => x?.Id)), "Duplicate consumable stacks.");
            foreach (var stack in run.Consumables)
                Require(stack != null && new[] { "ward", "chalk", "powder", "starshard" }.Contains(stack.Id) && stack.Count >= 0, "Invalid consumable stack.");
            Require(Unique(run.History.Select(x => x?.Round)), "An order settled twice.");
            foreach (var order in run.History)
                Require(order != null && order.RunId == run.Id && order.Id == run.Id + ":order:" + order.Round &&
                    order.Round >= 1 && order.Round <= run.Round && Money(order.BaseQuota) && Money(order.Quota) && order.Quota >= 1 &&
                    Money(order.Value) && order.Value == order.GoldPaid && order.Passed == (order.Value >= order.Quota) &&
                    order.AngerAfter >= 0 && order.AngerAfter < 3 && Unique(order.SwordIds), "Invalid settlement receipt.");
            if (run.PendingBreak != null)
            {
                var pending = run.PendingBreak;
                Require(pending.EventId > 0 && pending.EventId < state.NextEventId && !swordIds.Contains(pending.SwordId) && pending.Result != null &&
                    pending.Result.Accepted && pending.Result.Fatal && pending.SwordId == pending.PreSword?.Id && pending.SwordId == pending.PostSword?.Id &&
                    !pending.PreSword.Broken && pending.PostSword.Broken, "Invalid pending loss.");
                ValidateSword(pending.PreSword, state.NextSwordId); ValidateSword(pending.PostSword, state.NextSwordId);
                ValidateContext(pending.PostContext, profile.UnlockedRelicSlots);
                Require(pending.PostContext.Round == run.Round && pending.PostContext.Relics.SequenceEqual(run.Relics), "Pending loss belongs to another shelf or day.");
            }
            if (state.PendingReward != null)
            {
                var request = state.PendingReward;
                Require(run.Status == "active" && run.PendingBreak == null && !string.IsNullOrEmpty(request.Id) && IsPlacement(request.Placement) &&
                    request.ProfileId == profile.Id && request.RunId == run.Id && request.Round == run.Round && request.SlotBefore == profile.UnlockedRelicSlots &&
                    !state.RewardCommits.Any(x => x.RequestId == request.Id), "Invalid pending ad request.");
                Require(request.Placement == RiskRewardPlacement.RelicSlot ? request.TargetSlot == request.SlotBefore + 1 && request.TargetSlot <= 8 :
                    request.TargetSlot == 0 && run.Round % 3 == 0 && !run.RewardedWardGranted, "Invalid ad entitlement or contract budget.");
            }
        }

        static bool IsPlacement(string value) => value == RiskRewardPlacement.RelicSlot || value == RiskRewardPlacement.Ward;

        static void ValidateContext(RiskForgeContext context, int capacity)
        {
            Require(context != null && Unique(context.Relics) && context.Relics.Count <= capacity && context.OssuaryTickets != null &&
                context.OssuaryTickets.Length == 5 && context.OssuaryTickets.All(x => x >= 0) && context.GrudgeTickets >= 0 &&
                context.MourningGreatTickets >= 0 && context.SuccessfulTributes >= 0 && context.FailedTributes >= 0 &&
                context.InheritedGrade >= 0 && context.InheritedGrade <= 99 && context.InheritanceChargeGrant >= 0 && context.InheritanceChargeGrant <= 10 &&
                context.InheritedSuccessTickets >= 0 && context.BrokenSwords >= 0 && context.FuneralClaims >= 0 && context.FuneralClaims <= 2 &&
                context.NextSupply >= 0 && Money(context.BonusGold) && Money(context.ScrapTribute) && context.Round >= 1,
                "Invalid forging context.");
            foreach (string id in context.Relics) Require(RiskRelicData.Find(id) != null && !RiskRelicData.Find(id).Disabled, "Illegal owned relic.");
        }

        static void ValidateSword(RiskSword sword, int nextId)
        {
            Require(sword != null && sword.Id > 0 && sword.Id < nextId && sword.Grade >= 0 && sword.Grade <= 99 &&
                sword.PeakGrade >= sword.Grade && sword.PeakGrade <= 99 && sword.Hits >= 0 && sword.Charge >= 0 && sword.Charge <= 10 &&
                RiskForgeRules.Finite(sword.FrenzyRemaining) && sword.FrenzyRemaining >= 0 && sword.FrenzyRemaining <= 7.000001 &&
                sword.OriginDay >= 1 && sword.Effects != null && sword.FormMilestones != null && sword.FormMilestones.Length == 12 &&
                Money(sword.PeakValue) && Money(sword.ScrapValue) && RiskProgression.Families().Any(x => x.Id == sword.SourceFamily), "Invalid sword.");
            int[] minimum = { 0,1,3,5,8,12,17,28,43,58,78,99 }, maximum = { 0,3,5,7,10,14,19,32,47,62,82,99 };
            for (int i = 0; i < 12; i++) Require(sword.FormMilestones[i] >= minimum[i] && sword.FormMilestones[i] <= maximum[i] &&
                (i == 0 || sword.FormMilestones[i] > sword.FormMilestones[i - 1]), "Invalid visual form milestones.");
            foreach (double layer in new[] { sword.Effects.SeedBase, sword.Effects.GrowthBase, sword.Effects.Flat,
                sword.Effects.Multiplier, sword.Effects.GreatMultiplier, sword.Effects.ValueScale }) Require(Money(layer), "Invalid sword value layer.");
        }
    }
}
