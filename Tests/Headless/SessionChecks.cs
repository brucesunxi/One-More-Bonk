using System;
using System.IO;
using System.Linq;
using OneMoreBonk.Rebuild;

public static class SessionChecks
{
    sealed class MemoryStore : IRiskSaveStore
    {
        public string Payload;
        public bool Fail;
        public int Writes;
        public bool TryRead(out string payload, out bool found, out string reason) { payload = Payload; found = Payload != null; reason = null; return true; }
        public bool TryWrite(string payload, out string reason)
        {
            if (Fail) { reason = "Injected disk-full failure"; return false; }
            Payload = payload; Writes++; reason = null; return true;
        }
    }
    sealed class CallbackVerifier : IRiskRewardVerifier
    {
        public bool Earned = true;
        public bool Verify(RiskEarnedReward receipt, RiskRewardRequest request) => Earned && receipt.Provider == "RecordedTestCallback";
    }
    static int assertions;
    static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
    static void Ok(bool condition, string reason) => Check(condition, reason ?? "Expected an accepted command.");
    static RiskSession Open(MemoryStore store, uint seed = 1234)
    {
        Ok(RiskSession.TryOpen(store, seed, out var session, out string reason), reason);
        return session;
    }
    static RiskSession Fresh(out MemoryStore store)
    {
        store = new MemoryStore(); var session = Open(store); Ok(session.TryStart(out string reason), reason); return session;
    }
    static RiskSession Fixture(Action<RiskState> setup, out MemoryStore store)
    {
        var session = Fresh(out store);
        var state = session.Snapshot(); setup(state); RiskSession.Validate(state);
        store.Payload = RiskStateCodec.Encode(state);
        return Open(store);
    }
    static RiskEarnedReward Earned(RiskRewardRequest request) => new RiskEarnedReward
    {
        ReceiptId = "receipt:" + request.Id, Provider = "RecordedTestCallback", RequestId = request.Id,
        Placement = request.Placement, ProfileId = request.ProfileId, RunId = request.RunId,
        Round = request.Round, TargetSlot = request.TargetSlot
    };

    static void BasicLoop()
    {
        var session = Fresh(out var store); string reason;
        var initial = session.Snapshot();
        Check(initial.Profile.UnlockedRelicSlots == 2 && initial.Run.Swords.Count == 3 && initial.Run.ShopOffers.Count == 3, "New player must start at 2 slots with finite supply and three gifts.");
        Check(initial.Run.Swords.Select(x => x.Lane).SequenceEqual(new[] { 0, 1, 2 }), "Supply must retain the original three lanes.");
        var leaked = session.Snapshot(); leaked.Run.Swords.Clear(); leaked.Profile.UnlockedRelicSlots = 8;
        Check(session.Snapshot().Run.Swords.Count == 3 && session.Snapshot().Profile.UnlockedRelicSlots == 2, "Snapshot leaked mutable authority.");
        string beforePreview = RiskStateCodec.Encode(session.Snapshot());
        int[] odds = session.GetProbabilities(initial.Run.SelectedId);
        Check(odds.Sum() == 1000 && odds.SequenceEqual(session.GetProbabilities(initial.Run.SelectedId)), "Probability preview changed between reads.");
        Check(beforePreview == RiskStateCodec.Encode(session.Snapshot()), "Preview consumed RNG or inheritance.");
        Ok(session.TryBuyRelic(initial.Run.ShopOffers[0], null, 1, out reason), reason);
        Check(session.Snapshot().Run.Relics.Count == 1 && !session.Snapshot().Run.StarterGift && session.Snapshot().Run.ShopOffers.Count == 0, "Gift was not consumed exactly once.");
        long revision = session.Snapshot().Revision;
        Ok(session.TryStrike(out var hit, out reason), reason);
        Check(hit.Accepted && session.Snapshot().Revision == revision + 1 && session.Snapshot().Run.TotalHits == 1, "One hammer action must make one commit.");
        Check(!session.TryStrike(out hit, out reason), "Immediate double hit bypassed cooldown.");
        double clock = session.Snapshot().Run.ForgeClock;
        session.Advance(2, false); Check(session.Snapshot().Run.ForgeClock == clock, "Menu/background advanced forge time.");
        session.Advance(1, true);
        var run = session.Snapshot().Run;
        if (run.PendingBreak != null) Ok(session.TryAcceptBreak(run.PendingBreak.EventId, out reason), reason);
        run = session.Snapshot().Run;
        int id = run.Swords[0].Id;
        Ok(session.TrySetLocked(id, true, out reason), reason);
        Check(!session.TryMarkForOrder(id, true, out reason) && !session.TrySellForCredit(id, out reason), "Lock did not protect sword from disposal.");
        Ok(session.TrySetLocked(id, false, out reason), reason);
        Ok(session.TrySelect(id, out reason), reason);
        Ok(session.TryMarkForOrder(id, true, out reason), reason);
        Check(!session.TryStrike(out hit, out reason), "A sword marked for tribute could still be forged.");
        Ok(session.TryMarkAllForOrder(true, out reason), reason);
        int owned = session.Snapshot().Run.Swords.Count;
        double tribute = session.OrderValue;
        Ok(session.TrySubmitOrder(run.Id, 1, out reason), reason);
        var next = session.Snapshot();
        Check(next.Run.Round == 2 && next.Run.Gold == tribute && next.Run.Swords.Count == 3 && owned >= 1, "Order consumed wrong swords, payment or supply.");
        revision = next.Revision;
        Check(!session.TrySubmitOrder(run.Id, 1, out reason) && session.Snapshot().Revision == revision, "Duplicate order callback settled the next day.");
        var reopened = Open(store);
        Check(RiskStateCodec.Encode(reopened.Snapshot()) == RiskStateCodec.Encode(session.Snapshot()), "Save/reopen changed the accepted state.");
    }

