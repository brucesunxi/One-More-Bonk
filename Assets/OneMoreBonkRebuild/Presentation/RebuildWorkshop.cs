using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace OneMoreBonk.Rebuild.Presentation
{
    /// <summary>Independent mobile presentation. Session owns every accepted mutation.</summary>
    public sealed partial class RebuildWorkshop : MonoBehaviour
    {
        enum Page { Forge, Orders, Cabinet, Relics, Merchant }
        enum Dialog { None, Submit, Sell, Discard, Replace, RelicDetail, SlotReward, WardReward, Menu, Codex, End }
        delegate bool Command(out string reason);

        public event Action<string> RewardRequested;
        public event Action RewardRetryRequested;
        public event Action RewardCancelRequested;
        public RiskSession Session => session;
        public bool RewardProviderReady { get; set; }
        public bool RewardAwaitingSave { get; set; }

        RiskSession session;
        RiskState view;
        Page page;
        Dialog dialog;
        int selectedDialogSword;
        string selectedDialogRelic, capturedRunId;
        int capturedRound;
        Vector2[] scroll = new Vector2[5];
        Vector2 modalScroll;
        string notice, openError;
        float noticeUntil, nextRefresh;
        bool focused = true, appPaused, adVisible, commandBusy, skipClockFrame = true;
        bool hammerHeld, holdFired;
        float pressStarted;
        int heldSwordId;
        RiskStrikeResult lastStrike;
        float lastImpactAt = -100;
        int[] probabilities = new int[5];
        double selectedValue;
        const float Width = 720;
        float height = 1440;
        readonly Color ink = new Color(.055f, .17f, .23f);
        readonly Color mint = new Color(.20f, .63f, .60f);
        readonly Color muted = new Color(.34f, .43f, .46f);
        readonly Color gold = new Color(.85f, .57f, .13f);
        readonly Color danger = new Color(.68f, .20f, .15f);
        readonly Color cream = new Color(.98f, .97f, .91f);

        bool SpecialModal => view != null && (view.PendingReward != null || view.Run?.PendingBreak != null ||
            view.Run?.Status == "clear_offer" || view.Run?.StarterGift == true);
        bool Paused => !focused || appPaused || adVisible || dialog != Dialog.None || SpecialModal;
        bool Active => session != null && view?.Run != null && view.Run.Status == "active" &&
            !session.PersistenceBlocked && !Paused && !commandBusy;
        RiskSword Selected => view?.Run?.Swords.Find(s => s.Id == view.Run.SelectedId);
        bool CanHammer => Active && page == Page.Forge && Selected != null && !Selected.Locked &&
            !Selected.InTribute && Selected.Grade < 99;

        void Start()
        {
            Application.targetFrameRate = 60;
            LoadArt();
            CreateViewer();
            OpenSession();
        }

        void OpenSession()
        {
            try
            {
                uint seed = unchecked((uint)DateTime.UtcNow.Ticks);
                if (!RiskSession.TryOpen(new RiskFileSaveStore(Path.Combine(Application.persistentDataPath,
                    "minimal-v1/player.json")), seed, out session, out openError)) return;
                string recoveryNotice = openError;
                openError = null;
                RefreshView();
                if (!string.IsNullOrEmpty(recoveryNotice)) Notify(recoveryNotice);
            }
            catch (Exception error) { openError = error.Message; session = null; }
        }

        void Update()
        {
            if (session == null) return;
            bool ticking = page == Page.Forge && !Paused && !commandBusy;
            session.Advance(skipClockFrame ? 0 : Time.unscaledDeltaTime, ticking);
            skipClockFrame = false;
            if (!CanHammer || heldSwordId != (Selected?.Id ?? 0)) ClearPress();
            if (hammerHeld && Time.unscaledTime - pressStarted >= .36f && session.CooldownRemaining <= .00001)
            {
                holdFired = true;
                Strike();
            }
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + .10f;
                RefreshView();
            }
            UpdateViewer();
        }

        void RefreshView()
        {
            if (session == null) return;
            view = session.Snapshot();
            RiskSword sword = Selected;
            probabilities = sword == null ? new int[5] : session.GetProbabilities(sword.Id);
            selectedValue = sword == null ? 0 : RiskForgeRules.Value(sword, view.Run.Context);
        }

        public void RefreshFromSession(string message = null)
        {
            ClearPress();
            RefreshView();
            if (!string.IsNullOrEmpty(message)) Notify(message);
        }

        /// <summary>The SDK adapter must call this before showing and after closing its UI.</summary>
        public void SetRewardPresentation(bool visible, string message = null)
        {
            adVisible = visible;
            skipClockFrame = true;
            RefreshFromSession(message);
        }

        void Execute(Command command, string success = null, bool closeDialog = true)
        {
            if (session == null || commandBusy) return;
            commandBusy = true;
            ClearPress();
            try
            {
                if (command(out string reason))
                {
                    if (closeDialog) dialog = Dialog.None;
                    if (!string.IsNullOrEmpty(success)) Notify(success);
                }
                else Notify("操作未完成：" + reason);
                RefreshView();
            }
            catch (Exception error) { Notify("操作未完成：" + error.Message); }
            finally { commandBusy = false; }
        }

        void Strike()
        {
            if (!CanHammer || session.CooldownRemaining > .00001) return;
            commandBusy = true;
            try
            {
                if (session.TryStrike(out var result, out string reason))
                {
                    lastStrike = result;
                    lastImpactAt = Time.unscaledTime;
                    RefreshView();
                    if (result.Fatal) ClearPress();
                }
                else { ClearPress(); Notify("未锤击：" + reason); }
            }
            finally { commandBusy = false; }
        }

        void ClearPress() { hammerHeld = false; holdFired = false; heldSwordId = 0; }
        void Go(Page next)
        {
            ClearPress(); skipClockFrame = true;
            if (page == Page.Forge && next != Page.Forge) Checkpoint();
            dialog = Dialog.None; page = next; RefreshView();
        }
        void Show(Dialog next)
        {
            ClearPress(); skipClockFrame = true;
            if (page == Page.Forge && dialog == Dialog.None) Checkpoint();
            dialog = next; modalScroll = Vector2.zero;
            capturedRunId = view?.Run?.Id; capturedRound = view?.Run?.Round ?? 0;
        }
        void Notify(string message) { notice = message; noticeUntil = Time.unscaledTime + 7f; }
        static string PlayerDescription(string text) => (text ?? "").Replace("Frenzy", "入神").Replace("Success", "成功")
            .Replace("Great", "大成功").Replace("Damage", "损伤").Replace("Break", "碎裂").Replace("Hold", "不变");

        void RequestReward(string placement)
        {
            ClearPress(); dialog = Dialog.None;
            if (!RewardProviderReady || RewardRequested == null)
            { Notify("广告尚未就绪，未发放奖励。可继续游戏。"); return; }
            try { RewardRequested.Invoke(placement); RefreshView(); }
            catch (Exception error) { Notify("广告未启动：" + error.Message); }
        }

        void OnApplicationFocus(bool value) { focused = value; skipClockFrame = true; ClearPress(); if (!value) Checkpoint(); }
        void OnApplicationPause(bool value) { appPaused = value; skipClockFrame = true; ClearPress(); if (value) Checkpoint(); }
        void OnApplicationQuit() { ClearPress(); Checkpoint(); }
        void OnDisable() { ClearPress(); Checkpoint(); }
        void Checkpoint()
        {
            if (session != null && !session.TryCheckpoint(out string reason)) Notify("保存待重试：" + reason);
        }

        void OnDestroy() { DisposeViewer(); }

        void OnGUI()
        {
            if (font == null) LoadArt();
            InitStyles();
            Rect safe = Screen.safeArea;
            if (safe.width <= 1 || safe.height <= 1) safe = new Rect(0, 0, Screen.width, Screen.height);
            float canvasWidth = Mathf.Min(safe.width, safe.height * .70f);
            float scale = canvasWidth / Width;
            height = safe.height / scale;
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x + (safe.width - canvasWidth) / 2,
                Screen.height - safe.yMax, 0), Quaternion.identity, Vector3.one * scale);
            GUI.color = Color.white;
            Solid(new Rect(0, 0, Width, height), cream);
            DrawBackground();
            if (session == null)
            {
                Panel(new Rect(24, 160, 672, 470));
                Label(new Rect(52, 184, 616, 60), "工坊暂未打开", 34);
                Label(new Rect(52, 260, 616, 205), openError ?? "正在载入…", 23, danger);
                if (Button(new Rect(72, 520, 576, 82), "重试读取（保留原存档）", true, true)) OpenSession();
            }
            else
            {
                bool blocked = SpecialModal || dialog != Dialog.None || adVisible || commandBusy;
                Header();
                bool old = GUI.enabled;
                GUI.enabled = old && !blocked;
                if (view.Run == null) Home(); else Body();
                Footer();
                GUI.enabled = old;
                DrawModal();
                if (session.PersistenceBlocked) StorageBanner();
                if (Time.unscaledTime < noticeUntil && !string.IsNullOrEmpty(notice))
                {
                    float toastY = SpecialModal || dialog != Dialog.None || adVisible ? height - 108 : height - 224;
                    Panel(new Rect(28, toastY, 664, 100));
                    Label(new Rect(44, toastY + 10, 632, 82), notice, 20, ink, TextAnchor.MiddleCenter);
                }
            }
            if (Event.current.rawType == EventType.MouseUp) ClearPress();
            GUI.matrix = previousMatrix; GUI.color = previousColor;
        }

        void Header()
        {
            Panel(new Rect(16, 14, 688, 112));
            Sprite(new Rect(29, 28, 206, 66), "wordmark-reference");
            Label(new Rect(250, 23, 340, 38), view.Run == null ? "工坊就绪" :
                "第 " + view.Run.Round + (view.Run.Round > 21 ? " 单 · 延期" : " / 21 单"), 23);
            Label(new Rect(250, 67, 340, 38), view.Run == null ?
                "永久遗物槽 " + view.Profile.UnlockedRelicSlots + "/8" :
                "金币 " + Money(view.Run.Gold) + "   剑信用 " + view.Run.SwordCredits, 21);
            if (Button(new Rect(610, 32, 76, 72), "菜单", !SpecialModal && !adVisible)) Show(Dialog.Menu);
        }

        void Footer()
        {
            string[] names = { "锻造", "订单", "剑柜", "遗物", "商人" };
            Panel(new Rect(8, height - 110, 704, 100));
            for (int i = 0; i < names.Length; i++)
                if (Button(new Rect(16 + i * 139, height - 102, 132, 82), names[i], view.Run != null, page == (Page)i)) Go((Page)i);
        }

        void StorageBanner()
        {
            Panel(new Rect(16, 130, 688, 108));
            Label(new Rect(32, 138, 450, 88), "保存未完成，后续动作已暂停。\n" + session.LastSaveError, 19, danger);
            if (Button(new Rect(500, 145, 184, 76), "重试保存"))
            {
                if (view.PendingReward != null && RewardRetryRequested != null) RewardRetryRequested.Invoke();
                else if (session.TryCheckpoint(out string reason)) { RefreshView(); Notify("保存已恢复"); }
                else Notify(reason);
            }
        }

        static string Money(double value)
        {
            if (value >= 1e12) return value.ToString("0.##E+0", CultureInfo.InvariantCulture);
            if (value >= 1e8) return (value / 1e8).ToString("0.##", CultureInfo.InvariantCulture) + "亿";
            if (value >= 1e5) return (value / 1e4).ToString("0.##", CultureInfo.InvariantCulture) + "万";
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }
        static string Exact(double value) => value.ToString("N0", CultureInfo.InvariantCulture);
        static string SwordName(RiskSword s) => FamilyName(s.SourceFamily) + " #" + s.Id + "  +" + s.Grade;
        static string FamilyName(string family)
        {
            switch (family)
            {
                case "citadel": return "城堡剑"; case "holy": return "圣剑"; case "sawblade": return "锯刃";
                case "dragonfang": return "龙牙"; case "runeblade": return "符文剑"; case "splitblade": return "分刃";
                case "world-tree": return "世界树"; case "fishblade": return "海生剑"; case "funerary": return "葬仪剑";
                default: return family ?? "普通剑";
            }
        }
        static string ItemName(string id)
        {
            switch (id)
            {
                case "ward": return "龙鳞"; case "chalk": return "白垩"; case "powder": return "锻造粉";
                case "starshard": return "星之碎片"; case "delivery": return "供剑补给";
                case "oblivion": return "遗忘"; case "basic-sword": return "普通剑"; case "special-sword": return "商人剑";
                default: return id;
            }
        }
        static string ItemDescription(string id)
        {
            switch (id)
            {
                case "ward": return "锤前手动武装；下一锤任意结果均消耗，碎裂时保护剑。";
                case "chalk": return "下一锤大成功权重翻倍；归一化后的抽签概率见锻造页。";
                case "powder": return "下一次普通锤击仅抽碎裂/大成功：损伤、不变并入碎裂，成功并入大成功。";
                case "starshard": return "使用后当前剑充能增加3，最多10。";
                case "delivery": return "购买即生效：下一单额外供剑2把。";
                case "oblivion": return "购买即生效：愤怒减少1，本契约仅购一次。";
                default: return "购买后直接加入剑柜，规则与价值由会话计算。";
            }
        }
    }
}
