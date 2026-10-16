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
            InitializeFeedback();
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
            if (visible) SuspendFeedback();
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
                else Notify("Could not complete: " + RiskPlayerText.Message(reason));
                RefreshView();
            }
            catch (Exception error) { Notify("Could not complete: " + RiskPlayerText.Message(error.Message)); }
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
                    PlayStrikeFeedback(result);
                    if (result.Fatal) ClearPress();
                }
                else { ClearPress(); Notify("No strike: " + RiskPlayerText.Message(reason)); }
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
        void Notify(string message) { notice = RiskPlayerText.Message(message); noticeUntil = Time.unscaledTime + 7f; }
        static string PlayerDescription(string text) => RiskPlayerText.Message(text);

        void RequestReward(string placement)
        {
            ClearPress(); dialog = Dialog.None;
            if (!RewardProviderReady || RewardRequested == null)
            { Notify("Ad not ready. No reward granted. You can keep playing."); return; }
            try { RewardRequested.Invoke(placement); RefreshView(); }
            catch (Exception error) { Notify("Could not start ad: " + RiskPlayerText.Message(error.Message)); }
        }

        void OnApplicationFocus(bool value) { focused = value; skipClockFrame = true; ClearPress(); if (!value) { SuspendFeedback(); Checkpoint(); } }
        void OnApplicationPause(bool value) { appPaused = value; skipClockFrame = true; ClearPress(); if (value) { SuspendFeedback(); Checkpoint(); } }
        void OnApplicationQuit() { ClearPress(); Checkpoint(); }
        void OnDisable() { ClearPress(); SuspendFeedback(); Checkpoint(); }
        void Checkpoint()
        {
            if (session != null && !session.TryCheckpoint(out string reason)) Notify("Save needs a retry: " + RiskPlayerText.Message(reason));
        }

        void OnDestroy() { DisposeFeedback(); DisposeViewer(); }

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
                Label(new Rect(52, 184, 616, 60), "Workshop unavailable", 34);
                Label(new Rect(52, 260, 616, 205), RiskPlayerText.Message(openError ?? "Loading…"), 23, danger);
                if (Button(new Rect(72, 520, 576, 82), "Retry loading saved game", true, true)) OpenSession();
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
            Label(new Rect(34, 27, 205, 28), "ONE MORE", 24, ink);
            Label(new Rect(32, 52, 210, 56), "BONK", 43, ink);
            Label(new Rect(250, 23, 340, 38), view.Run == null ? "Workshop ready" :
                "Order " + view.Run.Round + (view.Run.Round > 21 ? " · Overtime" : " / 21"), 23);
            Label(new Rect(250, 67, 340, 38), view.Run == null ?
                "Relic slots " + view.Profile.UnlockedRelicSlots + "/8" :
                Money(view.Run.Gold) + " G   ·   " + view.Run.SwordCredits + " Credits", 21);
            if (Button(new Rect(599, 32, 88, 72), "Menu", dialog == Dialog.None && !SpecialModal && !adVisible && !commandBusy)) Show(Dialog.Menu);
        }

        void Footer()
        {
            string[] names = { "Forge", "Orders", "Cabinet", "Relics", "Shop" };
            Panel(new Rect(8, height - 110, 704, 100));
            for (int i = 0; i < names.Length; i++)
                if (Button(new Rect(16 + i * 139, height - 102, 132, 82), names[i], view.Run != null, page == (Page)i)) Go((Page)i);
        }

        void StorageBanner()
        {
            Panel(new Rect(16, 130, 688, 108));
            Label(new Rect(32, 138, 450, 88), "Save failed. Actions paused.\n" + RiskPlayerText.Message(session.LastSaveError), 19, danger);
            if (Button(new Rect(500, 145, 184, 76), "Retry save"))
            {
                if (view.PendingReward != null && RewardRetryRequested != null) RewardRetryRequested.Invoke();
                else if (session.TryCheckpoint(out string reason)) { RefreshView(); Notify("Game saved."); }
                else Notify(reason);
            }
        }

        static string Money(double value)
        {
            if (value >= 1e12) return value.ToString("0.##E+0", CultureInfo.InvariantCulture);
            if (value >= 1e9) return (value / 1e9).ToString("0.##", CultureInfo.InvariantCulture) + "B";
            if (value >= 1e6) return (value / 1e6).ToString("0.##", CultureInfo.InvariantCulture) + "M";
            if (value >= 1e4) return (value / 1e3).ToString("0.##", CultureInfo.InvariantCulture) + "K";
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }
        static string Exact(double value) => value.ToString("N0", CultureInfo.InvariantCulture);
        static string SwordName(RiskSword s) => FamilyName(s.SourceFamily) + " #" + s.Id + "  +" + s.Grade;
        static string FamilyName(string family) => RiskPlayerText.FamilyName(family);
        static string ItemName(string id)
        {
            switch (id)
            {
                case "ward": return "Dragon Scale"; case "chalk": return "Chalk"; case "powder": return "Forge Powder";
                case "starshard": return "Star Shard"; case "delivery": return "Sword Delivery";
                case "oblivion": return "Oblivion"; case "basic-sword": return "Steel Sword"; case "special-sword": return "Merchant Sword";
                default: return id;
            }
        }
        static string ItemDescription(string id)
        {
            switch (id)
            {
                case "ward": return "Arm before striking. Consumed on your next strike, whatever the outcome. Blocks a break.";
                case "chalk": return "Doubles Great's weight for the next strike. See the final normalized odds on the Forge page.";
                case "powder": return "Next normal strike draws only Break or Great: Damage/Hold become Break; Success becomes Great.";
                case "starshard": return "Adds 3 charge to the selected sword, up to 10.";
                case "delivery": return "On purchase: 2 extra swords supplied with the next order.";
                case "oblivion": return "On purchase: anger -1. Can be bought once per contract.";
                default: return "Added to your cabinet. Forge it or deliver it with an order.";
            }
        }
    }
}
