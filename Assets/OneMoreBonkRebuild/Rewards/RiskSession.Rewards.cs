using System;
using System.Linq;

namespace OneMoreBonk.Rebuild
{
    public sealed partial class RiskSession
    {
        RiskEarnedReward retryableEarned;

        public bool TryBeginReward(string placement, out RiskRewardRequest request, out string reason)
        {
            request = null;
            if (!Active(out reason)) return false;
            if (placement == RiskRewardPlacement.RelicSlot)
            {
                if (state.Profile.UnlockedRelicSlots >= 8) return Reject("All eight relic slots are already unlocked.", out reason);
            }
            else if (placement == RiskRewardPlacement.Ward)
            {
                if (state.Run.Round % 3 != 0 || state.Run.MerchantOffers.Count == 0 || state.Run.RewardedWardGranted)
                    return Reject("The merchant ward reward is unavailable for this contract.", out reason);
            }
            else return Reject("Unknown rewarded placement.", out reason);
            bool saved = Commit(next => next.PendingReward = new RiskRewardRequest
            {
                Id = next.Profile.Id + ":ad:" + next.NextEventId, Placement = placement,
                ProfileId = next.Profile.Id, RunId = next.Run.Id, Round = next.Run.Round,
                SlotBefore = next.Profile.UnlockedRelicSlots,
                TargetSlot = placement == RiskRewardPlacement.RelicSlot ? next.Profile.UnlockedRelicSlots + 1 : 0
            }, out reason);
            if (saved) request = Snapshot().PendingReward;
            return saved;
        }

        public bool TryCancelReward(string requestId, out string reason)
        {
            if (state.PendingReward == null || state.PendingReward.Id != requestId)
                return Reject("This ad request is already closed or stale.", out reason);
            if (retryableEarned != null) return Reject("This ad was earned. Retry saving its existing reward without watching again.", out reason);
            return Commit(next => next.PendingReward = null, out reason);
        }

        public bool TryApplyEarnedReward(RiskEarnedReward receipt, IRiskRewardVerifier verifier, out string reason)
        {
            if (receipt == null || string.IsNullOrEmpty(receipt.Provider) || string.IsNullOrEmpty(receipt.ReceiptId) ||
                receipt.ReceiptId.Length > 200 || string.IsNullOrEmpty(receipt.RequestId))
                return Reject("An SDK-earned receipt is required.", out reason);
            var previous = state.RewardCommits.Find(x => x.Provider == receipt.Provider && x.ReceiptId == receipt.ReceiptId);
            if (previous != null)
            {
                if (previous.RequestId != receipt.RequestId || previous.Placement != receipt.Placement)
                    return Reject("This receipt was consumed for another request.", out reason);
                reason = null; return true; // Idempotent acknowledgement; no mutation, save or new reward.
            }
            var request = state.PendingReward;
            if (request == null || receipt.RequestId != request.Id || receipt.Placement != request.Placement ||
                receipt.ProfileId != request.ProfileId || receipt.RunId != request.RunId || receipt.Round != request.Round ||
                receipt.TargetSlot != request.TargetSlot || request.ProfileId != state.Profile.Id ||
                state.Run == null || state.Run.Id != request.RunId || state.Run.Round != request.Round)
                return Reject("The reward does not belong to this exact pending request.", out reason);
            bool verified = false;
            try { verified = verifier != null && verifier.Verify(receipt, request); } catch { }
            if (!verified) return Reject("The ad provider did not verify an earned callback.", out reason);
            if (request.Placement == RiskRewardPlacement.RelicSlot)
            {
                if (request.SlotBefore != state.Profile.UnlockedRelicSlots || request.TargetSlot != request.SlotBefore + 1 || request.TargetSlot > 8)
                    return Reject("This slot entitlement is stale.", out reason);
            }
            else if (request.Placement == RiskRewardPlacement.Ward)
            {
                if (state.Run.RewardedWardGranted || state.Run.Round % 3 != 0)
                    return Reject("This contract already received its ward or the merchant visit expired.", out reason);
            }
            else return Reject("Unknown reward placement.", out reason);

            // Preserve the earned receipt in memory through a write failure. UI close/cancel must not erase it.
            retryableEarned = CopyReceipt(receipt);
            bool saved = Commit(next =>
            {
                if (request.Placement == RiskRewardPlacement.RelicSlot) next.Profile.UnlockedRelicSlots = request.TargetSlot;
                else { AddItem(next.Run, "ward", 1); next.Run.RewardedWardGranted = true; }
                next.RewardCommits.Add(new RiskRewardCommit { RequestId = request.Id, ReceiptId = receipt.ReceiptId,
                    Provider = receipt.Provider, Placement = request.Placement });
                if (next.RewardCommits.Count > 1024) next.RewardCommits.RemoveAt(0);
                next.PendingReward = null;
            }, out reason);
            if (saved) retryableEarned = null;
            return saved;
        }

        public bool TryRetryEarnedReward(IRiskRewardVerifier verifier, out string reason)
        {
            if (retryableEarned == null) return Reject("There is no earned reward awaiting a save retry.", out reason);
            if (PersistenceBlocked && !TryCheckpoint(out reason)) return false;
            return TryApplyEarnedReward(retryableEarned, verifier, out reason);
        }

        static RiskEarnedReward CopyReceipt(RiskEarnedReward value) => new RiskEarnedReward
        {
            ReceiptId = value.ReceiptId, Provider = value.Provider, RequestId = value.RequestId,
            Placement = value.Placement, ProfileId = value.ProfileId, RunId = value.RunId,
            Round = value.Round, TargetSlot = value.TargetSlot
        };
    }
}
