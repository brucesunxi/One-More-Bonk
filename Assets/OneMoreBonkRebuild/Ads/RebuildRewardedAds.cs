using System;
using System.Threading;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine;
using OneMoreBonk.Rebuild.Presentation;

namespace OneMoreBonk.Rebuild.Ads
{
    /// <summary>Official Google test inventory. Client earned-callback evidence, not SSV.</summary>
    [DisallowMultipleComponent]
    public sealed class RebuildRewardedAds : MonoBehaviour, IRiskRewardVerifier
    {
        public const string Provider = "GoogleMobileAdsOfficialTest-Rebuild";
        public const string AndroidTestAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string AndroidTestRewardedId = "ca-app-pub-3940256099942544/5224354917";
        const float LoadingTimeout = 30;
        const float InitializationTimeout = 35; // Android initialization can itself wait 30 seconds.
        static RebuildRewardedAds instance;
        static bool initialized, initializing;
        sealed class Flight
        {
            public RiskSession Session;
            public RiskRewardRequest Request;
            public RiskEarnedReward Receipt;
            public bool EarnedSignal, ReceiptReady, Closed, ShowStarted, Attempted;
            public float WaitSeconds;
            public string Failure;
            public string Phase = "begin";
        }
        readonly object gate = new object();
        RebuildWorkshop workshop;
        Flight flight;
        RewardedAd ad;
        int mainThread, generation;
        bool focused = true, appPaused;
        public string Status { get; private set; } = "Google官方测试广告（无生产收益）";
        public bool HasPendingEarned => flight != null && flight.ReceiptReady;
        public bool Busy => flight != null;
        public bool Supported => Application.platform == RuntimePlatform.Android;

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
            mainThread = Thread.CurrentThread.ManagedThreadId;
            Trace("awake", null, "official-test-adapter-created");
        }
        void Start()
        {
            Bind(GetComponent<RebuildWorkshop>() ?? FindFirstObjectByType<RebuildWorkshop>());
        }
        public void Bind(RebuildWorkshop target)
        {
            MainThread();
            Unbind(); workshop = target;
            if (workshop == null) return;
            workshop.RewardRequested += Request;
            workshop.RewardRetryRequested += RetryEarned;
            workshop.RewardCancelRequested += Cancel;
            workshop.RewardProviderReady = Supported;
            workshop.RefreshFromSession(Supported ? Status : "Google官方测试广告仅在Android设备启用，编辑器不发模拟奖励");
        }
        void Unbind()
        {
            if (workshop == null) return;
            workshop.RewardRequested -= Request;
            workshop.RewardRetryRequested -= RetryEarned;
            workshop.RewardCancelRequested -= Cancel;
            workshop.RewardProviderReady = false;
            workshop.RewardAwaitingSave = false;
        }
        void MainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThread)
                throw new InvalidOperationException("Reward commands must run on Unity's main thread.");
        }
        void Notice(string message, bool showing = false)
        {
            Status = message;
            Trace("notice", flight?.Request.Placement, "phase=" + (flight?.Phase ?? "idle") + "; fullscreen=" + showing + "; earned-awaiting-save=" + HasPendingEarned);
            if (workshop != null)
            {
                workshop.RewardAwaitingSave = HasPendingEarned;
                workshop.SetRewardPresentation(showing, message);
            }
        }
        public void Request(string placement)
        {
            MainThread();
            if (!Supported) { Notice("Google官方测试广告仅在Android设备启用"); return; }
            if (flight != null) { Notice(HasPendingEarned ? "已获奖励待保存，请重试保存，无需重看" : "已有一条广告请求，请等待或取消加载", flight.ShowStarted && !flight.Closed); return; }
            var session = workshop?.Session;
            if (session == null) { Notice("游戏存档尚未打开"); return; }
            if (!session.TryBeginReward(placement, out var request, out string reason)) { Notice(reason); return; }
            flight = new Flight { Session = session, Request = request };
            workshop.RewardProviderReady = false;
            Notice("正在加载Google官方测试广告，可在展示前取消", false);
            int token = ++generation;
            if (initialized) Load(flight, token);
            else Initialize(flight, token);
        }
        bool Current(Flight value, int token) => this != null && flight == value && token == generation;
        void Initialize(Flight value, int token)
        {
            value.Phase = "initializing"; value.WaitSeconds = 0;
            Trace("initialize-begin", value.Request.Placement, initializing ? "waiting-for-existing-initialization" : "calling-sdk");
            if (initializing) return;
            initializing = true;
            try
            {
                MobileAds.Initialize(status =>
                {
                    Trace("initialize-callback-native", value.Request.Placement, status == null ? "null-status" : "status-present");
                    MobileAdsEventExecutor.ExecuteInUpdate(() =>
                    {
                        Trace("initialize-callback-main", flight?.Request.Placement, status == null ? "null-status" : "status-present");
                        initializing = false; initialized = status != null;
                        if (this == null) return;
                        // Initialization belongs to the SDK, even if its original request was cancelled.
                        var waiting = flight;
                        if (waiting == null || waiting.Closed || waiting.ShowStarted) return;
                        if (!initialized) { Fail(waiting, "测试广告初始化失败，未发奖励，可稍后重试"); return; }
                        Load(waiting, generation);
                    });
                });
                TraceExecutor(value.Request.Placement);
            }
            catch (Exception error) { initializing = false; Fail(value, "测试广告初始化异常：" + error.GetType().Name); }
        }
        void Load(Flight value, int token)
        {
            if (!Current(value, token)) return;
            value.Phase = "loading"; value.WaitSeconds = 0;
            Trace("load-begin", value.Request.Placement, "calling-sdk");
            try
            {
                RewardedAd.Load(AndroidTestRewardedId, new AdRequest(), (loaded, error) =>
                {
                    TraceLoadResult(value.Request.Placement, loaded != null, error);
                    MobileAdsEventExecutor.ExecuteInUpdate(() =>
                    {
                        Trace("load-callback-main", value.Request.Placement, Current(value, token) ? "current" : "stale");
                        if (!Current(value, token)) { loaded?.Destroy(); return; }
                        if (error != null || loaded == null)
                        {
                            loaded?.Destroy(); Fail(value, "测试广告暂不可用，未消耗解锁或龙鳞额度"); return;
                        }
                        DestroyAd(); ad = loaded;
                        // Focus loss never cancels a request or shows over a backgrounded app.
                        ShowWhenForeground(value, token);
                    });
                });
            }
            catch (Exception error) { Fail(value, "测试广告加载异常：" + error.GetType().Name); }
        }
        void ShowWhenForeground(Flight value, int token)
        {
            if (!Current(value, token) || value.ShowStarted || value.Closed || !focused || appPaused || ad == null) return;
            try
            {
                if (!ad.CanShowAd()) { Fail(value, "测试广告已失效，请稍后重试"); return; }
            }
            catch (Exception error) { Fail(value, "测试广告状态异常：" + error.GetType().Name); return; }
            value.ShowStarted = true; value.Phase = "showing";
            Trace("show-begin", value.Request.Placement, "calling-sdk");
            var shown = ad;
            shown.OnAdFullScreenContentClosed += () => Closed(value, token, null);
            shown.OnAdFullScreenContentFailed += error => Closed(value, token, "测试广告展示失败，未获奖励");
            Notice("正在展示Google官方测试广告", true);
            try
            {
                shown.Show(reward =>
                {
                    // Capture earned immediately; queued close/UI callbacks may be delivered later.
                    lock (gate) { if (value.EarnedSignal) return; value.EarnedSignal = true; }
                    Trace("earned-callback-native", value.Request.Placement, "signal-received");
                    MobileAdsEventExecutor.ExecuteInUpdate(() =>
                    {
                        if (!Current(value, token)) return;
                        var r = value.Request;
                        value.Receipt = new RiskEarnedReward { ReceiptId = "google-test:" + r.Id,
                            Provider = Provider, RequestId = r.Id, Placement = r.Placement,
                            ProfileId = r.ProfileId, RunId = r.RunId, Round = r.Round, TargetSlot = r.TargetSlot };
                        value.ReceiptReady = true;
                        Trace("earned-callback-main", value.Request.Placement, "receipt-ready");
                        Complete(value);
                    });
                });
            }
            catch (Exception error) { Closed(value, token, "测试广告展示异常：" + error.GetType().Name); }
        }
        void Closed(Flight value, int token, string failure)
        {
            Trace("close-callback-native", value.Request.Placement, failure == null ? "closed" : "show-failed");
            lock (gate) { value.Closed = true; value.Failure = failure; }
            MobileAdsEventExecutor.ExecuteInUpdate(() => { if (Current(value, token)) Complete(value); });
        }
        void Fail(Flight value, string message)
        {
            value.Closed = true; value.Failure = message;
            Complete(value);
        }
        void Complete(Flight value)
        {
            if (value == null || flight != value || value.Attempted || !focused || appPaused) return;
            bool earned, closed;
            lock (gate) { earned = value.EarnedSignal; closed = value.Closed; }
            if (!closed || (earned && !value.ReceiptReady)) return;
            value.Attempted = true;
            DestroyAd();
            if (value.ReceiptReady)
            {
                value.Phase = "saving-earned";
                bool saved = value.Session.TryApplyEarnedReward(value.Receipt, this, out string reason);
                Trace("earned-commit", value.Request.Placement, saved ? "saved" : "save-not-committed");
                if (saved) Finish(value, value.Request.Placement == RiskRewardPlacement.RelicSlot ?
                    "官方测试奖励已保存：永久开启下一遗物格" : "官方测试奖励已保存：龙鳞×1已入库，请手动使用");
                else Notice("已获测试奖励，尚未保存；请重试保存，无需重看。" + reason);
            }
            else CancelPending(value, value.Failure ?? "测试广告已关闭，未获奖励，未消耗额度");
        }
        bool CancelPending(Flight value, string message)
        {
            if (value.Session.PersistenceBlocked && !value.Session.TryCheckpoint(out string saveReason))
            { Notice("取消状态尚未保存，可稍后重试取消。" + saveReason); return false; }
            if (!value.Session.TryCancelReward(value.Request.Id, out string reason))
            { Notice("请求尚未关闭：" + reason); return false; }
            Finish(value, message); return true;
        }
        void Finish(Flight value, string message)
        {
            if (flight != value) return;
            Trace("finish", value.Request.Placement, "phase=" + value.Phase + "; earned=" + value.ReceiptReady);
            flight = null; generation++; DestroyAd();
            if (workshop != null) workshop.RewardProviderReady = Supported;
            Notice(message);
        }
        public void Cancel()
        {
            MainThread();
            var value = flight;
            if (value == null)
            {
                // A process restart can leave a saved request, but not fabricated earned evidence.
                var session = workshop?.Session;
                var request = session?.Snapshot().PendingReward;
                if (request == null) { Notice("没有待取消请求"); return; }
                if (session.PersistenceBlocked && !session.TryCheckpoint(out string checkpoint)) { Notice(checkpoint); return; }
                if (session.TryCancelReward(request.Id, out string reason)) Notice("未获奖待请求已关闭，额度未消耗"); else Notice(reason);
                return;
            }
            lock (gate)
            {
                if (value.EarnedSignal) { Notice("已经获得奖励，请重试保存，无需重看"); return; }
            }
            if (value.ShowStarted && !value.Closed) { Notice("请先关闭系统广告；应用不会替您取消正在展示的广告", true); return; }
            if (!value.ShowStarted) { generation++; DestroyAd(); }
            value.Attempted = true;
            CancelPending(value, "测试广告请求已取消，未消耗解锁或龙鳞额度");
        }
        public void RetryEarned()
        {
            MainThread();
            var value = flight;
            if (value == null || !value.ReceiptReady)
            {
                bool earnedPending;
                lock (gate) { earnedPending = value != null && value.EarnedSignal; }
                if (earnedPending) { Notice("等待同一笔已获奖励回调，不会重新播放", value.ShowStarted && !value.Closed); return; }
                // The waiting-page save button also recovers a failed cancel/no-fill commit.
                Cancel(); return;
            }
            if (!value.Closed) { Notice("请先关闭系统广告", true); return; }
            if (value.Session.TryRetryEarnedReward(this, out string reason))
                Finish(value, "同一笔官方测试奖励已保存，无需重看");
            else Notice("已获奖励仍待保存，可继续重试。" + reason);
        }
        public bool Verify(RiskEarnedReward receipt, RiskRewardRequest request)
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThread || flight == null || !flight.ReceiptReady || receipt == null || request == null) return false;
            var expected = flight.Receipt;
            return receipt.Provider == Provider && receipt.ReceiptId == expected.ReceiptId &&
                receipt.RequestId == expected.RequestId && receipt.Placement == expected.Placement &&
                receipt.ProfileId == expected.ProfileId && receipt.RunId == expected.RunId &&
                receipt.Round == expected.Round && receipt.TargetSlot == expected.TargetSlot &&
                request.Id == flight.Request.Id && request.Placement == expected.Placement;
        }
        void Update()
        {
            if (flight == null || !focused || appPaused) return;
            if (!flight.ShowStarted && !flight.Closed)
            {
                flight.WaitSeconds += Time.unscaledDeltaTime;
                float limit = flight.Phase == "initializing" ? InitializationTimeout : LoadingTimeout;
                if (flight.WaitSeconds >= limit)
                {
                    Trace("timeout", flight.Request.Placement, "phase=" + flight.Phase + "; foreground-limit=" + limit);
                    generation++; Fail(flight, flight.Phase == "initializing" ? "测试广告初始化超时，未发奖励，可手动重试" : "测试广告加载超时，未发奖励，可手动重试"); return;
                }
                ShowWhenForeground(flight, generation);
            }
            Complete(flight);
        }
        void OnApplicationFocus(bool value) { focused = value; Trace("focus", flight?.Request.Placement, value ? "foreground" : "background"); }
        void OnApplicationPause(bool value) { appPaused = value; Trace("pause", flight?.Request.Placement, value ? "paused" : "resumed"); }
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        static void Trace(string stage, string placement, string status)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // No profile/run/request/receipt/device IDs, response info or save-error paths.
            Debug.Log("[RebuildAds] stage=" + stage + " placement=" + (placement ?? "none") + " status=" + status);
#endif
        }
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        static void TraceLoadResult(string placement, bool loaded, LoadAdError error)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            string detail = "loaded=" + loaded;
            if (error != null)
            {
                string message = (error.GetMessage() ?? "").Replace('\n', ' ').Replace('\r', ' ');
                message = System.Text.RegularExpressions.Regex.Replace(message,
                    @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}|[0-9a-fA-F]{32}|ca-app-pub-[0-9]+[~/][0-9]+", "[redacted]");
                if (message.Length > 500) message = message.Substring(0, 500);
                detail += "; code=" + error.GetCode() + "; message=" + message;
            }
            Trace("load-callback-native", placement, detail);
#endif
        }
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        static void TraceExecutor(string placement)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Trace("initialize-call-return", placement, "executor-active=" + MobileAdsEventExecutor.IsActive());
#endif
        }
        void DestroyAd()
        {
            if (ad == null) return; var old = ad; ad = null;
            try { old.Destroy(); } catch (Exception error) { Debug.LogWarning("Test ad cleanup failed: " + error.GetType().Name); }
        }
        void OnDestroy()
        {
            if (instance != this) return;
            instance = null; generation++; Unbind(); DestroyAd();
            // Teardown never fabricates close/earned or mutates a saved request.
        }
    }
}