    static void FailureAndRestart()
    {
        var session = Fresh(out var store); string reason;
        for (int round = 1; round <= 3; round++)
        {
            var run = session.Snapshot().Run;
            Ok(session.TrySubmitOrder(run.Id, round, out reason), reason);
        }
        var ended = session.Snapshot();
        Check(ended.Run == null && ended.LastResult.Kind == "failed" && ended.LastResult.Anger == 3 && ended.Profile.Contracts == 1, "Third unprotected underpayment did not terminate once.");
        Check(ended.Profile.Achievements.Contains("FORGE_FIRSTDEATH"), "Death achievement missing.");
        Check(Open(store).Snapshot().Run == null, "Restart resurrected a terminated run.");
        Ok(session.TryStart(out reason), reason);
        Check(session.Snapshot().Run.Round == 1 && session.Snapshot().Run.Anger == 0 && session.Snapshot().Profile.Contracts == 1, "Free restart reset permanent facts or retained anger.");
    }

    static void FullShelfReplacement()
    {
        var session = Fixture(state =>
        {
            var run = state.Run; run.Round = run.Context.Round = 2;
            run.BaseQuota = run.Quota = RiskEconomy.BaseQuotaForRound(2); run.StarterGift = false;
            run.Relics.AddRange(new[] { "nail", "seal" }); run.SwordCredits = 20;
            run.ShopOffers.Clear(); run.ShopOffers.Add("file");
        }, out var store);
        string reason;
        var before = session.Snapshot();
        Check(!session.TryBuyRelic("file", null, 2, out reason), "Full two-slot shelf silently exceeded capacity.");
        Check(RiskStateCodec.Encode(before) == RiskStateCodec.Encode(session.Snapshot()), "Rejected capacity command spent currency or RNG.");
        Ok(session.TryBuyRelic("file", "nail", 2, out reason), reason);
        var after = session.Snapshot();
        Check(after.Profile.UnlockedRelicSlots == 2 && after.Run.Relics.Count == 2 && after.Run.Relics.Contains("file") &&
            !after.Run.Relics.Contains("nail") && after.Run.SwordCredits == before.Run.SwordCredits - RiskRelicData.Find("file").Price, "Free replacement required ad or spent wrong credits.");
    }

