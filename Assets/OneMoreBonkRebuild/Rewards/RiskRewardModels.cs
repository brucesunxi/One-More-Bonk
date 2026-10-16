using System;
using System.Collections.Generic;

namespace OneMoreBonk.Rebuild
{
    public static class RiskRewardPlacement
    {
        public const string RelicSlot = "relic-slot-unlock";
        public const string Ward = "prestrike-ward-grant";
    }

    [Serializable]
    public sealed class RiskRewardRequest
    {
        public string Id;
        public string Placement;
        public string ProfileId;
        public string RunId;
        public int Round;
        public int SlotBefore;
        public int TargetSlot;
    }

    public sealed class RiskEarnedReward
    {
        public string ReceiptId;
        public string Provider;
        public string RequestId;
        public string Placement;
        public string ProfileId;
        public string RunId;
        public int Round;
        public int TargetSlot;
    }

    // Implemented by an adapter which records the actual SDK earned callback, never by a UI close handler.
    public interface IRiskRewardVerifier
    {
        bool Verify(RiskEarnedReward receipt, RiskRewardRequest request);
    }

    [Serializable]
    public sealed class RiskRewardCommit
    {
        public string RequestId;
        public string ReceiptId;
        public string Provider;
        public string Placement;
    }

    public sealed partial class RiskState
    {
        public RiskRewardRequest PendingReward;
        public List<RiskRewardCommit> RewardCommits = new List<RiskRewardCommit>();
    }
}
