using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using OneMoreBonk.Rebuild;

// Research actor: real Session commands, no outcome-RNG peeking or post-start fixture writes.
public static class BalanceProbe
{
    const int MaxHits = 2000;
    const int MaxCommands = 15000;
    const string Provider = "BalanceProbeSimulatedEarned";
    sealed class MemoryStore : IRiskSaveStore
    {
        string payload;
        public int Writes;
        public bool TryRead(out string value,out bool found,out string reason) { value=payload;found=payload!=null;reason=null;return true; }
        public bool TryWrite(string value,out string reason) { payload=value;Writes++;reason=null;return true; }
    }
    sealed class SimulatedEarnedVerifier : IRiskRewardVerifier
    {
        public bool Verify(RiskEarnedReward receipt,RiskRewardRequest request) => receipt.Provider==Provider;
    }
    sealed class Case
    {
        public uint seed;
        public int slots;
        public bool simulated_ward_earned;
        public string outcome;
        public string error;
        public int terminal_round;
        public int terminal_anger;
        public int paid_orders;
        public int total_hits;
        public int accepted_breaks;
        public int frenzy_starts;
        public double forge_seconds;
        public int supplied_swords;
        public int merchant_swords;
        public int duplicated_swords;
        public int delivered_swords;
        public int retired_at_terminal_swords;
        public int terminal_credits;
        public int terminal_wards;
        public int sword_conservation_residual;
        public int credit_conservation_residual;
        public double gold_conservation_residual;
        public int ward_conservation_residual;
        public int traded_swords;
        public int credits_from_trades;
        public int credits_spent_on_relics;
        public int credits_from_tributes;
        public double gold_from_tributes;
        public double gold_from_forge_effects;
        public double gold_spent_at_merchant;
        public double gold_spent_on_rerolls;
        public double terminal_gold;
        public int ward_grants;
        public int wards_armed;
        public int ward_blocks;
        public int max_relics;
        public int max_live_swords;
        public int accepted_commands;
        public int store_writes;
        public List<string> final_relics=new List<string>();
        public Dictionary<string,int> relic_choices=new Dictionary<string,int>();
        public Dictionary<string,int> merchant_choices=new Dictionary<string,int>();
        public Dictionary<string,int> command_counts=new Dictionary<string,int>();
        public List<object> replacements=new List<object>();
        public List<object> rounds=new List<object>();
    }
    sealed class Actor
    {
        public RiskSession Session;
        public MemoryStore Store;
        public Case Result;
        int tradesThisRound;
        int merchantSwordBuys;
        int merchantItemBuys;
        bool securedCommerce;
        static void Count(Dictionary<string,int> values,string key) { if(!values.ContainsKey(key))values[key]=0;values[key]++; }
        void Accepted(bool ok,string action,string reason)
        {
            if(!ok)throw new InvalidOperationException(action+": "+reason);
            Result.accepted_commands++;Count(Result.command_counts,action);
            if(Result.accepted_commands>MaxCommands)throw new TimeoutException("accepted-command guard");
        }
        RiskState Read()
        {
            var value=Session.Snapshot();
            if(value.Run!=null)
            {
                Result.max_relics=Math.Max(Result.max_relics,value.Run.Relics.Count);
                Result.max_live_swords=Math.Max(Result.max_live_swords,value.Run.Swords.Count);
            }
            return value;
        }
        static double Value(RiskSword sword,RiskRun run) => RiskForgeRules.Value(sword,run.Context);
        static double Total(RiskRun run) => RiskEconomy.Money(run.ScrapValue+run.Swords.Sum(x=>Value(x,run)));
        static int Items(RiskRun run,string id) => run.Consumables.Find(x=>x.Id==id)?.Count??0;
        static RiskSword Effective(RiskSword sword,RiskRun run)
        {
            var copy=RiskForgeRules.Clone(sword);
            RiskForgeRules.Prime(copy,RiskForgeRules.Clone(run.Context));
            return copy; // Read-only preview; never written to Session or store.
        }
        static bool Safe(RiskSword sword) => sword.FrenzyActive&&sword.FrenzyRemaining>1e-9 || sword.Charge>=10;
        static int Score(string id)
        {
            switch(id)
            {
                case "lifecontract":return 140;case "perfectcore":return 125;case "everwick":return 120;
                case "repair":return 115;case "spine":return 110;case "cornerstone":return 105;case "hourglass":return 105;
                case "gear":return 100;case "tongs":return 100;case "watch":return 98;case "supply":return 95;
                case "insurance":return 95;case "dawnsheath":return 95;case "dragonpardon":return 94;
                case "headless":return 93;case "seal":return 90;case "redglass":return 90;case "holy":return 90;
                case "badge":return 88;case "hearth":return 88;case "locket":return 87;case "rage":return 85;
                case "phoenix":return 85;case "letter":return 84;case "nail":return 82;case "coin":return 80;
                case "hold":return 78;case "mold":return 76;case "brace":return 75;case "coal":return 75;
                case "shard":return 74;case "spark":return 72;case "testament":return 72;case "collector":return 70;
                case "royalchange":return 68;case "voucher":return 65;case "silverpermit":return 55;case "ghostledger":return 50;
                default:return 45;
            }
        }
        sealed class Upgrade { public string Id;public string Replace;public int Price;public double Rank; }
        Upgrade ChooseUpgrade(RiskState state,bool affordable)
        {
            var run=state.Run;Upgrade best=null;
            string worst=run.Relics.OrderBy(Score).ThenBy(x=>x,StringComparer.Ordinal).FirstOrDefault();
            bool full=run.Relics.Count>=state.Profile.UnlockedRelicSlots;
            foreach(string id in run.ShopOffers)
            {
                int price=run.StarterGift?0:RiskRelicData.Find(id).Price;
                int improvement=Score(id)-(full?Score(worst):0);
                if(!run.StarterGift&&full&&improvement<12 || affordable&&price>run.SwordCredits)continue;
                double rank=run.StarterGift?Score(id):improvement/(double)Math.Max(1,price);
                if(best==null||rank>best.Rank||rank==best.Rank&&string.CompareOrdinal(id,best.Id)<0)
                    best=new Upgrade {Id=id,Replace=full?worst:null,Price=price,Rank=rank};
            }
            return best;
        }
        void BuyAvailableRelics()
        {
            for(int limit=0;limit<3;limit++)
            {
                var state=Read();var choice=ChooseUpgrade(state,true);if(choice==null)return;
                Accepted(Session.TryBuyRelic(choice.Id,choice.Replace,state.Run.Round,out string reason),"buy-relic",reason);
                Result.credits_spent_on_relics+=choice.Price;Count(Result.relic_choices,choice.Id);
                if(choice.Replace!=null)Result.replacements.Add(new {round=state.Run.Round,from=choice.Replace,to=choice.Id});
            }
        }
        void GrantWardIfEligible()
        {
            var state=Read();var run=state.Run;
            if(!Result.simulated_ward_earned||run.RewardedWardGranted||!RiskEconomy.IsMerchantRound(run.Round))return;
            Accepted(Session.TryBeginReward(RiskRewardPlacement.Ward,out var request,out string reason),"begin-simulated-ward",reason);
            var receipt=new RiskEarnedReward {ReceiptId="probe-earned:"+request.Id,Provider=Provider,RequestId=request.Id,
                Placement=request.Placement,ProfileId=request.ProfileId,RunId=request.RunId,Round=request.Round,TargetSlot=request.TargetSlot};
            Accepted(Session.TryApplyEarnedReward(receipt,new SimulatedEarnedVerifier(),out reason),"commit-simulated-ward",reason);
            Result.ward_grants++;
        }
        bool BuyMerchant(string id,bool preserveReserve)
        {
            var state=Read();var run=state.Run;var offer=run.MerchantOffers.Find(x=>x.Id==id);
            if(offer==null||offer.Purchased>=offer.Stock)return false;
            double reserve=preserveReserve?run.BaseQuota*.5:0;
            if(run.Gold<offer.Price+reserve)return false;
            double price=offer.Price;
            Accepted(Session.TryBuyMerchant(id,run.Round,out string reason),"buy-merchant",reason);
            Result.gold_spent_at_merchant+=price;Count(Result.merchant_choices,id);
            if(offer.Kind=="sword") {merchantSwordBuys++;Result.merchant_swords++;} else merchantItemBuys++;
            return true;
        }
        void PrepareMerchant()
        {
            var state=Read();var run=state.Run;if(!RiskEconomy.IsMerchantRound(run.Round))return;
            if(run.Anger==2)BuyMerchant("oblivion",false);
            state=Read();run=state.Run;
            if(Total(run)<run.Quota&&merchantSwordBuys<2)BuyMerchant("basic-sword",true);
            state=Read();run=state.Run;
            if(Items(run,"ward")==0&&merchantItemBuys<2)BuyMerchant("ward",true);
            state=Read();run=state.Run;
            if(Items(run,"starshard")==0&&run.Swords.Any(x=>{var e=Effective(x,run);return !e.FrenzyActive&&e.Charge>=7&&e.Charge<10;})&&merchantItemBuys<2)
                BuyMerchant("starshard",true);
        }
        bool RescueMerchant()
        {
            var state=Read();var run=state.Run;
            if(!RiskEconomy.IsMerchantRound(run.Round)||merchantSwordBuys>=2)return false;
            var offers=run.MerchantOffers.Where(x=>x.Kind=="sword"&&x.Purchased<x.Stock&&x.Price<=run.Gold)
                .OrderBy(x=>x.Price/(x.Id=="special-sword"?640*Math.Pow(1.5,x.Grade):320)).ThenBy(x=>x.Id,StringComparer.Ordinal).ToList();
            return offers.Count>0&&BuyMerchant(offers[0].Id,false);
        }
        void SetTributeSelection()
        {
            var state=Read();var run=state.Run;double needed=Math.Max(0,run.Quota-run.ScrapValue);
            var selected=new List<RiskSword>();
            var single=run.Swords.Where(x=>Value(x,run)>=needed).OrderBy(x=>Value(x,run)).ThenBy(x=>x.Charge).ThenBy(x=>x.Id).FirstOrDefault();
            if(needed>0&&single!=null)selected.Add(single);
            else if(needed>0)
            {
                double sum=0;
                foreach(var sword in run.Swords.OrderByDescending(x=>Value(x,run)).ThenBy(x=>x.Charge).ThenBy(x=>x.Id))
                { selected.Add(sword);sum+=Value(sword,run);if(sum>=needed)break; }
                foreach(var sword in selected.OrderBy(x=>Value(x,run)).ToList())
                    if(sum-Value(sword,run)>=needed) {selected.Remove(sword);sum-=Value(sword,run);}
            }
            var ids=new HashSet<int>(selected.Select(x=>x.Id));
            foreach(var sword in run.Swords)
                if(sword.InTribute!=ids.Contains(sword.Id))
                    Accepted(Session.TryMarkForOrder(sword.Id,ids.Contains(sword.Id),out string reason),"mark-order",reason);
        }
        void TradeForVisibleUpgrade()
        {
            for(int guard=0;guard<2&&tradesThisRound<2;guard++)
            {
                var state=Read();var run=state.Run;
                if(Session.OrderValue<run.Quota)return;
                var choice=ChooseUpgrade(state,false);if(choice==null||choice.Price<=run.SwordCredits)return;
                var extras=run.Swords.Where(x=>!x.InTribute&&!x.Locked).OrderBy(x=>Value(x,run)).ThenBy(x=>x.Charge).ThenBy(x=>x.Id).ToList();
                if(extras.Count<1||Value(extras[0],run)>Math.Max(640,.15*run.Quota))return;
                int credits=RiskEconomy.SwordCreditsForTrade(extras[0],run.Context,run.AnyReroll,run.HookUsed);
                Accepted(Session.TrySellForCredit(extras[0].Id,out string reason),"trade-sword",reason);
                Result.traded_swords++;Result.credits_from_trades+=credits;tradesThisRound++;
                BuyAvailableRelics();
            }
        }
        void SecuredCommerce()
        {
            if(securedCommerce)return;securedCommerce=true;
            BuyAvailableRelics();TradeForVisibleUpgrade();BuyAvailableRelics();
            var state=Read();var run=state.Run;
            bool free=run.Relics.Contains("voucher")&&!run.VoucherUsed;
            if(run.Round>1&&!run.AnyReroll&&run.SwordCredits>=1&&ChooseUpgrade(state,true)==null&&
                (free||run.Gold>=Session.RerollPrice+.5*run.BaseQuota))
            {
                double price=free?0:Session.RerollPrice;
                Accepted(Session.TryReroll(run.Round,out string reason),"reroll",reason);
                Result.gold_spent_on_rerolls+=price;
                // Purchases are attempted again immediately on this new shelf, in the same round.
                BuyAvailableRelics();TradeForVisibleUpgrade();BuyAvailableRelics();
            }
        }
        RiskSword ChooseForgeSword(RiskRun run,bool onlySafe)
        {
            RiskSword best=null;double bestRisk=double.PositiveInfinity;
            foreach(var sword in run.Swords.Where(x=>!x.Locked&&!x.InTribute&&x.Grade<99))
            {
                var effective=Effective(sword,run);bool safe=Safe(effective);
                if(onlySafe&&!safe)continue;
                double risk=Session.GetProbabilities(sword.Id)[0]/1000.0;
                if(sword.Effects.WardArmed)risk=0;
                else if(run.Relics.Contains("cornerstone")&&sword.Effects.CornerstoneUses==0||run.Relics.Contains("insurance")&&!run.Context.InsuranceUsed||
                    run.Relics.Contains("spine")&&sword.Effects.SpineShield)risk*=.25;
                else if(run.Relics.Contains("phoenix")&&sword.Effects.PhoenixUses==0)risk*=.1;
                double cost=safe?-1e12-Value(sword,run):Value(sword,run)*risk/(1+.15*effective.Charge);
                if(best==null||cost<bestRisk||cost==bestRisk&&effective.Charge>Effective(best,run).Charge)
                {best=sword;bestRisk=cost;}
            }
            return best;
        }
        bool ForgeOne()
        {
            if(Result.total_hits>=MaxHits)throw new TimeoutException("accepted-hit guard");
            double wait=Session.CooldownRemaining;
            if(wait>0)Session.Advance(wait+1e-9,true);
            var state=Read();var run=state.Run;
            bool enough=Total(run)>=run.Quota;
            var sword=ChooseForgeSword(run,enough);
            if(sword==null)return false;
            if(run.SelectedId!=sword.Id)Accepted(Session.TrySelect(sword.Id,out string selectReason),"select",selectReason);
            var effective=Effective(sword,run);
            if(Items(run,"starshard")>0&&!effective.FrenzyActive&&effective.Charge>=7&&effective.Charge<10)
            {
                Accepted(Session.TryUseConsumable("starshard",out string reason),"use-starshard",reason);
                state=Read();run=state.Run;sword=run.Swords.Find(x=>x.Id==sword.Id);effective=Effective(sword,run);
            }
            double risk=Session.GetProbabilities(sword.Id)[0]/1000.0;
            if(Items(run,"ward")>0&&!Safe(effective)&&!sword.Effects.WardArmed&&risk>=.20&&Value(sword,run)>=Math.Max(640,.20*run.Quota))
                Accepted(Session.TryUseConsumable("ward",out string reason),"arm-ward",reason);
            if(Items(run,"chalk")>0&&Safe(effective)&&!sword.Effects.ChalkArmed)
                Accepted(Session.TryUseConsumable("chalk",out string reason),"use-chalk",reason);
            double beforeGold=run.Gold;int beforeIds=state.NextSwordId;
            Accepted(Session.TryStrike(out var hit,out string strikeReason),"strike",strikeReason);
            Result.total_hits++;if(hit.StartedFrenzy)Result.frenzy_starts++;
            if(hit.ProtectionId=="ward")Result.ward_blocks++;
            var after=Read();Result.duplicated_swords+=after.NextSwordId-beforeIds;
            Result.gold_from_forge_effects+=Math.Max(0,after.Run.Gold-beforeGold);
            if(hit.Fatal)
            {
                double priorGold=after.Run.Gold;
                Accepted(Session.TryAcceptBreak(after.Run.PendingBreak.EventId,out string reason),"accept-break",reason);
                Result.accepted_breaks++;
                Result.gold_from_forge_effects+=Math.Max(0,Read().Run.Gold-priorGold);
            }
            Result.wards_armed=Result.command_counts.ContainsKey("arm-ward")?Result.command_counts["arm-ward"]:0;
            return true;
        }
        void Submit()
        {
            var state=Read();var run=state.Run;SetTributeSelection();state=Read();run=state.Run;
            double value=Session.OrderValue;bool passed=value>=run.Quota;int ids=state.NextSwordId;
            bool collector=run.Relics.Contains("collector");int round=run.Round,anger=run.Anger;
            Result.final_relics=new List<string>(run.Relics);
            Accepted(Session.TrySubmitOrder(run.Id,round,out string reason),"submit-order",reason);
            var after=Read();int angerAfter=after.Run?.Anger??after.LastResult.Anger;
            Result.gold_from_tributes+=value;if(passed)Result.paid_orders++;
            Result.delivered_swords+=run.OrderIds.Count;
            if(after.Run==null)
            {
                Result.retired_at_terminal_swords=run.Swords.Count-run.OrderIds.Count;
                Result.terminal_credits=run.SwordCredits+(passed&&collector?1:0);
                Result.terminal_wards=Items(run,"ward");
            }
            if(passed&&collector)Result.credits_from_tributes++;
            Result.rounds.Add(new {round,quota=run.Quota,value,passed,anger_before=anger,anger_after=angerAfter,hits_total=Result.total_hits,
                breaks_total=Result.accepted_breaks,credit_before_submit=run.SwordCredits,live_swords_before=run.Swords.Count,offered_swords=run.OrderIds.Count,
                relics=run.Relics.ToArray()});
            if(after.Run!=null)Result.supplied_swords+=after.NextSwordId-ids;
            Result.terminal_round=round;Result.terminal_anger=angerAfter;
        }
        public void Play()
        {
            Accepted(Session.TryStart(out string reason),"start",reason);
            var first=Read();Result.supplied_swords=first.Run.Swords.Count;
            int currentRound=0;
            while(true)
            {
                var state=Read();var run=state.Run;
                if(run==null)
                {
                    Result.outcome=state.LastResult.ContractCleared?"clear":"failed";
                    Result.terminal_round=state.LastResult.Rounds;Result.terminal_anger=state.LastResult.Anger;
                    Result.terminal_gold=state.LastResult.Gold;Result.forge_seconds=state.LastResult.ActiveSeconds;
                    if(state.LastResult.TotalHits!=Result.total_hits||state.LastResult.BrokenSwords!=Result.accepted_breaks)
                        throw new InvalidOperationException("Probe/session accepted hit/loss counters disagree.");
                    break;
                }
                if(run.Status=="clear_offer")
                {
                    Result.final_relics=new List<string>(run.Relics);
                    Result.retired_at_terminal_swords=run.Swords.Count;
                    Result.terminal_credits=run.SwordCredits;Result.terminal_wards=Items(run,"ward");
                    Accepted(Session.TryEndContract(run.Id,out reason),"end-cleared-contract",reason);continue;
                }
                if(run.Round!=currentRound)
                {
                    currentRound=run.Round;tradesThisRound=merchantSwordBuys=merchantItemBuys=0;securedCommerce=false;
                    GrantWardIfEligible();BuyAvailableRelics();PrepareMerchant();BuyAvailableRelics();continue;
                }
                double total=Total(run);
                bool safeWork=run.Swords.Any(x=>!x.InTribute&&!x.Locked&&x.Grade<99&&Safe(Effective(x,run)));
                if(total>=run.Quota&&!safeWork)
                {
                    SetTributeSelection();SecuredCommerce();state=Read();run=state.Run;
                    if(Total(run)>=run.Quota) {Submit();continue;}
                }
                state=Read();run=state.Run;
                if(run.OrderIds.Count>0)Accepted(Session.TryMarkAllForOrder(false,out reason),"unmark-forge",reason);
                if(ForgeOne())continue;
                state=Read();run=state.Run;
                if(Total(run)>=run.Quota) {SetTributeSelection();SecuredCommerce();Submit();continue;}
                if(RescueMerchant())continue;
                Submit();
            }
            Result.store_writes=Store.Writes;
            Result.sword_conservation_residual=Result.supplied_swords+Result.merchant_swords+Result.duplicated_swords-
                Result.traded_swords-Result.accepted_breaks-Result.delivered_swords-Result.retired_at_terminal_swords;
            Result.credit_conservation_residual=Result.credits_from_trades+Result.credits_from_tributes-Result.credits_spent_on_relics-Result.terminal_credits;
            Result.gold_conservation_residual=Result.gold_from_tributes+Result.gold_from_forge_effects-Result.gold_spent_at_merchant-
                Result.gold_spent_on_rerolls-Result.terminal_gold;
            Result.ward_conservation_residual=Result.ward_grants+(Result.merchant_choices.ContainsKey("ward")?Result.merchant_choices["ward"]:0)-Result.wards_armed-Result.terminal_wards;
            if(Result.sword_conservation_residual!=0||Result.credit_conservation_residual!=0||Math.Abs(Result.gold_conservation_residual)>1||Result.ward_conservation_residual!=0)
                throw new InvalidOperationException("Probe resource accounting did not close.");
            if(Read().Profile.UnlockedRelicSlots!=Result.slots||Result.ward_grants>1)throw new InvalidOperationException("Probe entitlement cohort changed.");
        }
    }
    static Case RunCase(uint seed,int slots,bool ward)
    {
        var result=new Case {seed=seed,slots=slots,simulated_ward_earned=ward};var store=new MemoryStore();Actor actor=null;
        try
        {
            if(!RiskSession.TryOpen(store,seed,out var session,out string reason))throw new Exception(reason);
            // The only entitlement fixture: no contract exists yet, and no other field is changed.
            var initial=session.Snapshot();initial.Profile.UnlockedRelicSlots=slots;
            if(!store.TryWrite(RiskStateCodec.Encode(initial),out reason)||!RiskSession.TryOpen(store,seed,out session,out reason))throw new Exception(reason);
            actor=new Actor {Session=session,Store=store,Result=result};actor.Play();
        }
        catch(TimeoutException error) {result.outcome="censored";result.error=error.Message;result.store_writes=store.Writes;}
        catch(Exception error) {result.outcome="command_error";result.error=error.Message;result.store_writes=store.Writes;}
        if(actor!=null&&(result.outcome=="censored"||result.outcome=="command_error"))
        {
            var stopped=actor.Session.Snapshot();
            if(stopped.Run!=null)
            {
                result.terminal_round=stopped.Run.Round;result.terminal_anger=stopped.Run.Anger;
                result.terminal_gold=stopped.Run.Gold;result.forge_seconds=stopped.Run.ActiveSeconds;
                result.final_relics=new List<string>(stopped.Run.Relics);
            }
        }
        return result;
    }
    static Dictionary<string,string> Hashes()
    {
        var files=new List<string>();
        foreach(string folder in new[]{"Gameplay","Session","Rewards","Persistence"})files.AddRange(Directory.GetFiles("Assets/OneMoreBonkRebuild/"+folder,"*.cs"));
        files.AddRange(new[]{"Assets/Resources/Risk/rule-data.json","Tests/Headless/UnityDataShim.cs","Tests/Headless/BalanceProbe.cs","Tools/probe-balance.sh"});
        var output=new Dictionary<string,string>();
        foreach(string path in files.OrderBy(x=>x,StringComparer.Ordinal))using(var hash=SHA256.Create())output[path.Replace('\\','/')]=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
        return output;
    }
#if BALANCE_PROBE_STANDALONE
    public static int Main(string[] args)
    {
        try
        {
            if(args.Length!=3)throw new ArgumentException("Usage: balance.exe <resources-root> <output.json> <common-seeds>");
            UnityEngine.Resources.Root=args[0];int count=int.Parse(args[2]);if(count<1||count>1000)throw new ArgumentOutOfRangeException("common-seeds");
            var before=Hashes();
            string compileManifest="Tests/Headless/.build/balance-compile-sources.sha256";
            bool compiledMatches=File.Exists(compileManifest)&&File.ReadAllLines(compileManifest).All(line=>
            {
                int split=line.IndexOf("  ",StringComparison.Ordinal);if(split<1)return false;
                string path=line.Substring(split+2);return before.ContainsKey(path)&&before[path]==line.Substring(0,split);
            });
            if(!compiledMatches)throw new InvalidOperationException("Compiled source manifest is missing or differs from the current source files; rerun Tools/probe-balance.sh.");
            var clock=Stopwatch.StartNew();uint sequence=0xC0FFEE21u;var seeds=new List<uint>();
            for(int i=0;i<count;i++) {sequence=RiskEconomy.NextRandom(sequence);seeds.Add(sequence);}
            var cases=new List<Case>();
            foreach(int slots in new[]{2,4,8})foreach(bool ward in new[]{false,true})
            {
                foreach(uint seed in seeds)cases.Add(RunCase(seed,slots,ward));
                var cohort=cases.Where(x=>x.slots==slots&&x.simulated_ward_earned==ward).ToList();
                Console.Error.WriteLine(JsonConvert.SerializeObject(new {progress="cohort-finished",slots,ward,cases=cohort.Count,clear=cohort.Count(x=>x.outcome=="clear"),failed=cohort.Count(x=>x.outcome=="failed"),errors=cohort.Count(x=>x.outcome=="command_error"),elapsed_seconds=clock.Elapsed.TotalSeconds}));
            }
            var after=Hashes();bool stable=before.All(x=>after.ContainsKey(x.Key)&&after[x.Key]==x.Value)&&before.Count==after.Count;
            var cohorts=cases.GroupBy(x=>new {x.slots,x.simulated_ward_earned}).Select(group=>new {
                slots=group.Key.slots,ward=group.Key.simulated_ward_earned,attempts=group.Count(),clear=group.Count(x=>x.outcome=="clear"),
                failed=group.Count(x=>x.outcome=="failed"),censored=group.Count(x=>x.outcome=="censored"),command_errors=group.Count(x=>x.outcome=="command_error"),
                observed_policy_completion_fraction=group.Any(x=>x.outcome=="command_error"||x.outcome=="censored")?(double?)null:group.Count(x=>x.outcome=="clear")/(double)group.Count(),
                failure_rounds=group.Where(x=>x.outcome=="failed").GroupBy(x=>x.terminal_round).ToDictionary(x=>x.Key.ToString(),x=>x.Count()),
                mean_hits=group.Average(x=>x.total_hits),mean_supplied_swords=group.Average(x=>x.supplied_swords),mean_traded_swords=group.Average(x=>x.traded_swords),
                mean_credits_earned=group.Average(x=>x.credits_from_trades+x.credits_from_tributes),mean_credits_spent=group.Average(x=>x.credits_spent_on_relics),
                mean_merchant_spend=group.Average(x=>x.gold_spent_at_merchant),mean_reroll_spend=group.Average(x=>x.gold_spent_on_rerolls),
                mean_max_relics=group.Average(x=>x.max_relics),max_relics=group.Max(x=>x.max_relics),mean_ward_grants=group.Average(x=>x.ward_grants),
                mean_ward_arms=group.Average(x=>x.wards_armed),mean_ward_blocks=group.Average(x=>x.ward_blocks)
            }).ToArray();
            var paired=new List<object>();foreach(int slots in new[]{2,4,8})
            {
                var control=cases.Where(x=>x.slots==slots&&!x.simulated_ward_earned).ToDictionary(x=>x.seed);var treatment=cases.Where(x=>x.slots==slots&&x.simulated_ward_earned).ToDictionary(x=>x.seed);
                var valid=seeds.Where(s=>(control[s].outcome=="clear"||control[s].outcome=="failed")&&(treatment[s].outcome=="clear"||treatment[s].outcome=="failed")).ToList();
                paired.Add(new {comparison="ward-on-vs-off",slots,common_seeds=count,valid_natural_pairs=valid.Count,
                    changed_to_clear=valid.Count(s=>control[s].outcome=="failed"&&treatment[s].outcome=="clear"),
                    changed_from_clear=valid.Count(s=>control[s].outcome=="clear"&&treatment[s].outcome=="failed"),
                    mean_round_difference=valid.Count==0?(double?)null:valid.Average(s=>treatment[s].terminal_round-control[s].terminal_round)});
            }
            var slotPairs=new List<object>();foreach(bool ward in new[]{false,true})foreach(int larger in new[]{4,8})
            {
                var control=cases.Where(x=>x.slots==2&&x.simulated_ward_earned==ward).ToDictionary(x=>x.seed);
                var treatment=cases.Where(x=>x.slots==larger&&x.simulated_ward_earned==ward).ToDictionary(x=>x.seed);
                var valid=seeds.Where(s=>(control[s].outcome=="clear"||control[s].outcome=="failed")&&(treatment[s].outcome=="clear"||treatment[s].outcome=="failed")).ToList();
                slotPairs.Add(new {comparison="larger-slots-vs-two",ward,larger_slots=larger,common_seeds=count,valid_natural_pairs=valid.Count,
                    changed_to_clear=valid.Count(s=>control[s].outcome=="failed"&&treatment[s].outcome=="clear"),
                    changed_from_clear=valid.Count(s=>control[s].outcome=="clear"&&treatment[s].outcome=="failed"),
                    mean_round_difference=valid.Count==0?(double?)null:valid.Average(s=>treatment[s].terminal_round-control[s].terminal_round)});
            }
            bool errors=cases.Any(x=>x.outcome=="command_error");
            var report=new {schema_version="1.0",generated_utc=DateTime.UtcNow.ToString("o"),status=!stable?"STALE_SOURCE":errors?"COMMAND_ERRORS":"COMPLETE",source_stable_during_run=stable,
                common_seeds=seeds,elapsed_seconds=clock.Elapsed.TotalSeconds,fixture_changes=new[]{"Profile.UnlockedRelicSlots before TryStart only"},
                policy="Visible-state conservative v2: secure tribute first; use remaining safe/full-charge Frenzy; low expected capital at risk when short; at most two low-value spare trades, visible scored relics/replacements, one reroll then buy again, limited merchant purchases, manual legal ward use.",
                policy_limits=new[]{"One hand-authored strategy and this small common-seed sample are an exploratory command probe, not player win-rate or calibrated balance.","Slots4/8 are pre-unlocked entitlement cohorts, not natural rewarded-slot unlock behavior or its time cost.","Ward treatment simulates one verified-earned test callback at the first legal merchant visit, not an SDK/video/ad conversion test. Inventory wards are fungible; individual free-ward causality is not inferred.","Fresh profiles; original permanent unlocks earned only within this one contract. No carry-over unlock population is modeled.","No post-start injected money, probability, relics, levels, swords or skipped rounds; all accepted actions use Session commands and a memory SaveStore.","Exact cooldown pacing models continuous legal input and frozen menu time, without human reaction, watch time, device latency or native persistence failures.","Fixed relic scores/trade caps and greedy tribute/forge rules are imperfect. Failure may reflect this policy, not game impossibility.","The probe stops after the natural21-day ending; extension mastery and full population retention/monetization are untested.","Censored guards and command errors are separate outcomes and are never relabeled natural failures. No automatic balance changes are made."},
                pilot_note="A one-seed availability pilot was discarded before the official sample. It exposed an overly restrictive actor rule requiring an extra spare sword beyond secured tribute, which suppressed all trades. V2 permits trading the last unmarked low-value spare only while marked tribute still covers quota; no game rules changed.",
                compiled_source_manifest_matches=compiledMatches,
                guards=new {maximum_accepted_hits=MaxHits,maximum_accepted_commands=MaxCommands},cohorts,paired_ward_comparisons=paired,paired_slot_comparisons=slotPairs,cases,source_sha256=before};
            Directory.CreateDirectory(Path.GetDirectoryName(args[1]));File.WriteAllText(args[1],JsonConvert.SerializeObject(report,Formatting.Indented)+"\n");
            Console.WriteLine(JsonConvert.SerializeObject(new {status=report.status,attempts=cases.Count,clear=cases.Count(x=>x.outcome=="clear"),failed=cases.Count(x=>x.outcome=="failed"),censored=cases.Count(x=>x.outcome=="censored"),command_errors=cases.Count(x=>x.outcome=="command_error"),elapsed_seconds=clock.Elapsed.TotalSeconds,output=args[1]}));
            return stable&&!errors?0:1;
        }
        catch(Exception error) {Console.Error.WriteLine(error);return 2;}
    }
#endif
}