    static void Rewards()
    {
        var session = Fresh(out var store); var verifier = new CallbackVerifier(); string reason;
        for (int target = 3; target <= 8; target++)
        {
            Ok(session.TryBeginReward(RiskRewardPlacement.RelicSlot, out var request, out reason), reason);
            Check(request.TargetSlot == target && request.SlotBefore == target - 1, "Slot rewards skipped capacity.");
            uint rng = session.Snapshot().Run.ResultRngState;
            double clock = session.Snapshot().Run.ForgeClock;
            session.Advance(5, true);
            Check(session.Snapshot().Run.ForgeClock == clock && !session.TryStrike(out _, out reason), "Ad in-flight did not pause forging.");
            Check(!session.TryBeginReward(RiskRewardPlacement.RelicSlot, out _, out reason), "Two SDK requests became active.");
            var receipt = Earned(request);
            verifier.Earned = false;
            Check(!session.TryApplyEarnedReward(receipt, verifier, out reason), "Closing/showing without earned granted a reward.");
            verifier.Earned = true;
            receipt.TargetSlot++;
            Check(!session.TryApplyEarnedReward(receipt, verifier, out reason), "Cross-target receipt granted.");
            receipt.TargetSlot--;
            Ok(session.TryApplyEarnedReward(receipt, verifier, out reason), reason);
            int writes = store.Writes;
            Ok(session.TryApplyEarnedReward(receipt, verifier, out reason), reason);
            Check(store.Writes == writes && session.Snapshot().Profile.UnlockedRelicSlots == target && session.Snapshot().Run.ResultRngState == rng,
                "Duplicate receipt granted twice or slot ad consumed outcome RNG.");
            session = Open(store);
            Check(session.Snapshot().Profile.UnlockedRelicSlots == target, "Permanent slots did not survive process restart.");
        }
        Check(!session.TryBeginReward(RiskRewardPlacement.RelicSlot, out _, out reason), "A ninth slot was offered.");
        var run = session.Snapshot().Run;
        Ok(session.TryEndContract(run.Id, out reason), reason); Ok(session.TryStart(out reason), reason);
        Check(session.Snapshot().Profile.UnlockedRelicSlots == 8 && !session.Snapshot().Run.RewardedWardGranted, "New contract lost permanent slots or kept run ward budget.");

        session = Fixture(state =>
        {
            var current = state.Run; current.Round = current.Context.Round = 3;
            current.BaseQuota = current.Quota = RiskEconomy.BaseQuotaForRound(3); current.StarterGift = false; current.ShopOffers.Clear();
            current.MerchantOffers = RiskEconomy.GenerateMerchantOffers(3, 0, false, current.Context, ref current.ShopRngState);
        }, out store);
        Ok(session.TryBeginReward(RiskRewardPlacement.Ward, out var ward, out reason), reason);
        Ok(session.TryCancelReward(ward.Id, out reason), reason);
        Check(session.ItemCount("ward") == 0 && !session.Snapshot().Run.RewardedWardGranted, "Cancel/no-fill consumed contract budget.");
        Check(!session.TryApplyEarnedReward(Earned(ward), verifier, out reason), "A cancelled request could still grant.");
        Ok(session.TryBeginReward(RiskRewardPlacement.Ward, out ward, out reason), reason);
        var beforeWard = session.Snapshot();
        Ok(session.TryApplyEarnedReward(Earned(ward), verifier, out reason), reason);
        Check(session.ItemCount("ward") == 1 && session.Snapshot().Run.RewardedWardGranted &&
            !session.Snapshot().Run.Swords.Any(x => x.Effects.WardArmed), "Ward ad auto-armed or failed to grant inventory.");
        Check(session.Snapshot().Run.MerchantOffers[0].Purchased == beforeWard.Run.MerchantOffers[0].Purchased &&
            session.Snapshot().Run.ShopRngState == beforeWard.Run.ShopRngState, "Ward ad mutated store stock or shop RNG.");
        Check(!session.TryBeginReward(RiskRewardPlacement.Ward, out _, out reason), "Second ward reward in same contract.");
        Ok(session.TryUseConsumable("ward", out reason), reason);
        Check(session.ItemCount("ward") == 0 && session.Snapshot().Run.Swords.Find(x => x.Id == session.Snapshot().Run.SelectedId).Effects.WardArmed,
            "Owned ward was not manually armed.");
        Check(!session.TryUseConsumable("ward", out reason), "Same ward stack spent twice.");
        Ok(session.TryBeginReward(RiskRewardPlacement.RelicSlot, out _, out reason), reason);
    }

