#if UNITY_EDITOR || BONK_DEVICE_ACCEPTANCE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace OneMoreBonk.Rebuild.Acceptance
{
    // Compiled into the isolated acceptance player only. Never an entitlement path in the game.
    public sealed class RebuildDeviceAcceptance : MonoBehaviour
    {
        public string BuildId;
        [Serializable] public sealed class CaseResult { public string Name, Status, Detail; public int Assertions; }
        [Serializable] public sealed class Report
        {
            public string BuildId, CompletedUtc, Platform, UnityVersion, Status;
            public string Scope = "Controlled Session fixtures on native Unity; not natural gameplay, UI or real SDK earned evidence.";
            public List<CaseResult> Cases = new List<CaseResult>();
        }
        sealed class GateStore : IRiskSaveStore
        {
            public readonly RiskFileSaveStore Files;
            public bool DenyWrites;
            public GateStore(string path) { Files = new RiskFileSaveStore(path); }
            public bool TryRead(out string payload, out bool found, out string reason) => Files.TryRead(out payload, out found, out reason);
            public bool TryWrite(string payload, out string reason)
            {
                if (DenyWrites) { reason = "Acceptance-injected storage denial"; return false; }
                return Files.TryWrite(payload, out reason);
            }
        }
        sealed class FixtureVerifier : IRiskRewardVerifier
        {
            public bool Verify(RiskEarnedReward receipt, RiskRewardRequest request) => receipt.Provider == "ControlledAcceptanceOnly";
        }
        readonly Report report = new Report();
        string directory;
        int assertions;
        string status = "Starting controlled native checks...";
        public static Report Run(string buildId, string root)
        {
            var host = new GameObject("Temporary Acceptance Runner") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var runner = host.AddComponent<RebuildDeviceAcceptance>();
                runner.BuildId = buildId; runner.directory = root; runner.RunAll();
                return runner.report;
            }
            finally { DestroyImmediate(host); }
        }
        IEnumerator Start()
        {
            yield return null;
            directory = Path.Combine(Application.persistentDataPath, "controlled-acceptance");
            RunAll();
        }
        void RunAll()
        {
            Directory.CreateDirectory(directory);
            report.BuildId = BuildId; report.Platform = Application.platform.ToString(); report.UnityVersion = Application.unityVersion;
            Case("native-null-preview", NativeNullPreview);
            Case("orders-1-through-23", OrdersAndExtension);
            Case("ward-natural-break", WardBreak);
            Case("pending-break-reopen", PendingBreak);
            Case("full-shelf-write-failure", ShelfWriteFailure);
            Case("slots-and-earned-write-retry", SlotsAndEarned);
            Case("file-backup-corruption", Backups);
            report.Status = report.Cases.All(x => x.Status == "PASS") ? "PASS" : "FAIL";
            report.CompletedUtc = DateTime.UtcNow.ToString("O");
            status = report.Status + " / " + report.Cases.Count + " cases / " + assertions + " assertions";
            File.WriteAllText(Path.Combine(directory, "acceptance-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("BONK_DEVICE_ACCEPTANCE_" + report.Status + ": " + JsonUtility.ToJson(report));
        }
        void Case(string name, Action action)
        {
            int before = assertions;
            var result = new CaseResult { Name = name };
            try { action(); result.Status = "PASS"; }
            catch (Exception error) { result.Status = "FAIL"; result.Detail = error.Message; }
            result.Assertions = assertions - before;
            report.Cases.Add(result);
        }
        void Check(bool condition, string detail)
        { assertions++; if (!condition) throw new InvalidOperationException(detail); }
        void Ok(bool condition, string reason) => Check(condition, reason ?? "Rejected command");
        GateStore Store(string name) => new GateStore(Path.Combine(directory, name + "-" + Guid.NewGuid().ToString("N") + ".json"));
        RiskSession Open(GateStore store)
        { Ok(RiskSession.TryOpen(store, 1234, out var session, out string reason), reason); return session; }
        RiskSession Fresh(GateStore store)
        { var session = Open(store); Ok(session.TryStart(out string reason), reason); return session; }
        RiskSession Fixture(RiskSession session, GateStore store, Action<RiskState> edit)
        {
            var copy = session.Snapshot(); edit(copy); RiskSession.Validate(copy);
            Ok(store.TryWrite(RiskStateCodec.Encode(copy), out string reason), reason);
            return Open(store);
        }
        static RiskEarnedReward Receipt(RiskRewardRequest r) => new RiskEarnedReward {
            ReceiptId = "acceptance:" + r.Id, Provider = "ControlledAcceptanceOnly", RequestId = r.Id,
            Placement = r.Placement, ProfileId = r.ProfileId, RunId = r.RunId, Round = r.Round, TargetSlot = r.TargetSlot
        };
        void NativeNullPreview()
        {
            var empty = RiskStateCodec.Copy(new RiskState());
            Check(empty.Run == null && empty.LastResult == null && empty.PendingReward == null, "Inline-null flags failed");
            var store = Store("null"); var s = Fresh(store);
            string before = RiskStateCodec.Encode(s.Snapshot());
            Check(s.GetProbabilities(s.Snapshot().Run.SelectedId).Sum() == 1000, "Tickets not 1000");
            Check(before == RiskStateCodec.Encode(s.Snapshot()), "Preview changed durable authority");
            Check(before == RiskStateCodec.Encode(Open(store).Snapshot()), "Native file reopen changed state");
        }
        void OrdersAndExtension()
        {
            var store = Store("orders"); var s = Fresh(store);
            string id = s.Snapshot().Run.Id;
            for (int day = 1; day <= 23; day++)
            {
                // Fixture supplies exact tribute value. This deliberately does not claim natural sword growth.
                s = Fixture(s, store, state => {
                    state.Run.StarterGift = false; state.Run.ShopOffers.Clear();
                    state.Run.ScrapValue = state.Run.Quota;
                });
                var before = s.Snapshot();
                Check(before.Run.Round == day, "Wrong active day");
                Ok(s.TrySubmitOrder(id, day, out string reason), reason);
                Check(s.Snapshot().Run.Gold == before.Run.Gold + before.Run.Quota, "Wrong gold payment");
                long revision = s.Snapshot().Revision;
                Check(!s.TrySubmitOrder(id, day, out reason) && s.Snapshot().Revision == revision, "Duplicate day settled twice");
                s = Open(store);
                Check(s.Snapshot().Run.History.Count == day, "Order history did not persist");
                if (day == 21)
                {
                    Check(s.Snapshot().Run.Status == "clear_offer" && s.Snapshot().Profile.Completed == 1, "21-day completion missing");
                    Ok(s.TryContinueExtended(id, out reason), reason);
                    Check(!s.TryContinueExtended(id, out reason), "Extension continued twice");
                }
            }
            Check(s.Snapshot().Run.Round == 24 && s.Snapshot().Profile.Completed == 1, "Extended flow corrupted completion");
            Ok(s.TryEndContract(id, out string endReason), endReason);
            Check(!s.TryEndContract(id, out endReason), "Contract ended twice");
            s = Open(store); Ok(s.TryStart(out endReason), endReason);
            Check(s.Snapshot().Run.Round == 1 && s.Snapshot().Run.Swords.Count == 3 && s.Snapshot().Profile.Completed == 1,
                "Restart changed permanent completion or supply");
        }
        RiskSession BreakFixture(GateStore store, bool ward)
        {
            var s = Fresh(store);
            return Fixture(s, store, state => {
                var sword = state.Run.Swords[0]; sword.Grade = sword.PeakGrade = 30;
                sword.Hits = 1; sword.Effects.Primed = true;
                state.Run.Swords.RemoveRange(1, 2); state.Run.ResultRngState = 1;
                state.Run.Relics.Add("shard");
                if (ward) state.Run.Consumables.Add(new RiskConsumableStack { Id = "ward", Count = 1 });
            });
        }
        void WardBreak()
        {
            var rawStore = Store("raw-break"); var raw = BreakFixture(rawStore, false);
            Ok(raw.TryStrike(out var fatal, out string reason), reason);
            Check(fatal.Fatal && fatal.RolledOutcome == ForgeOutcome.Break, "Controlled RNG did not naturally roll Break");
            var store = Store("ward"); var s = BreakFixture(store, true);
            int[] before = s.GetProbabilities(s.Snapshot().Run.SelectedId);
            Ok(s.TryUseConsumable("ward", out reason), reason);
            Check(before.SequenceEqual(s.GetProbabilities(s.Snapshot().Run.SelectedId)), "Ward changed raw draw probabilities");
            s = Open(store); Ok(s.TryStrike(out var hit, out reason), reason);
            var run = s.Snapshot().Run;
            Check(hit.RolledOutcome == ForgeOutcome.Break && hit.ProtectionId == "ward" && !hit.Fatal,
                "Ward did not cancel the genuine Break");
            Check(run.Swords[0].Grade == 30 && !run.Swords[0].Effects.WardArmed && s.ItemCount("ward") == 0 &&
                run.PendingBreak == null && run.RealBrokenSwords == 0 && run.ScrapValue == 0, "Protected loss applied death or retained ward");
            Check(run.ResultRngState == raw.Snapshot().Run.ResultRngState, "Ward rerolled result RNG");
        }
        void PendingBreak()
        {
            var store = Store("pending"); var s = BreakFixture(store, false);
            Ok(s.TryStrike(out var hit, out string reason), reason);
            Check(hit.Fatal, "Expected genuine fatal result");
            string pending = RiskStateCodec.Encode(s.Snapshot()); s = Open(store);
            Check(pending == RiskStateCodec.Encode(s.Snapshot()), "Pending loss or consumed RNG did not survive reopen");
            var run = s.Snapshot().Run; uint rng = run.ResultRngState;
            Check(!s.TrySubmitOrder(run.Id, 1, out reason), "Pending loss allowed tribute");
            Ok(s.TryAcceptBreak(run.PendingBreak.EventId, out reason), reason);
            double scrap = s.Snapshot().Run.ScrapValue;
            Check(scrap > 0 && s.Snapshot().Run.RealBrokenSwords == 1 && s.Snapshot().Run.ResultRngState == rng,
                "Loss did not commit once");
            Check(!s.TryAcceptBreak(run.PendingBreak.EventId, out reason) && s.Snapshot().Run.ScrapValue == scrap, "Repeated loss duplicated scrap");
        }
        void ShelfWriteFailure()
        {
            var store = Store("shelf"); var s = Fresh(store);
            s = Fixture(s, store, state => {
                var run = state.Run; run.Round = run.Context.Round = 2; run.StarterGift = false;
                run.BaseQuota = run.Quota = RiskEconomy.BaseQuotaForRound(2);
                run.Relics.AddRange(new[] { "nail", "seal" }); run.SwordCredits = 20;
                run.ShopOffers.Clear(); run.ShopOffers.Add("file");
            });
            string before = RiskStateCodec.Encode(s.Snapshot()); store.DenyWrites = true;
            Check(!s.TryBuyRelic("file", "nail", 2, out string reason) && s.PersistenceBlocked, "Injected write denial did not block replacement");
            Check(before == RiskStateCodec.Encode(s.Snapshot()), "Rejected replacement lost relic or credits");
            s = Open(store); Check(before == RiskStateCodec.Encode(s.Snapshot()), "Failed replacement reached disk");
            store.DenyWrites = false; Ok(s.TryBuyRelic("file", "nail", 2, out reason), reason);
            s = Open(store);
            Check(s.Snapshot().Run.Relics.Count == 2 && s.Snapshot().Run.Relics.Contains("file") && !s.Snapshot().Run.Relics.Contains("nail"),
                "Replacement or free two-slot capacity changed after reopen");
        }
        void SlotsAndEarned()
        {
            var store = Store("slots"); var s = Fresh(store); var verifier = new FixtureVerifier();
            for (int target = 3; target <= 8; target++)
            {
                Ok(s.TryBeginReward(RiskRewardPlacement.RelicSlot, out var request, out string reason), reason);
                var receipt = Receipt(request);
                if (target == 3)
                {
                    store.DenyWrites = true;
                    Check(!s.TryApplyEarnedReward(receipt, verifier, out reason), "Denied earned write committed");
                    Check(s.Snapshot().Profile.UnlockedRelicSlots == 2 && s.Snapshot().PendingReward != null, "Partial earned state published");
                    Check(!s.TryCancelReward(request.Id, out reason), "Earned evidence was cancelled");
                    store.DenyWrites = false;
                    Ok(s.TryCheckpoint(out reason), reason);
                }
                Ok(s.TryApplyEarnedReward(receipt, verifier, out reason), reason);
                long revision = s.Snapshot().Revision;
                Ok(s.TryApplyEarnedReward(receipt, verifier, out reason), reason);
                Check(s.Snapshot().Revision == revision, "Duplicate receipt wrote another entitlement");
                s = Open(store);
                Check(s.Snapshot().Profile.UnlockedRelicSlots == target && s.Snapshot().RewardCommits.Count == target - 2,
                    "Slot sequence or receipt persistence changed");
            }
            Check(!s.TryBeginReward(RiskRewardPlacement.RelicSlot, out _, out string endReason), "Ninth slot request accepted");
            Ok(s.TryEndContract(s.Snapshot().Run.Id, out endReason), endReason); Ok(s.TryStart(out endReason), endReason);
            Check(s.Snapshot().Profile.UnlockedRelicSlots == 8 && !s.Snapshot().Run.RewardedWardGranted, "Contract reset lost permanent capacity");
        }
        void Backups()
        {
            var store = Store("backup");
            Ok(store.TryWrite("previous", out string reason), reason); Ok(store.TryWrite("current", out reason), reason);
            File.WriteAllText(store.Files.Path, "corrupt-primary");
            Ok(store.TryRead(out string payload, out bool found, out reason), reason);
            Check(found && payload == "previous" && !string.IsNullOrEmpty(reason), "Backup recovery lacked correct payload or warning");
            File.WriteAllText(store.Files.Path + ".bak", "corrupt-backup");
            Check(!store.TryRead(out _, out found, out reason) && found && File.Exists(store.Files.Path), "Both corrupt saves silently reset or disappeared");
        }
        void OnGUI()
        {
            GUI.skin.label.fontSize = Mathf.Max(20, Screen.width / 30);
            GUI.Label(new Rect(24, 90, Screen.width - 48, 240), "ONE MORE BONK — ACCEPTANCE ONLY\n" + status +
                "\nControlled fixtures; no real ads; separate save.\nNot a player build or natural completion proof.");
        }
    }
}
#endif
