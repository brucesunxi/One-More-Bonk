using System;
using System.Collections.Generic;

namespace OneMoreBonk.Rebuild
{
    [Serializable]
    public sealed partial class RiskSword
    {
        public int Id;
        public int Grade;
        public int PeakGrade;
        public int Hits;
        public int Charge;
        public double FrenzyRemaining;
        public bool FrenzyActive;
        public bool Broken;
        public bool Locked;
        public bool ProjectMarked;
        public bool InTribute;
        public string SourceFamily = "citadel";
        public string SourceLine = "ordinary";
        public int Lane = -1;
        public int Branch = -1;
        public ForgeMaterialRoute MaterialRoute;
        public int[] FormMilestones;
        public string ArtRevision = "redwarm-v2-frontdense12";
        public int AdRestores;
        public int ItemRestores;
        public bool IsFixture;
        public int OriginDay;
        public float ViewYaw;
        public float ViewPitch;
        public float ViewDistance = 8.3f;
        public bool ViewFlipped;
        public double PeakValue;
        public double ScrapValue;
        public RiskSwordEffects Effects = new RiskSwordEffects();

        // Compatibility aliases are properties; canonical save fields remain Grade/PeakGrade/etc.
        public int Level { get => Grade; set => Grade = value; }
        public int Peak { get => PeakGrade; set => PeakGrade = value; }
        public string Family { get => SourceFamily; set => SourceFamily = value; }
        public int OriginRound { get => OriginDay; set => OriginDay = value; }
        public int AdRecoveries { get => AdRestores; set => AdRestores = value; }
        public int ItemRecoveries { get => ItemRestores; set => ItemRestores = value; }

        internal RiskSword Copy()
        {
            RiskSword copy = (RiskSword)MemberwiseClone();
            copy.FormMilestones = FormMilestones == null ? null : (int[])FormMilestones.Clone();
            copy.Effects = Effects == null ? new RiskSwordEffects() : Effects.Copy();
            return copy;
        }
    }

    [Serializable]
    public sealed class RiskSwordEffects
    {
        public bool Primed;
        public bool HasPreviousOutcome;
        public ForgeOutcome PreviousOutcome;
        public int SameResultStreak;
        public int SuccessStreak;
        public int HoldStreak;
        public int NormalHits;
        public int NormalSuccesses;
        public int DamageCount;
        public int HoldsTotal;
        public int Successes;
        public int BraceStacks;
        public int TemperScars;
        public int PhoenixUses;
        public int CornerstoneUses;
        public int PearlUses;
        public int AxleSuccesses;
        public int SpineDamages;
        public bool SpineShield;
        public int HeldGreatLevels;
        public int RoyalGreatLevels;
        public int ChimeLevels;
        public int KingGreatTickets;
        public int BronzeSuccessTickets;
        public int InheritedSuccessTickets;
        public uint MilestoneMask;
        public bool ThreadReady;
        public bool StarReady;
        public bool DiceReady;
        public bool WardArmed;
        public bool ChalkArmed;
        public bool PowderArmed;
        // Retired items remain representable for faithful inspection, never sold or armed by the new session.
        public int OilHits;
        public bool CandleArmed;
        public bool DustArmed;
        public int FrenzyActivations;
        public int FrenzyStartGrade;
        public int FrenzyEndBonus;
        public double SeedBase;
        public double GrowthBase;
        public double Flat;
        public double ValueScale = 1;
        public double Multiplier = 1;
        public double GreatMultiplier = 1;
        internal RiskSwordEffects Copy() => (RiskSwordEffects)MemberwiseClone();
    }

    [Serializable]
    public sealed partial class RiskForgeContext
    {
        public List<string> Relics = new List<string>();
        public int[] OssuaryTickets = new int[5];
        public int GrudgeTickets;
        public int MourningGreatTickets;
        public int SuccessfulTributes;
        public int FailedTributes;
        public int InheritedGrade;
        public int InheritanceChargeGrant;
        public int InheritedSuccessTickets;
        public bool InsuranceUsed;
        public int BrokenSwords;
        public int FuneralClaims;
        public int NextSupply;
        // These are pending economic deltas: Session absorbs/clears them after accepting the post-state.
        // For fatal rolls, Session seals them in the post-branch until the player accepts the loss.
        public double BonusGold;
        public double ScrapTribute;
        public int GreatEvents;
        public int DamageEvents;
        public int HoldEvents;
        public int FrenzyEvents;
        public int Round = 1;
        internal RiskForgeContext Copy()
        {
            RiskForgeContext copy = (RiskForgeContext)MemberwiseClone();
            copy.Relics = Relics == null ? new List<string>() : new List<string>(Relics);
            copy.OssuaryTickets = OssuaryTickets == null ? new int[5] : (int[])OssuaryTickets.Clone();
            return copy;
        }
    }

    [Serializable]
    public sealed class RiskProc
    {
        public string Id;
        public string Text;
        public double Before;
        public double After;
        public bool ValueBonus;
    }

    [Serializable]
    public sealed class RiskStrikeResult
    {
        public bool Accepted;
        public string Reason;
        public ForgeOutcome RolledOutcome;
        public ForgeOutcome Outcome;
        public int PreviousGrade;
        public int Grade;
        public int PreviousCharge;
        public int Charge;
        public int StageIndex;
        public double ValueBefore;
        public double ValueAfter;
        public double IntervalSeconds;
        public bool Fatal;
        public string ProtectionId;
        public bool Duplicated;
        public bool WasFrenzy;
        public bool StartedFrenzy;
        public bool FrenzyEnded;
        public int[] Tickets;
        public List<RiskProc> Procs = new List<RiskProc>();
    }

    [Serializable]
    public sealed class RiskRelicEntry
    {
        public string Id;
        public string Name;
        public string Description;
        public string NameEn;
        public string SourceUrl;
        public int Rarity;
        public int FirstDay;
        public int Price;
        public string Group;
        public string Family;
        public string Unlock;
        public bool Starter;
        public bool Disabled;
    }
}