    static void PersistenceFailure()
    {
        var session = Fresh(out var store); string reason;
        string before = RiskStateCodec.Encode(session.Snapshot());
        store.Fail = true;
        Check(!session.TrySellForCredit(session.Snapshot().Run.SelectedId, out reason) && session.PersistenceBlocked, "Disk failure did not stop a sale.");
        Check(RiskStateCodec.Encode(session.Snapshot()) == before, "Failed save published a partial economic state.");
        Check(!session.TryStart(out reason), "Blocked persistence allowed another action.");
        store.Fail = false; Ok(session.TryCheckpoint(out reason), reason);
        Check(!session.PersistenceBlocked && RiskStateCodec.Encode(session.Snapshot()) == before, "Save retry changed the rejected state.");
        Ok(session.TryBeginReward(RiskRewardPlacement.RelicSlot, out var request, out reason), reason);
        var receipt = Earned(request); var verifier = new CallbackVerifier();
        store.Fail = true;
        Check(!session.TryApplyEarnedReward(receipt, verifier, out reason) && session.Snapshot().Profile.UnlockedRelicSlots == 2 &&
            session.Snapshot().RewardCommits.Count == 0 && session.Snapshot().PendingReward != null, "Failed reward write partly granted entitlement or consumed receipt.");
        Check(!session.TryCancelReward(request.Id, out reason), "A failed-save earned reward could be discarded as cancellation.");
        store.Fail = false;
        Ok(session.TryRetryEarnedReward(verifier, out reason), reason);
        Check(session.Snapshot().Profile.UnlockedRelicSlots == 3 && session.Snapshot().RewardCommits.Count == 1 &&
            session.Snapshot().PendingReward == null, "Existing earned receipt did not retry exactly once without another ad.");
    }

    static void FatalBranch()
    {
        var session = Fixture(state =>
        {
            var sword = state.Run.Swords[0]; sword.Grade = sword.PeakGrade = 30; sword.Hits = 1; sword.Effects.Primed = true;
            state.Run.Swords.RemoveRange(1, 2); state.Run.SelectedId = sword.Id;
            state.Run.Relics.Add("shard");
            uint chosen = 0;
            for (uint seed = 1; seed < 100000; seed++)
            {
                uint rng = seed; var copy = RiskForgeRules.Clone(sword); var ctx = RiskForgeRules.Clone(state.Run.Context);
                if (RiskForgeRules.Strike(copy, ctx, ref rng).Fatal) { chosen = seed; break; }
            }
            Check(chosen != 0, "Could not find controlled fatal RNG seed."); state.Run.ResultRngState = chosen;
        }, out var store);
        string reason; uint beforeRng = session.Snapshot().Run.ResultRngState;
        Ok(session.TryStrike(out var hit, out reason), reason);
        var pending = session.Snapshot().Run.PendingBreak;
        Check(hit.Fatal && pending != null && session.Snapshot().Run.Swords.Count == 0 && session.Snapshot().Run.RealBrokenSwords == 0 &&
            session.Snapshot().Run.ScrapValue == 0 && session.Snapshot().Run.ResultRngState != beforeRng, "Fatal branch committed deferred loss or failed to consume RNG.");
        Check(!session.TrySubmitOrder(session.Snapshot().Run.Id, 1, out reason), "Unresolved break allowed settlement.");
        session = Open(store);
        uint afterRng = session.Snapshot().Run.ResultRngState;
        Ok(session.TryAcceptBreak(pending.EventId, out reason), reason);
        Check(session.Snapshot().Run.RealBrokenSwords == 1 && session.Snapshot().Run.ScrapValue > 0 &&
            session.Snapshot().Run.ResultRngState == afterRng, "Accepting loss did not apply exactly one post-context branch.");
        Check(!session.TryAcceptBreak(pending.EventId, out reason), "Repeated result close duplicated loss.");
    }

    static void CompletionAndFiles()
    {
        var session = Fixture(state =>
        {
            var run = state.Run; run.Round = run.Context.Round = 21; run.Anger = 1; run.StarterGift = false;
            run.BaseQuota = run.Quota = RiskEconomy.BaseQuotaForRound(21); run.ShopOffers.Clear();
        }, out var store); string reason;
        var id = session.Snapshot().Run.Id;
        Ok(session.TrySubmitOrder(id, 21, out reason), reason);
        Check(session.Snapshot().Run.Status == "clear_offer" && session.Snapshot().Run.Anger == 2 &&
            session.Snapshot().Profile.Completed == 1 && session.Snapshot().Profile.Achievements.Contains("FORGE_CLEAR"), "Survivable final underpayment failed completion.");
        Ok(session.TryContinueExtended(id, out reason), reason);
        Check(session.Snapshot().Run.Round == 22 && session.Snapshot().Run.Quota == RiskEconomy.BaseQuotaForRound(22), "Extended contract did not begin at day22.");
        Ok(session.TryEndContract(id, out reason), reason);
        Check(session.Snapshot().LastResult.ContractCleared && session.Snapshot().Profile.Completed == 1, "Finishing counted completion twice.");
        string directory = Path.Combine(Path.GetTempPath(), "bonk-rebuild-save-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "player.json"); var files = new RiskFileSaveStore(path);
            Ok(files.TryWrite("first", out reason), reason); Ok(files.TryWrite("second", out reason), reason);
            Ok(files.TryRead(out string payload, out bool found, out reason), reason); Check(found && payload == "second", "Atomic file save read wrong payload.");
            File.WriteAllText(path, "corrupt-primary");
            Ok(files.TryRead(out payload, out found, out reason), reason); Check(payload == "first", "Verified backup was not recoverable.");
            File.WriteAllText(path + ".bak", "corrupt-backup");
            Check(!files.TryRead(out payload, out found, out reason) && found && File.Exists(path), "Corrupt saves were silently reset or deleted.");
        }
        finally { Directory.Delete(directory, true); }
    }

