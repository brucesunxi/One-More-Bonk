using System;
using System.Collections.Generic;

namespace OneMoreBonk.Rebuild
{
    [Serializable]
    public sealed partial class RiskProfile
    {
        public string Id = Guid.NewGuid().ToString("N");
        public int UnlockedRelicSlots = 2;
        public int Best;
        public int Contracts;
        public int Completed;
        public List<string> Achievements = new List<string>();
        public List<string> UnlockedRelics = new List<string>();
        public List<string> Discoveries = new List<string>();
    }

    [Serializable]
    public sealed partial class RiskState
    {
        public int Version = 1;
        public string RuleId = "minimal-v1";
        public long Revision;
        public long NextEventId = 1;
        public int NextSwordId = 1;
        public uint Seed = 0xA7C31E25u;
        public RiskProfile Profile = new RiskProfile();
        public RiskRun Run;
        public RiskResult LastResult;
    }

    [Serializable]
    public sealed partial class RiskRun
    {
        public string Id;
        public string Status = "active";
        public int Round = 1;
        public int Anger;
        public double Gold;
        public int SwordCredits;
        public double BaseQuota = 1800;
        public double Quota = 1800;
        public double RoyalCredit;
        public double NextRoyalCredit;
        public double ScrapValue;
        public int SelectedId;
        public int PassCount;
        public int TotalHits;
        public int Best;
        public int RealBrokenSwords;
        public int RealFrenzies;
        public bool Cleared;
        public bool CompletionCounted;
        public int ExtendedPayments;
        public int NextMinimumGrade;
        public int NextFirstGrade;
        public uint ResultRngState;
        public uint ShopRngState;
        public uint CosmeticSeed;
        public double ActiveSeconds;
        public double ForgeClock;
        public double NextAllowedForgeTime;
        public double LastAcceptedInterval = .64;
        public bool StarterGift = true;
        public bool VoucherUsed;
        public bool HookUsed;
        public bool AnyReroll;
        public int PaidRerolls;
        public bool OblivionBought;
        public bool RewardedWardGranted;
        public RiskForgeContext Context = new RiskForgeContext();
        public List<RiskSword> Swords = new List<RiskSword>();
        public List<int> OrderIds = new List<int>();
        public List<string> ShopOffers = new List<string>();
        public List<RiskMerchantOffer> MerchantOffers = new List<RiskMerchantOffer>();
        public List<RiskConsumableStack> Consumables = new List<RiskConsumableStack>();
        public List<RiskOrderReceipt> History = new List<RiskOrderReceipt>();
        public RiskPendingBreak PendingBreak;
        public List<string> Relics => Context.Relics;
    }

    // The result RNG is already consumed. Only accepting the loss applies PostContext deltas.
    // No recovery command or replay of the roll is implied by this presentation boundary.
    [Serializable]
    public sealed class RiskPendingBreak
    {
        public long EventId;
        public int SwordId;
        public RiskSword PreSword;
        public RiskSword PostSword;
        public RiskForgeContext PostContext;
        public RiskStrikeResult Result;
    }

    [Serializable]
    public sealed class RiskOrderReceipt
    {
        public string Id;
        public string RunId;
        public int Round;
        public double BaseQuota;
        public double Quota;
        public double Value;
        public double GoldPaid;
        public bool Passed;
        public int AngerAfter;
        public List<int> SwordIds = new List<int>();
    }

    [Serializable]
    public sealed class RiskMerchantOffer
    {
        public string Id;
        public string Kind;
        public string Family;
        public int Grade;
        public int Stock;
        public int Purchased;
        public double FirstPrice;
        public bool RequiresEmptyHands;
        public double Price => RiskEconomy.MerchantPrice(FirstPrice, Purchased);
    }

    [Serializable]
    public sealed class RiskConsumableStack
    {
        public string Id;
        public int Count;
    }

    [Serializable]
    public sealed partial class RiskResult
    {
        public string Id;
        public string Kind;
        public int Rounds;
        public int PassedOrders;
        public int Anger;
        public int Best;
        public int TotalHits;
        public int BrokenSwords;
        public int Frenzies;
        public double Gold;
        public double ActiveSeconds;
        public bool ContractCleared;
    }

    [Serializable]
    public sealed class RiskSupplyPlan
    {
        public int Count;
        public int MinimumGrade;
        public int FirstGrade;
    }

    [Serializable]
    public sealed class RiskTributeOutcome
    {
        public bool Passed;
        public double GoldPaid;
        public int AngerAfter;
        public bool PardonConsumed;
        public int SwordCreditsGain;
        public int MourningGreatTicketsGain;
        public int ExtendedPaymentsGain;
        public double NextRoyalCredit;
        public int NextMinimumGrade;
        public int NextFirstGrade;
        public bool Failed;
        public bool ContractCleared;
    }
}
