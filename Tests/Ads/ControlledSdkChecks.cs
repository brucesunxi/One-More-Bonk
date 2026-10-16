// Controlled event harness ONLY. These Unity/GMA/UI doubles must never enter Assets.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OneMoreBonk.Rebuild;
using OneMoreBonk.Rebuild.Ads;
using OneMoreBonk.Rebuild.Presentation;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
namespace UnityEngine
{
    public class Object { public static void Destroy(Object value) {} public static T FindFirstObjectByType<T>() where T:class => null; }
    public class MonoBehaviour:Object { public T GetComponent<T>() where T:class => null; }
    public class DisallowMultipleComponent:Attribute {}
    public enum RuntimePlatform { Android, OSXEditor }
    public static class Application { public static RuntimePlatform platform=RuntimePlatform.Android; }
    public static class Time { public static float unscaledDeltaTime=.02f; }
    public static class Debug { public static void LogWarning(object value) {} }
}
namespace GoogleMobileAds.Common
{
    public static class MobileAdsEventExecutor
    {
        public static readonly List<Action> Queue=new List<Action>();
        public static void ExecuteInUpdate(Action value) => Queue.Add(value);
        public static void Flush(bool reverse=false) { while(Queue.Count>0) { int i=reverse?Queue.Count-1:0;var a=Queue[i];Queue.RemoveAt(i);a(); } }
    }
}
namespace GoogleMobileAds.Api
{
    public sealed class InitializationStatus {}
    public sealed class LoadAdError {}
    public sealed class AdError {}
    public sealed class Reward {}
    public sealed class AdRequest {}
    public static class MobileAds { public static void Initialize(Action<InitializationStatus> callback) => callback(new InitializationStatus()); }
    public sealed class RewardedAd
    {
        public static Action<RewardedAd,LoadAdError> Loading;
        public static int Shows;
        public bool Destroyed,ThrowCanShow,ThrowShow,CanShow=true;
        public Action<Reward> Earn;
        public event Action OnAdFullScreenContentClosed;
        public event Action<AdError> OnAdFullScreenContentFailed;
        public static void Load(string id,AdRequest request,Action<RewardedAd,LoadAdError> callback) { Loading=callback; }
        public bool CanShowAd() { if(ThrowCanShow) throw new Exception("controlled state exception");return CanShow; }
        public void Show(Action<Reward> callback) { if(ThrowShow) throw new Exception("controlled show exception");Shows++;Earn=callback; }
        public void Destroy() { Destroyed=true; }
        public void EarnNow() => Earn?.Invoke(new Reward());
        public void Close() => OnAdFullScreenContentClosed?.Invoke();
        public void FailShow() => OnAdFullScreenContentFailed?.Invoke(new AdError());
    }
}
namespace OneMoreBonk.Rebuild.Presentation
{
    public sealed class RebuildWorkshop:UnityEngine.MonoBehaviour
    {
        public RiskSession Session;
        public event Action<string> RewardRequested;
        public event Action RewardRetryRequested,RewardCancelRequested;
        public bool RewardProviderReady,RewardAwaitingSave,Visible;
        public string Message;
        public void SetRewardPresentation(bool value,string message=null) { Visible=value;Message=message; }
        public void RefreshFromSession(string message=null) { Message=message; }
        public void Click(string placement)=>RewardRequested?.Invoke(placement);
        public void Retry()=>RewardRetryRequested?.Invoke();
        public void Cancel()=>RewardCancelRequested?.Invoke();
    }
}
public static class ControlledSdkChecks
{
    sealed class Store:IRiskSaveStore
    {
        public string Payload; public bool Fail;
        public bool TryRead(out string payload,out bool found,out string reason) {payload=Payload;found=Payload!=null;reason=null;return true;}
        public bool TryWrite(string payload,out string reason) {reason=Fail?"controlled disk full":null;if(Fail)return false;Payload=payload;return true;}
    }
    static int checks;
    static RebuildRewardedAds adapter; static RebuildWorkshop ui;static Store store;
    static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
    static object Call(string name,params object[] args)=>typeof(RebuildRewardedAds).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(adapter,args);
    static void Fresh(bool merchant=false)
    {
        if(adapter!=null)Call("OnDestroy");
        MobileAdsEventExecutor.Queue.Clear();RewardedAd.Loading=null;RewardedAd.Shows=0;
        typeof(RebuildRewardedAds).GetField("initialized",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,false);
        typeof(RebuildRewardedAds).GetField("initializing",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,false);
        store=new Store();Check(RiskSession.TryOpen(store,1234,out var session,out string reason),reason);Check(session.TryStart(out reason),reason);
        if(merchant)
        {
            var s=session.Snapshot();var r=s.Run;r.Round=r.Context.Round=3;r.BaseQuota=r.Quota=2600;r.StarterGift=false;r.ShopOffers.Clear();
            r.MerchantOffers=RiskEconomy.GenerateMerchantOffers(3,0,false,r.Context,ref r.ShopRngState);
            store.Payload=RiskStateCodec.Encode(s);Check(RiskSession.TryOpen(store,1234,out session,out reason),reason);
        }
        adapter=new RebuildRewardedAds();Call("Awake");ui=new RebuildWorkshop{Session=session};adapter.Bind(ui);
    }
    static RewardedAd Loaded(bool badCan=false,bool badShow=false)
    {
        MobileAdsEventExecutor.Flush();var ad=new RewardedAd{ThrowCanShow=badCan,ThrowShow=badShow};var callback=RewardedAd.Loading;
        Check(callback!=null,"No controlled SDK Load issued");callback(ad,null);MobileAdsEventExecutor.Flush();return ad;
    }
    static void NoGrant(string why)=>Check(ui.Session.Snapshot().Profile.UnlockedRelicSlots==2&&ui.Session.ItemCount("ward")==0,why);
    static void Cases()
    {
        Fresh();ui.Click(RiskRewardPlacement.RelicSlot);MobileAdsEventExecutor.Flush();var late=RewardedAd.Loading;
        Check(!ui.Visible&&!ui.RewardAwaitingSave&&ui.Session.Snapshot().PendingReward!=null,"loading must expose waiting-page cancellation, not a fullscreen overlay or earned retry");
        ui.Cancel();Check(ui.Session.Snapshot().PendingReward==null&&!adapter.Busy,"cancel before show");
        var stale=new RewardedAd();late(stale,null);MobileAdsEventExecutor.Flush();Check(stale.Destroyed,"stale loaded object not destroyed");NoGrant("cancel granted");

        Fresh();ui.Click(RiskRewardPlacement.RelicSlot);MobileAdsEventExecutor.Flush();RewardedAd.Loading(null,new LoadAdError());MobileAdsEventExecutor.Flush();
        Check(ui.Session.Snapshot().PendingReward==null&&!adapter.Busy,"no-fill remained frozen");NoGrant("no-fill granted");

        Fresh();ui.Click(RiskRewardPlacement.RelicSlot);var closed=Loaded();ui.Cancel();Check(adapter.Busy,"cancelled a showing system ad");
        closed.Close();MobileAdsEventExecutor.Flush();NoGrant("close alone granted");Check(!adapter.Busy,"closed no-earned request remained busy");
        closed.EarnNow();MobileAdsEventExecutor.Flush();NoGrant("late earned for cancelled/closed request granted");

        Fresh();ui.Click(RiskRewardPlacement.RelicSlot);var earned=Loaded();var before=ui.Session.Snapshot();
        earned.EarnNow();earned.EarnNow();earned.Close();MobileAdsEventExecutor.Flush(true);
        var after=ui.Session.Snapshot();Check(after.Profile.UnlockedRelicSlots==3&&after.RewardCommits.Count==1&&after.PendingReward==null,"earned/duplicate/reversed queued close failed");
        Check(before.Run.ResultRngState==after.Run.ResultRngState&&before.Run.ShopRngState==after.Run.ShopRngState&&before.Run.Gold==after.Run.Gold,"slot mutated economics/RNG");
        earned.EarnNow();earned.Close();MobileAdsEventExecutor.Flush();Check(ui.Session.Snapshot().Profile.UnlockedRelicSlots==3,"old callback double-granted");

        Fresh();ui.Click(RiskRewardPlacement.RelicSlot);var failedSave=Loaded();store.Fail=true;failedSave.EarnNow();failedSave.Close();MobileAdsEventExecutor.Flush();
        NoGrant("failed save published entitlement");Check(ui.RewardAwaitingSave&&!ui.Visible,"earned save failure must expose earned retry outside fullscreen");Check(adapter.HasPendingEarned&&ui.Session.Snapshot().PendingReward!=null,"lost earned evidence");ui.Cancel();Check(adapter.Busy,"cancel discarded earned");
        store.Fail=false;ui.Retry();Check(ui.Session.Snapshot().Profile.UnlockedRelicSlots==3&&!adapter.Busy&&RewardedAd.Shows==1,"save retry required another video or failed");
        Check(!ui.RewardAwaitingSave&&!ui.Visible,"successful earned retry did not reset presentation state");

        Fresh();ui.Click(RiskRewardPlacement.RelicSlot);var failedCancel=Loaded();store.Fail=true;failedCancel.Close();MobileAdsEventExecutor.Flush();
        Check(adapter.Busy&&ui.Session.PersistenceBlocked,"cancel write failure not held");
        Check(!ui.RewardAwaitingSave&&!ui.Visible,"unearned cancel save failure must expose cancellation, not earned retry");
        store.Fail=false;ui.Cancel();Check(!adapter.Busy&&ui.Session.Snapshot().PendingReward==null,"cancel button could not recover failed cancellation");
        Check(!ui.RewardAwaitingSave&&!ui.Visible,"successful cancellation did not reset presentation state");NoGrant("cancel retry granted");

        Fresh(true);ui.Click(RiskRewardPlacement.Ward);var ward=Loaded();var merchant=ui.Session.Snapshot();ward.EarnNow();ward.Close();MobileAdsEventExecutor.Flush();
        var reward=ui.Session.Snapshot();Check(ui.Session.ItemCount("ward")==1&&reward.Run.RewardedWardGranted&&reward.Profile.UnlockedRelicSlots==2,"ward mixed with permanent slots");
        Check(!reward.Run.Swords.Any(x=>x.Effects.WardArmed)&&reward.Run.MerchantOffers[0].Purchased==merchant.Run.MerchantOffers[0].Purchased,"ward auto-armed or counted as purchase");
        ui.Click(RiskRewardPlacement.Ward);Check(!adapter.Busy&&ui.Session.ItemCount("ward")==1,"second run ward allowed");

        Fresh();ui.Click(RiskRewardPlacement.RelicSlot);var paused=Loaded();Call("OnApplicationFocus",false);paused.EarnNow();paused.Close();MobileAdsEventExecutor.Flush();
        NoGrant("background committed before returning");Call("OnApplicationFocus",true);Call("Update");Check(ui.Session.Snapshot().Profile.UnlockedRelicSlots==3,"foreground lost earned");

        Fresh();ui.Click(RiskRewardPlacement.RelicSlot);Loaded(true);Check(!adapter.Busy,"CanShowAd exception stranded request");NoGrant("CanShowAd failure granted");
        Fresh();ui.Click(RiskRewardPlacement.RelicSlot);Loaded(false,true);Check(!adapter.Busy,"Show exception stranded request");NoGrant("Show failure granted");
        foreach(bool stateThrows in new[]{false,true})
        {
            Fresh();ui.Click(RiskRewardPlacement.RelicSlot);MobileAdsEventExecutor.Flush();Call("OnApplicationPause",true);
            var invalid=new RewardedAd{CanShow=false,ThrowCanShow=stateThrows};var callback=RewardedAd.Loading;
            Check(callback!=null,"background regression did not issue Load");callback(invalid,null);MobileAdsEventExecutor.Flush();
            Check(adapter.Busy&&RewardedAd.Shows==0&&ui.Session.Snapshot().PendingReward!=null,"background load showed or cancelled before returning");
            Call("OnApplicationPause",false);Call("Update");
            Check(!adapter.Busy&&ui.Session.Snapshot().PendingReward==null&&invalid.Destroyed&&ui.RewardProviderReady,"expired/throwing background ad did not safely cancel on return");
            NoGrant("expired/throwing background ad granted");
        }
        Fresh();ui.Click(RiskRewardPlacement.RelicSlot);MobileAdsEventExecutor.Flush();UnityEngine.Time.unscaledDeltaTime=31;Call("OnApplicationPause",true);Call("Update");Check(adapter.Busy,"background timed out request");
        Call("OnApplicationPause",false);Call("Update");Check(!adapter.Busy,"foreground loading timeout failed");NoGrant("timeout granted");UnityEngine.Time.unscaledDeltaTime=.02f;
    }
    public static int Main(string[] args)
    {
        try {UnityEngine.Resources.Root=args[0];Cases();Call("OnDestroy");Console.WriteLine("{\"status\":\"PASS\",\"assertions\":"+checks+",\"kind\":\"controlled Unity/GMA/UI event doubles; actual adapter+Session, not SDK playback\"}");return 0;}
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