    static void InheritanceAndRoyalCreditRegressions()
    {
        string reason;
        foreach (int inheritance in new[] { 3, 4, 7 })
        foreach (int charge in new[] { 0, 2, 5 })
        {
            var session = Fixture(state =>
            {
                state.Run.Context.InheritedGrade = 2;
                state.Run.Context.InheritanceChargeGrant = inheritance;
                state.Run.Relics.AddRange(new[] { "holy", "dawnsheath" });
                RiskProgression.Award(state.Profile, "FORGE20_HOLY");
                var sword = state.Run.Swords[0]; sword.Charge = charge;
                state.Run.Consumables.Add(new RiskConsumableStack { Id = "starshard", Count = 1 });
            }, out _);
            uint rng = session.Snapshot().Run.ResultRngState;
            session.GetProbabilities(session.Snapshot().Run.SelectedId);
            Check(session.Snapshot().Run.Context.InheritanceChargeGrant == inheritance, "Odds preview spent pending inheritance.");
            Ok(session.TryUseConsumable("starshard", out reason), reason);
            var after = session.Snapshot(); var swordAfter = after.Run.Swords[0];
            Check(swordAfter.Charge == Math.Min(10, Math.Max(charge, inheritance) + 3) &&
                after.Run.Context.InheritedGrade == 0 && after.Run.Context.InheritanceChargeGrant == 0 &&
                session.ItemCount("starshard") == 0 && after.Run.ResultRngState == rng, "Starshard swallowed inherited charge or used outcome RNG.");
        }
        foreach (double oldCredit in new[] { 0.0, 15000.0 })
        foreach (bool surplus in new[] { false, true })
        {
            var session = Fixture(state =>
            {
                var run = state.Run; run.Round = run.Context.Round = 21; run.StarterGift = false;
                run.BaseQuota = RiskEconomy.BaseQuotaForRound(21); run.RoyalCredit = oldCredit;
                run.Quota = RiskEconomy.EffectiveQuota(run.BaseQuota, false, oldCredit);
                run.Relics.Add("royalchange"); run.ShopOffers.Clear();
                // Controlled settlement amount isolates carry accounting; not a natural-play balance fixture.
                run.ScrapValue = run.Quota + (surplus ? 60000 : 0);
            }, out var store);
            var runBefore = session.Snapshot().Run;
            double expectedCredit = surplus ? 18000 : 0;
            Ok(session.TrySubmitOrder(runBefore.Id, 21, out reason), reason);
            var cleared = session.Snapshot().Run;
            Check(cleared.Quota == runBefore.Quota && cleared.RoyalCredit == oldCredit && cleared.NextRoyalCredit == expectedCredit,
                "Final order mixed current quota with next-day credit.");
            session = Open(store);
            Ok(session.TryContinueExtended(runBefore.Id, out reason), reason);
            var extended = session.Snapshot().Run;
            Check(extended.RoyalCredit == expectedCredit && extended.NextRoyalCredit == 0 &&
                extended.Quota == RiskEconomy.EffectiveQuota(RiskEconomy.BaseQuotaForRound(22), false, expectedCredit),
                "Extended supply did not adopt next-day credit once.");
        }
    }

