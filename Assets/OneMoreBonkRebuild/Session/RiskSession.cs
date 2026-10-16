using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OneMoreBonk.Rebuild
{
    // One authority for accepted actions. Views receive copies; SDKs send verified commands.
    public sealed partial class RiskSession
    {
        readonly IRiskSaveStore store;
        RiskState state;
        public bool PersistenceBlocked { get; private set; }
        public string LastSaveError { get; private set; }
        public RiskState Snapshot() => RiskStateCodec.Copy(state);
        public double CooldownRemaining => state.Run == null ? 0 : Math.Max(0, state.Run.NextAllowedForgeTime - state.Run.ForgeClock);
        public double OrderValue => TributeValue(state.Run);
        public double RerollPrice => state.Run == null ? 0 : RiskEconomy.RerollPrice(state.Run.BaseQuota, state.Run.PaidRerolls);

        RiskSession(IRiskSaveStore saveStore, RiskState saved) { store = saveStore; state = saved; }

        public static bool TryOpen(IRiskSaveStore store, uint seed, out RiskSession session, out string reason)
        {
            session = null;
            if (store == null) return Reject("A dedicated save store is required.", out reason);
            if (!store.TryRead(out string payload, out bool found, out reason)) return false;
            string loadNotice = reason;
            try
            {
                RiskState saved = found ? RiskStateCodec.Decode(payload) : new RiskState { Seed = seed == 0 ? 0xA7C31E25u : seed };
                Validate(saved);
                if (!found && !store.TryWrite(RiskStateCodec.Encode(saved), out reason)) return false;
                session = new RiskSession(store, saved);
                reason = loadNotice;
                return true;
            }
            catch (Exception error) { return Reject("Player state could not be opened: " + error.Message, out reason); }
        }

        public bool TryCheckpoint(out string reason)
        {
            try { Validate(state); }
            catch (Exception error) { return Reject(error.Message, out reason); }
            bool saved = store.TryWrite(RiskStateCodec.Encode(state), out reason);
            PersistenceBlocked = !saved;
            LastSaveError = reason;
            return saved;
        }

        bool Commit(Action<RiskState> mutation, out string reason)
        {
            if (PersistenceBlocked) return Reject("Storage is unavailable. Retry saving before continuing.", out reason);
            try
            {
                RiskState candidate = RiskStateCodec.Copy(state);
                mutation(candidate);
                candidate.Revision = checked(candidate.Revision + 1);
                candidate.NextEventId = checked(candidate.NextEventId + 1);
                Validate(candidate);
                if (!store.TryWrite(RiskStateCodec.Encode(candidate), out reason))
                {
                    PersistenceBlocked = true;
                    LastSaveError = reason;
                    return false;
                }
                state = candidate;
                LastSaveError = null;
                reason = null;
                return true;
            }
            catch (Exception error) { return Reject("Action did not commit: " + error.Message, out reason); }
        }

        public bool TryStart(out string reason)
        {
            if (state.Run != null || state.PendingReward != null) return Reject("Resolve the current contract or ad request first.", out reason);
            return Commit(next =>
            {
                uint seed = next.Seed;
                RiskForgeRules.NextUInt(ref seed);
                next.Seed = seed;
                next.Run = new RiskRun { Id = next.Profile.Id + ":run:" + next.NextEventId,
                    ResultRngState = seed, ShopRngState = Mix(seed ^ 0x3CF22E91u), CosmeticSeed = Mix(seed ^ 0x46F0A1D3u) };
                next.LastResult = null;
                NewRound(next);
            }, out reason);
        }

        // Only the foreground forge page advances this clock. Menus, ads and background pause it.
        public void Advance(double seconds, bool forgePageActive)
        {
            if (!Finite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            RiskRun run = state.Run;
            if (!forgePageActive || PersistenceBlocked || run == null || run.Status != "active" ||
                run.PendingBreak != null || state.PendingReward != null) return;
            if (!Finite(run.ForgeClock + seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            run.ActiveSeconds += seconds;
            run.ForgeClock += seconds;
            foreach (var sword in run.Swords)
            {
                RiskForgeRules.Advance(sword, run.Context, seconds);
                ObserveSword(state, sword);
            }
        }

        public int[] GetProbabilities(int swordId)
        {
            RiskSword sword = Find(state.Run, swordId);
            if (sword == null) throw new ArgumentOutOfRangeException(nameof(swordId));
            return RiskForgeRules.Tickets(sword, state.Run.Context, true);
        }

        public bool TrySelect(int swordId, out string reason)
        {
            if (!Active(out reason)) return false;
            if (Find(state.Run, swordId) == null) return Reject("This sword is no longer owned.", out reason);
            return Commit(next => next.Run.SelectedId = swordId, out reason);
        }

        public bool TrySetLocked(int swordId, bool locked, out string reason)
        {
            if (!Active(out reason)) return false;
            if (Find(state.Run, swordId) == null) return Reject("This sword is no longer owned.", out reason);
            return Commit(next =>
            {
                var sword = Find(next.Run, swordId);
                sword.Locked = locked;
                if (locked) { sword.InTribute = false; next.Run.OrderIds.Remove(swordId); }
            }, out reason);
        }

        public bool TryMarkForOrder(int swordId, bool marked, out string reason)
        {
            if (!Active(out reason)) return false;
            var sword = Find(state.Run, swordId);
            if (sword == null || (marked && sword.Locked)) return Reject("Choose an owned, unlocked sword.", out reason);
            return Commit(next =>
            {
                Find(next.Run, swordId).InTribute = marked;
                if (marked && !next.Run.OrderIds.Contains(swordId)) next.Run.OrderIds.Add(swordId);
                if (!marked) next.Run.OrderIds.Remove(swordId);
            }, out reason);
        }

        public bool TryMarkAllForOrder(bool marked, out string reason)
        {
            if (!Active(out reason)) return false;
            return Commit(next =>
            {
                next.Run.OrderIds.Clear();
                foreach (var sword in next.Run.Swords)
                {
                    sword.InTribute = marked && !sword.Locked;
                    if (sword.InTribute) next.Run.OrderIds.Add(sword.Id);
                }
            }, out reason);
        }

        public bool TryStrike(out RiskStrikeResult result, out string reason)
        {
            result = null;
            if (!Active(out reason)) return false;
            var selected = Find(state.Run, state.Run.SelectedId);
            if (selected == null || selected.Locked || selected.InTribute || selected.Grade >= 99)
                return Reject("Choose an unlocked forge sword below +99, outside the tribute selection.", out reason);
            if (CooldownRemaining > 1e-9) return Reject("The hammer is recovering.", out reason);
            RiskStrikeResult accepted = null;
            bool saved = Commit(next =>
            {
                var run = next.Run;
                var sword = Find(run, run.SelectedId);
                RiskForgeRules.Prime(sword, run.Context);
                var beforeSword = RiskForgeRules.Clone(sword);
                var beforeContext = RiskForgeRules.Clone(run.Context);
                var strike = RiskForgeRules.Strike(sword, run.Context, ref run.ResultRngState);
                if (!strike.Accepted) throw new InvalidOperationException(strike.Reason);
                accepted = strike;
                run.TotalHits = checked(run.TotalHits + 1);
                run.LastAcceptedInterval = strike.IntervalSeconds;
                run.NextAllowedForgeTime = run.ForgeClock + strike.IntervalSeconds;
                if (strike.Fatal)
                {
                    run.PendingBreak = new RiskPendingBreak { EventId = next.NextEventId, SwordId = sword.Id,
                        PreSword = beforeSword, PostSword = RiskForgeRules.Clone(sword),
                        PostContext = RiskForgeRules.Clone(run.Context), Result = strike };
                    run.Context = beforeContext;
                    run.Swords.Remove(sword);
                    run.OrderIds.Remove(sword.Id);
                    FixSelection(run);
                }
                else
                {
                    AbsorbEconomicDeltas(run);
                    if (strike.Duplicated)
                    {
                        var copy = RiskForgeRules.Clone(sword);
                        copy.Id = next.NextSwordId++;
                        copy.InTribute = copy.Locked = copy.ProjectMarked = false;
                        run.Swords.Add(copy);
                    }
                    if (strike.StartedFrenzy)
                    {
                        run.RealFrenzies = checked(run.RealFrenzies + 1);
                        RiskProgression.ObserveFrenzy(next.Profile, run);
                    }
                    ObserveSword(next, sword);
                }
            }, out reason);
            if (saved) result = accepted;
            return saved;
        }

        public bool TryAcceptBreak(long eventId, out string reason)
        {
            var pending = state.Run?.PendingBreak;
            if (pending == null || pending.EventId != eventId) return Reject("This loss has already resolved or is stale.", out reason);
            return Commit(next =>
            {
                var run = next.Run;
                run.Context = RiskForgeRules.Clone(run.PendingBreak.PostContext);
                AbsorbEconomicDeltas(run);
                run.RealBrokenSwords = checked(run.RealBrokenSwords + 1);
                RiskProgression.ObserveBreak(next.Profile, run);
                run.PendingBreak = null;
                FixSelection(run);
            }, out reason);
        }

        public bool TrySellForCredit(int swordId, out string reason)
        {
            if (!Active(out reason)) return false;
            var sword = Find(state.Run, swordId);
            if (sword == null || sword.Locked || sword.InTribute) return Reject("Choose an unlocked sword outside tribute.", out reason);
            return Commit(next =>
            {
                var run = next.Run;
                var item = Find(run, swordId);
                int credits = 1;
                if (Has(run, "hook") && run.AnyReroll && !run.HookUsed && item.Grade == 0 && item.Hits == 0)
                { credits = 2; run.HookUsed = true; }
                run.SwordCredits = checked(run.SwordCredits + credits);
                run.Swords.Remove(item);
                FixSelection(run);
            }, out reason);
        }

        public bool TryBuyRelic(string id, string replaceId, int expectedRound, out string reason)
        {
            if (!RoundActive(expectedRound, out reason)) return false;
            var run = state.Run;
            var entry = RiskRelicData.Find(id);
            if (entry == null || entry.Disabled || !run.ShopOffers.Contains(id) || Has(run, id)) return Reject("This relic is no longer an available offer.", out reason);
            if (!string.IsNullOrEmpty(replaceId) && !Has(run, replaceId)) return Reject("The chosen replacement is not on the shelf.", out reason);
            if (run.Relics.Count >= state.Profile.UnlockedRelicSlots && string.IsNullOrEmpty(replaceId))
                return Reject("Choose a relic to replace. An ad is optional.", out reason);
            if (!run.StarterGift && run.SwordCredits < entry.Price) return Reject("Not enough sword credits.", out reason);
            return Commit(next =>
            {
                var current = next.Run;
                if (!string.IsNullOrEmpty(replaceId)) current.Relics.Remove(replaceId);
                current.Relics.Add(id);
                if (current.StarterGift) { current.StarterGift = false; current.ShopOffers.Clear(); }
                else { current.SwordCredits -= entry.Price; current.ShopOffers.Remove(id); }
                RefreshShelf(current);
            }, out reason);
        }

        public bool TryDiscardRelic(string id, out string reason)
        {
            if (!Active(out reason)) return false;
            if (!Has(state.Run, id)) return Reject("This relic is not on the shelf.", out reason);
            return Commit(next => { next.Run.Relics.Remove(id); RefreshShelf(next.Run); }, out reason);
        }

        public bool TryReroll(int expectedRound, out string reason)
        {
            if (!RoundActive(expectedRound, out reason)) return false;
            if (expectedRound == 1) return Reject("The free starter gift cannot be rerolled.", out reason);
            bool free = Has(state.Run, "voucher") && !state.Run.VoucherUsed;
            if (!free && state.Run.Gold < RerollPrice) return Reject("Not enough gold.", out reason);
            return Commit(next =>
            {
                var run = next.Run;
                if (free) run.VoucherUsed = true;
                else { run.Gold = Money(run.Gold - RiskEconomy.RerollPrice(run.BaseQuota, run.PaidRerolls)); run.PaidRerolls++; }
                run.AnyReroll = true;
                run.ShopOffers = RiskEconomy.GenerateRelicOffers(run.Round, next.Profile.Achievements, run.Relics, ref run.ShopRngState);
            }, out reason);
        }

        public int ItemCount(string id) => ItemCount(state.Run, id);

        public bool TryBuyMerchant(string id, int expectedRound, out string reason)
        {
            if (!RoundActive(expectedRound, out reason)) return false;
            var offer = state.Run.MerchantOffers.Find(x => x.Id == id);
            if (offer == null || offer.Purchased >= offer.Stock) return Reject("This offer is sold out or unavailable.", out reason);
            if (id == "rewind") return Reject("Original undo is deferred; no substitute recovery is sold.", out reason);
            if (state.Run.Gold < offer.Price) return Reject("Not enough gold.", out reason);
            if (id == "oblivion" && (state.Run.Anger < 1 || state.Run.OblivionBought)) return Reject("Anger relief is unavailable.", out reason);
            return Commit(next =>
            {
                var run = next.Run;
                var purchase = run.MerchantOffers.Find(x => x.Id == id);
                run.Gold = Money(run.Gold - purchase.Price);
                purchase.Purchased++;
                if (purchase.Kind == "sword") AddSword(next, purchase.Family, purchase.Grade);
                else if (id == "delivery") run.Context.NextSupply = checked(run.Context.NextSupply + 2);
                else if (id == "oblivion") { run.Anger--; run.OblivionBought = true; }
                else AddItem(run, id, 1);
                if (RiskProgression.IsMerchantSoldOut(run.MerchantOffers)) RiskProgression.ObserveSoldOut(next.Profile);
            }, out reason);
        }

        public bool TryUseConsumable(string id, out string reason)
        {
            if (!Active(out reason)) return false;
            var run = state.Run;
            var sword = Find(run, run.SelectedId);
            if (sword == null || sword.Locked || sword.InTribute || sword.Grade >= 99 || ItemCount(run, id) < 1)
                return Reject("Choose a legal forge sword and an owned item.", out reason);
            if (Array.IndexOf(new[] { "ward", "chalk", "powder", "starshard" }, id) < 0) return Reject("This item is unavailable.", out reason);
            var preview = RiskForgeRules.Clone(sword);
            RiskForgeRules.Prime(preview, RiskForgeRules.Clone(run.Context));
            if (id != "chalk" && (preview.FrenzyActive || preview.Charge >= 10)) return Reject("This item cannot be used during Frenzy or full charge.", out reason);
            if ((id == "ward" && sword.Effects.WardArmed) || (id == "chalk" && sword.Effects.ChalkArmed) || (id == "powder" && sword.Effects.PowderArmed))
                return Reject("This next-hit item is already armed.", out reason);
            return Commit(next =>
            {
                var item = Find(next.Run, next.Run.SelectedId);
                RiskForgeRules.Prime(item, next.Run.Context);
                next.Run.Consumables.Find(x => x.Id == id).Count--;
                if (id == "ward") item.Effects.WardArmed = true;
                if (id == "chalk") item.Effects.ChalkArmed = true;
                if (id == "powder") item.Effects.PowderArmed = true;
                if (id == "starshard") item.Charge = Math.Min(10, item.Charge + 3);
            }, out reason);
        }

        public bool TrySubmitOrder(string runId, int expectedRound, out string reason)
        {
            if (!RoundActive(expectedRound, out reason)) return false;
            if (state.Run.Id != runId) return Reject("This order belongs to a previous contract.", out reason);
            return Commit(next =>
            {
                var run = next.Run;
                double value = TributeValue(run);
                var delivered = new List<int>(run.OrderIds);
                int best = delivered.Select(id => Find(run, id)?.Grade ?? 0).DefaultIfEmpty(0).Max();
                int angerBefore = run.Anger;
                var outcome = RiskEconomy.EvaluateTribute(run.Round, run.Anger, value, run.Quota, best, run.Context);
                run.Swords.RemoveAll(x => delivered.Contains(x.Id));
                run.OrderIds.Clear();
                run.ScrapValue = 0;
                run.Gold = Money(run.Gold + outcome.GoldPaid);
                run.Anger = outcome.AngerAfter;
                run.NextRoyalCredit = outcome.NextRoyalCredit;
                run.SwordCredits = checked(run.SwordCredits + outcome.SwordCreditsGain);
                run.Context.MourningGreatTickets = checked(run.Context.MourningGreatTickets + outcome.MourningGreatTicketsGain);
                run.ExtendedPayments += outcome.ExtendedPaymentsGain;
                if (outcome.PardonConsumed) run.Relics.Remove("dragonpardon");
                if (outcome.Passed)
                {
                    run.PassCount++; run.Context.SuccessfulTributes++;
                    if (angerBefore == 2) Award(next.Profile, "FORGE_LASTCHANCE");
                    if (value >= Math.Min(RiskForgeRules.MoneyCap, run.Quota * 3)) Award(next.Profile, "FORGE_TRIPLETRIBUTE");
                    if (run.ExtendedPayments >= 3) Award(next.Profile, "FORGE_OVERTIME3");
                }
                else run.Context.FailedTributes++;
                run.History.Add(new RiskOrderReceipt { Id = run.Id + ":order:" + run.Round, RunId = run.Id,
                    Round = run.Round, BaseQuota = run.BaseQuota, Quota = run.Quota, Value = value,
                    GoldPaid = outcome.GoldPaid, Passed = outcome.Passed, AngerAfter = run.Anger, SwordIds = delivered });
                if (run.History.Count > 1024) run.History.RemoveAt(0);
                FixSelection(run);
                if (outcome.Failed) { Award(next.Profile, "FORGE_FIRSTDEATH"); EndRun(next, "failed"); return; }
                if (outcome.ContractCleared)
                {
                    run.Cleared = true; run.Status = "clear_offer";
                    if (!run.CompletionCounted) { next.Profile.Completed++; run.CompletionCounted = true; }
                    Award(next.Profile, "FORGE_CLEAR");
                    run.NextMinimumGrade = outcome.NextMinimumGrade; run.NextFirstGrade = outcome.NextFirstGrade;
                    return;
                }
                run.RoyalCredit = run.NextRoyalCredit; run.NextRoyalCredit = 0;
                run.Round++;
                NewRound(next, outcome.NextMinimumGrade, outcome.NextFirstGrade);
            }, out reason);
        }

        public bool TryContinueExtended(string runId, out string reason)
        {
            if (state.PendingReward != null || state.Run == null || state.Run.Id != runId || state.Run.Status != "clear_offer")
                return Reject("There is no completed contract awaiting a decision.", out reason);
            return Commit(next =>
            {
                var run = next.Run;
                run.Status = "active"; run.Round++;
                run.RoyalCredit = run.NextRoyalCredit; run.NextRoyalCredit = 0;
                int minimum = run.NextMinimumGrade, first = run.NextFirstGrade;
                run.NextMinimumGrade = run.NextFirstGrade = 0;
                NewRound(next, minimum, first);
            }, out reason);
        }

        public bool TryEndContract(string runId, out string reason)
        {
            if (state.Run == null || state.Run.Id != runId || state.Run.PendingBreak != null || state.PendingReward != null)
                return Reject("Resolve the current action before closing this contract.", out reason);
            return Commit(next => EndRun(next, next.Run.Cleared ? "complete" : "abandoned"), out reason);
        }

        static void NewRound(RiskState next, int minimum = 0, int first = 0)
        {
            var run = next.Run;
            run.Context.Round = run.Round; run.Context.InsuranceUsed = false; run.Context.FuneralClaims = 0;
            run.BaseQuota = RiskEconomy.BaseQuotaForRound(run.Round);
            RecalculateQuota(run);
            run.PaidRerolls = 0; run.VoucherUsed = run.HookUsed = run.AnyReroll = false;
            var supply = RiskEconomy.SupplyForNextRound(run.Context, minimum, first);
            run.Context.NextSupply = 0;
            for (int i = 0; i < supply.Count; i++) AddSword(next, "ordinary", Math.Max(supply.MinimumGrade, i == 0 ? supply.FirstGrade : 0), i % 3);
            run.StarterGift = run.Round == 1;
            run.ShopOffers = RiskEconomy.GenerateRelicOffers(run.Round, next.Profile.Achievements, run.Relics, ref run.ShopRngState);
            run.MerchantOffers = RiskEconomy.GenerateMerchantOffers(run.Round, run.Anger, run.OblivionBought, run.Context, ref run.ShopRngState);
            FixSelection(run);
        }

        static void AddSword(RiskState next, string line, int grade, int lane = -1)
        {
            var run = next.Run;
            RiskForgeRules.NextUInt(ref run.CosmeticSeed);
            var sword = RiskForgeRules.NewSword(next.NextSwordId++, run.CosmeticSeed, line, grade);
            sword.OriginDay = run.Round;
            if (line == "ordinary" && lane >= 0)
            {
                string[] families = { "citadel", "holy", "sawblade", "dragonfang", "runeblade", "splitblade" };
                sword.Lane = lane; sword.SourceFamily = families[lane * 2 + sword.Branch];
            }
            if (line != "ordinary") sword.ArtRevision = "merchant-concept-v1-12";
            run.Swords.Add(sword);
            FixSelection(run);
            ObserveSword(next, sword);
        }

        static void EndRun(RiskState next, string kind)
        {
            var run = next.Run;
            next.LastResult = new RiskResult { Id = run.Id, Kind = kind, Rounds = run.History.LastOrDefault()?.Round ?? 0,
                PassedOrders = run.PassCount, Anger = run.Anger, Best = run.Best, TotalHits = run.TotalHits,
                BrokenSwords = run.RealBrokenSwords, Frenzies = run.RealFrenzies, Gold = run.Gold,
                ActiveSeconds = run.ActiveSeconds, ContractCleared = run.Cleared };
            next.Profile.Contracts++;
            next.Run = null;
        }

        static void ObserveSword(RiskState next, RiskSword sword)
        {
            RiskProgression.ObserveSword(next.Profile, next.Run, sword);
        }
        static void Award(RiskProfile profile, string id) => RiskProgression.Award(profile, id);
        static void AbsorbEconomicDeltas(RiskRun run)
        {
            run.Gold = Money(run.Gold + run.Context.BonusGold);
            run.ScrapValue = Money(run.ScrapValue + run.Context.ScrapTribute);
            run.Context.BonusGold = run.Context.ScrapTribute = 0;
        }
        static void RecalculateQuota(RiskRun run) => run.Quota = RiskEconomy.EffectiveQuota(run.BaseQuota, Has(run, "letter"), run.RoyalCredit);
        static void RefreshShelf(RiskRun run)
        {
            RecalculateQuota(run);
            foreach (var sword in run.Swords) RiskForgeRules.RefreshForShelfChanges(sword, run.Context);
        }
        static double TributeValue(RiskRun run)
        {
            if (run == null) return 0;
            double value = run.ScrapValue;
            foreach (int id in run.OrderIds) value = Money(value + RiskForgeRules.Value(Find(run, id), run.Context));
            return Money(value);
        }
        static RiskSword Find(RiskRun run, int id) => run?.Swords.Find(x => x.Id == id);
        static bool Has(RiskRun run, string id) => RiskForgeRules.Has(run.Context, id);
        static void FixSelection(RiskRun run) { if (Find(run, run.SelectedId) == null) run.SelectedId = run.Swords.FirstOrDefault()?.Id ?? 0; }
        static int ItemCount(RiskRun run, string id) => run?.Consumables.Find(x => x.Id == id)?.Count ?? 0;
        static void AddItem(RiskRun run, string id, int count)
        {
            var stack = run.Consumables.Find(x => x.Id == id);
            if (stack == null) { stack = new RiskConsumableStack { Id = id }; run.Consumables.Add(stack); }
            stack.Count = checked(stack.Count + count);
        }
        bool Active(out string reason)
        {
            if (state.Run == null || state.Run.Status != "active" || state.Run.PendingBreak != null || state.PendingReward != null)
                return Reject("Resolve the current action before another workshop command.", out reason);
            reason = null; return true;
        }
        bool RoundActive(int round, out string reason)
        {
            if (!Active(out reason)) return false;
            if (state.Run.Round != round) return Reject("This offer belongs to a previous order.", out reason);
            reason = null; return true;
        }
        static bool Reject(string text, out string reason) { reason = text; return false; }
        static double Money(double value) => RiskForgeRules.Money(value);
        static bool Finite(double value) => RiskForgeRules.Finite(value);
        static uint Mix(uint value) { value ^= value >> 16; value *= 0x7FEB352Du; value ^= value >> 15; value *= 0x846CA68Bu; value ^= value >> 16; return value == 0 ? 0xD1B54A35u : value; }

        public static void Validate(RiskState saved)
        {
            RiskStateValidation.Validate(saved);
        }
    }
}