    static void FatalWriteFailures()
    {
        var session = Fixture(state =>
        {
            var sword = state.Run.Swords[0]; sword.Grade = sword.PeakGrade = 30; sword.Hits = 1; sword.Effects.Primed = true;
            uint chosen = 0;
            for (uint seed = 1; seed < 100000; seed++)
            {
                uint rng = seed;
                if (RiskForgeRules.Strike(RiskForgeRules.Clone(sword), RiskForgeRules.Clone(state.Run.Context), ref rng).Fatal) { chosen = seed; break; }
            }
            Check(chosen != 0, "Fatal failure fixture lacked a fatal seed."); state.Run.ResultRngState = chosen;
            state.Run.Relics.Add("shard");
        }, out var store); string reason;
        string before = RiskStateCodec.Encode(session.Snapshot());
        store.Fail = true;
        Check(!session.TryStrike(out var failedHit, out reason) && failedHit == null &&
            RiskStateCodec.Encode(session.Snapshot()) == before, "Failed fatal commit spent RNG, sword or result.");
        session = Open(store); // Simulate a process restart, not a save retry on the old object.
        Check(RiskStateCodec.Encode(session.Snapshot()) == before, "Failed fatal write changed the durable pre-hit state.");
        store.Fail = false; Ok(session.TryStrike(out var fatal, out reason), reason);
        Check(fatal.Fatal, "Replaying the uncommitted command did not use the original durable RNG.");
        long eventId = session.Snapshot().Run.PendingBreak.EventId;
        string pending = RiskStateCodec.Encode(session.Snapshot());
        store.Fail = true;
        Check(!session.TryAcceptBreak(eventId, out reason) && RiskStateCodec.Encode(session.Snapshot()) == pending,
            "Failed accept-loss commit partly applied death effects.");
        session = Open(store);
        Check(RiskStateCodec.Encode(session.Snapshot()) == pending, "Pending loss was lost on restart after a save failure.");
        store.Fail = false; Ok(session.TryAcceptBreak(eventId, out reason), reason);
        double scrap = session.Snapshot().Run.ScrapValue;
        Check(scrap > 0 && session.Snapshot().Run.RealBrokenSwords == 1 && !session.TryAcceptBreak(eventId, out reason) &&
            session.Snapshot().Run.ScrapValue == scrap, "Restart/repeated loss confirmation duplicated scrap or death.");
    }

    static void EmptyCabinetMerchantRescue()
    {
        var session = Fixture(state =>
        {
            var run = state.Run; run.Round = run.Context.Round = 3; run.BaseQuota = run.Quota = RiskEconomy.BaseQuotaForRound(3);
            run.StarterGift = false; run.ShopOffers.Clear(); run.Swords.Clear(); run.SelectedId = 0; run.Gold = 10000;
            run.MerchantOffers = RiskEconomy.GenerateMerchantOffers(3, 0, false, run.Context, ref run.ShopRngState);
        }, out var store); string reason;
        double before = session.Snapshot().Run.Gold;
        double price = session.Snapshot().Run.MerchantOffers.Find(x => x.Id == "basic-sword").Price;
        Ok(session.TryBuyMerchant("basic-sword", 3, out reason), reason);
        var runAfter = session.Snapshot().Run;
        Check(runAfter.Swords.Count == 1 && runAfter.SelectedId == runAfter.Swords[0].Id && runAfter.Gold == before - price,
            "Merchant rescue into an empty cabinet failed to select the new sword or spent wrong gold.");
        int selected = runAfter.SelectedId;
        Ok(session.TryBuyMerchant("special-sword", 3, out reason), reason);
        Check(session.Snapshot().Run.Swords.Count == 2 && session.Snapshot().Run.SelectedId == selected,
            "Merchant purchase wrongly required empty inventory or changed a valid selection.");
        Check(Open(store).Snapshot().Run.SelectedId == selected, "Rescue purchase did not survive reopening.");
    }

    public static int Main(string[] args)
    {
        try
        {
            UnityEngine.Resources.Root = args[0];
            int economyAssertions = EconomyChecks.Run();
            BasicLoop(); FailureAndRestart(); FullShelfReplacement(); Rewards(); PersistenceFailure(); FatalBranch(); CompletionAndFiles();
            InheritanceAndRoyalCreditRegressions(); FatalWriteFailures();
            EmptyCabinetMerchantRescue();
            Console.WriteLine("{\"status\":\"PASS\",\"session_assertions\":" + assertions + ",\"economy_assertions\":" + economyAssertions +
                ",\"scope\":\"Headless command, receipt, save and controlled branch checks; not native Unity, SDK, device, natural completion or whole-run balance proof.\"}");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
