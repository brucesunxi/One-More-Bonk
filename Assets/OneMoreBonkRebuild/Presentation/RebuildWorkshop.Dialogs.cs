using System;
using UnityEngine;

namespace OneMoreBonk.Rebuild.Presentation
{
    public sealed partial class RebuildWorkshop
    {
        void DrawModal()
        {
            if (!SpecialModal && dialog == Dialog.None && !adVisible) return;
            ClearPress();
            bool oldEnabled = GUI.enabled; GUI.enabled = true;
            Solid(new Rect(0, 0, Width, height), new Color(.025f, .075f, .09f, .70f));
            Rect frame = new Rect(32, 54, 656, height - 170);
            Panel(frame);
            GUI.BeginGroup(frame);
            if (view.PendingReward != null || adVisible) RewardPending(frame.height);
            else if (view.Run?.PendingBreak != null) FatalDialog(frame.height);
            else if (view.Run?.Status == "clear_offer") ClearDialog(frame.height);
            else if (view.Run?.StarterGift == true) StarterDialog(frame.height);
            else StandardDialog(frame.height);
            GUI.EndGroup(); GUI.enabled = oldEnabled;
        }

        void ModalTitle(string title) => Label(new Rect(24, 12, 608, 76), title, 32, ink, TextAnchor.MiddleCenter);
        void CloseModal() { dialog = Dialog.None; skipClockFrame = true; ClearPress(); }
        bool ModalButton(float y, string text, bool primary = false) => Button(new Rect(28, y, 600, 82), text, !commandBusy, primary);

        void StarterDialog(float h)
        {
            ModalTitle("Choose Your Starting Relic");
            Label(new Rect(28, 85, 600, 58), "Pick one relic to begin this contract.", 22, muted, TextAnchor.MiddleCenter);
            string[] offers = view.Run.ShopOffers.ToArray();
            float contentHeight = 0;
            foreach (string id in offers) contentHeight += RelicOfferHeight(id, 594, true) + 16;
            modalScroll = GUI.BeginScrollView(new Rect(18, 154, 620, h - 174), modalScroll, new Rect(0, 0, 600, contentHeight));
            float y = 0;
            foreach (string id in offers)
            {
                float cardHeight = RelicOfferHeight(id, 594, true);
                OfferCard(new Rect(0, y, 594, cardHeight), id, true);
                y += cardHeight + 16;
            }
            GUI.EndScrollView();
        }

        void FatalDialog(float h)
        {
            var pending = view.Run.PendingBreak;
            long eventId = pending.EventId;
            ModalTitle("Your Sword Broke");
            Sprite(new Rect(50, 108, 556, 210), "universal-break");
            Label(new Rect(34, 330, 588, 146), SwordName(pending.PreSword) + "\nValue before the hit: " + Exact(pending.Result.ValueBefore) +
                "\nThis sword is lost. Choose another to keep forging.", 26, ink, TextAnchor.MiddleCenter);
            string effects = "Your relics may still grant rewards from this loss.";
            if (pending.Result.Procs != null)
                foreach (var proc in pending.Result.Procs) effects += "\n" + RiskPlayerText.Message(proc.Text);
            Label(new Rect(40, 490, 576, Mathf.Max(80, h - 680)), effects, 22, muted, TextAnchor.UpperCenter);
            if (ModalButton(h - 116, "Accept Loss & Continue", true)) Execute((out string reason) => session.TryAcceptBreak(eventId, out reason));
        }

        void ClearDialog(float h)
        {
            string id = view.Run.Id;
            ModalTitle("21-Order Contract Complete");
            Label(new Rect(40, 132, 576, 230), "You survived all 21 orders.\nOrders fulfilled: " + view.Run.PassCount + "\nBest +" + view.Run.Best +
                " · Hits: " + view.Run.TotalHits + "\nGold: " + Exact(view.Run.Gold), 31, ink, TextAnchor.MiddleCenter);
            Label(new Rect(42, 390, 572, 140), "Finish here and keep your discoveries and unlocked slots, or continue with harder orders.", 25, muted, TextAnchor.MiddleCenter);
            if (ModalButton(h - 220, "Continue Beyond 21", true)) { Execute((out string reason) => session.TryContinueExtended(id, out reason)); page = Page.Forge; }
            if (ModalButton(h - 118, "Finish Contract")) Execute((out string reason) => session.TryEndContract(id, out reason));
        }

        void RewardPending(float h)
        {
            var request = view.PendingReward;
            ModalTitle(adVisible ? "Ad in Progress" : "Reward Pending");
            string text = request == null ? "Waiting for the ad service." : request.Placement == RiskRewardPlacement.RelicSlot ?
                "Unlocking relic slot " + request.TargetSlot + "." : "Claiming 1 Dragon Scale for this contract.";
            string instructions = RewardAwaitingSave ? "\nYour reward has been earned. Retry saving it below." :
                "\nComplete the ad to earn your reward.\nClosing early or an unavailable ad does not use your claim.";
            Label(new Rect(40, 155, 576, 230), text + instructions, 28, ink, TextAnchor.MiddleCenter);
            Label(new Rect(42, 414, 572, 160), "If your reward was earned but not saved, retry saving. You do not need to watch again.", 24, muted, TextAnchor.MiddleCenter);
            if (!adVisible)
            {
                if (RewardAwaitingSave && ModalButton(h - 320, "Retry Saving Reward", true))
                {
                    if (RewardRetryRequested != null) RewardRetryRequested.Invoke();
                    else Notify("The ad service is unavailable. No reward can be verified.");
                }
                if (ModalButton(h - 220, "Refresh Status")) RefreshFromSession();
                if (!RewardAwaitingSave && request != null && ModalButton(h - 118, "Cancel Unearned Request"))
                {
                    if (RewardCancelRequested != null) RewardCancelRequested.Invoke();
                    else Execute((out string reason) => session.TryCancelReward(request.Id, out reason));
                }
            }
        }

        void StandardDialog(float h)
        {
            switch (dialog)
            {
                case Dialog.Submit: SubmitDialog(h); break;
                case Dialog.Sell: SellDialog(h); break;
                case Dialog.Discard: DiscardDialog(h); break;
                case Dialog.Replace: ReplaceDialog(h); break;
                case Dialog.RelicDetail: RelicDetail(h); break;
                case Dialog.SlotReward: SlotDialog(h); break;
                case Dialog.WardReward: WardDialog(h); break;
                case Dialog.Menu: MenuDialog(h); break;
                case Dialog.Codex: CodexDialog(h); break;
                case Dialog.End: EndDialog(h); break;
            }
        }

        void SubmitDialog(float h)
        {
            ModalTitle("Deliver Order " + capturedRound + "?");
            var run = view.Run;
            Label(new Rect(34, 128, 588, 280), "Swords to consume: " + run.OrderIds.Count + "\nDelivery value: " + Exact(session.OrderValue) +
                "\nEffective target: " + Exact(run.Quota) + "\n" + (session.OrderValue >= run.Quota ? "Target met" : "Below target: anger will rise") +
                "\nCurrent anger: " + run.Anger + "/3", 29, session.OrderValue >= run.Quota ? ink : danger, TextAnchor.MiddleCenter);
            Label(new Rect(40, 448, 576, 100), "Submitting consumes the selected swords and scrap.\nThis order settles immediately.", 24, muted, TextAnchor.MiddleCenter);
            if (ModalButton(h - 220, "Confirm Delivery", true)) Execute((out string reason) => session.TrySubmitOrder(capturedRunId, capturedRound, out reason));
            if (ModalButton(h - 118, "Back to Selection")) CloseModal();
        }

        void SellDialog(float h)
        {
            var sword = view.Run.Swords.Find(s => s.Id == selectedDialogSword);
            ModalTitle("Trade for Sword Credits");
            if (sword == null) { CloseModal(); return; }
            int credits = RiskEconomy.SwordCreditsForTrade(sword, view.Run.Context, view.Run.AnyReroll, view.Run.HookUsed);
            Label(new Rect(40, 165, 576, 290), SwordName(sword) + "\nValue: " + Exact(RiskForgeRules.Value(sword, view.Run.Context)) +
                "\nConsume this sword for " + credits + (credits == 1 ? " Sword Credit." : " Sword Credits.") + "\nThis trade does not grant gold.", 29, ink, TextAnchor.MiddleCenter);
            if (ModalButton(h - 220, "Trade for " + credits + (credits == 1 ? " Sword Credit" : " Sword Credits"), true)) Execute((out string reason) => session.TrySellForCredit(selectedDialogSword, out reason));
            if (ModalButton(h - 118, "Keep This Sword")) CloseModal();
        }

        void RelicDetail(float h)
        {
            var entry = RiskRelicData.Find(selectedDialogRelic);
            if (entry == null) { CloseModal(); return; }
            ModalTitle(RiskPlayerText.RelicName(entry.Id));
            string description = RiskPlayerText.RelicDescription(entry.Id);
            float descriptionHeight = EnglishTextHeight(description, 26, 568, h - 390);
            modalScroll = GUI.BeginScrollView(new Rect(28, 112, 600, h - 382), modalScroll, new Rect(0, 0, 574, descriptionHeight));
            Label(new Rect(4, 0, 568, descriptionHeight), description, 26, ink);
            GUI.EndScrollView();
            bool owned = view.Run?.Relics.Contains(entry.Id) == true;
            if (owned && ModalButton(h - 220, "Discard This Relic")) Show(Dialog.Discard);
            if (!owned && view.Run?.ShopOffers.Contains(entry.Id) == true && ModalButton(h - 220, "Buy · " + SwordCreditText(entry.Price), true))
            {
                if (view.Run.Relics.Count >= view.Profile.UnlockedRelicSlots) Show(Dialog.Replace);
                else Execute((out string reason) => session.TryBuyRelic(entry.Id, null, capturedRound, out reason));
            }
            if (ModalButton(h - 118, "Back")) CloseModal();
        }

        void DiscardDialog(float h)
        {
            ModalTitle("Discard This Relic?");
            Label(new Rect(40, 165, 576, 280), RiskPlayerText.RelicName(selectedDialogRelic) + "\nIts effects end immediately. Sword Credits are not refunded.\nTo buy a new relic, you can replace an old one directly from its offer.", 28, ink, TextAnchor.MiddleCenter);
            if (ModalButton(h - 220, "Discard Relic", true)) Execute((out string reason) => session.TryDiscardRelic(selectedDialogRelic, out reason));
            if (ModalButton(h - 118, "Keep Relic")) CloseModal();
        }

        void ReplaceDialog(float h)
        {
            string incoming = selectedDialogRelic; int round = capturedRound;
            var entry = RiskRelicData.Find(incoming);
            ModalTitle("Shelf Full · Choose a Replacement");
            Label(new Rect(32, 96, 592, 104), "Buy " + RiskPlayerText.RelicName(incoming) + " · " + SwordCreditText(entry?.Price ?? 0) + "\nChoose a relic to discard. No refund. No ad needed.", 24, ink, TextAnchor.MiddleCenter);
            modalScroll = GUI.BeginScrollView(new Rect(18, 218, 620, h - 352), modalScroll,
                new Rect(0, 0, 596, view.Run.Relics.Count * 102));
            string[] relics = view.Run.Relics.ToArray();
            for (int i = 0; i < relics.Length; i++)
            {
                string outgoing = relics[i];
                if (Button(new Rect(8, i * 102, 580, 90), "Replace " + RiskPlayerText.RelicName(outgoing), true, true))
                    Execute((out string reason) => session.TryBuyRelic(incoming, outgoing, round, out reason));
            }
            GUI.EndScrollView();
            if (ModalButton(h - 118, "Cancel · Keep Current Shelf")) CloseModal();
        }

        void SlotDialog(float h)
        {
            ModalTitle("Unlock Relic Slot " + (view.Profile.UnlockedRelicSlots + 1));
            Label(new Rect(40, 145, 576, 280), "Complete an ad to unlock the next slot.\nUnlocked slots stay on this device across contracts and restarts.\nSlots add space; relics are obtained separately.\nCurrently " + view.Profile.UnlockedRelicSlots + "/8 slots. Maximum: 8.", 28, ink, TextAnchor.MiddleCenter);
            RewardAvailability(new Rect(40, 450, 576, 130));
            if (ModalButton(h - 220, RewardProviderReady && RewardRequested != null ? "Watch Ad · Unlock Next Slot" : "Ad Unavailable · No Reward", true)) RequestReward(RiskRewardPlacement.RelicSlot);
            if (ModalButton(h - 118, "Keep Current Slots")) CloseModal();
        }

        void WardDialog(float h)
        {
            ModalTitle("Claim Dragon Scale ×1");
            Label(new Rect(40, 140, 576, 290), "Watch an ad for 1 Dragon Scale, once per contract.\nIt is added to your item inventory.\nArm it on the Forge page to block a Break on your next hit.\nAny result consumes the scale.", 28, ink, TextAnchor.MiddleCenter);
            RewardAvailability(new Rect(40, 450, 576, 130));
            if (ModalButton(h - 220, RewardProviderReady && RewardRequested != null ? "Watch Ad · Get Dragon Scale" : "Ad Unavailable · No Reward", true)) RequestReward(RiskRewardPlacement.Ward);
            if (ModalButton(h - 118, "Back to Merchant")) CloseModal();
        }

        void RewardAvailability(Rect r) => Label(r, RewardProviderReady && RewardRequested != null ?
            "This build uses official Google test ads.\nComplete an ad to earn the reward. If an ad is unavailable or closed early, you can keep playing." :
            "Ads are unavailable. Please try again later.\nYou can keep playing with your current slots and items.", 23, muted, TextAnchor.MiddleCenter);

        void MenuDialog(float h)
        {
            ModalTitle("Workshop Menu");
            Label(new Rect(40, 110, 576, 96), "Relic slots: " + view.Profile.UnlockedRelicSlots + "/8\nVersion " + Application.version + " · Local profile", 23, muted, TextAnchor.MiddleCenter);
            float contentHeight = view.Run == null ? 376 : 470;
            modalScroll = GUI.BeginScrollView(new Rect(18, 228, 620, h - 362), modalScroll, new Rect(0, 0, 594, contentHeight));
            if (Button(new Rect(8, 0, 580, 82), "Collection & Achievements", !commandBusy)) Show(Dialog.Codex);
            if (Button(new Rect(8, 94, 580, 82), SoundEnabled ? "Sound: On" : "Sound: Off", !commandBusy)) ToggleSound();
            if (Button(new Rect(8, 188, 580, 82), HapticsEnabled ? "Haptics: On" : "Haptics: Off", !commandBusy)) ToggleHaptics();
            if (Button(new Rect(8, 282, 580, 82), "Save & Return to Workshop", !commandBusy, true))
            { Execute(session.TryCheckpoint, "Saved"); }
            if (view.Run != null && Button(new Rect(8, 376, 580, 82), "End Current Contract", !commandBusy)) Show(Dialog.End);
            GUI.EndScrollView();
            if (ModalButton(h - 118, "Back to Game")) CloseModal();
        }

        void EndDialog(float h)
        {
            ModalTitle("End This Contract?");
            Label(new Rect(40, 150, 576, 250), "This ends the current orders, gold, swords and relics.\nUnlocked slots, discoveries and achievements stay.\nTo resume this contract later, go back to the game and close the app.", 28, ink, TextAnchor.MiddleCenter);
            if (ModalButton(h - 220, "End This Contract", true)) Execute((out string reason) => session.TryEndContract(capturedRunId, out reason));
            if (ModalButton(h - 118, "Back · Keep This Contract")) CloseModal();
        }

        void CodexDialog(float h)
        {
            ModalTitle("Collection & Achievements");
            var achievements = RiskProgression.Achievements();
            float contentHeight = 170 + achievements.Length * 114 + view.Profile.Discoveries.Count * 58;
            foreach (var entry in RiskRelicData.All)
            {
                if (entry.Disabled) continue;
                contentHeight += EnglishTextHeight(RiskPlayerText.RelicName(entry.Id), 25, 580, 42) +
                    EnglishTextHeight(RiskPlayerText.RelicDescription(entry.Id), 19, 580, 72) + 18;
            }
            modalScroll = GUI.BeginScrollView(new Rect(18, 94, 620, h - 225), modalScroll, new Rect(0, 0, 594, contentHeight));
            float y = 0;
            Label(new Rect(8, y, 580, 50), "Achievements: " + view.Profile.Achievements.Count + "/" + achievements.Length + " · Best +" + view.Profile.Best, 26); y += 58;
            foreach (var definition in achievements)
            {
                bool complete = view.Profile.Achievements.Contains(definition.Id);
                Label(new Rect(8, y, 580, 62), (complete ? "✓ " : "○ ") + AchievementText(definition), 22, complete ? mint : muted); y += 64;
                Label(new Rect(8, y, 580, 44), string.IsNullOrEmpty(definition.UnlockedRelicId) ? "" : "Unlocks in shop: " + RiskPlayerText.RelicName(definition.UnlockedRelicId), 19, muted); y += 50;
            }
            Label(new Rect(8, y, 580, 48), "Discovered Appearances", 26); y += 52;
            foreach (string id in view.Profile.Discoveries) { Label(new Rect(8, y, 580, 52), DiscoveryText(id), 22, muted); y += 58; }
            Label(new Rect(8, y, 580, 48), "Relic Catalogue", 26); y += 52;
            foreach (var entry in RiskRelicData.All)
            {
                if (entry.Disabled) continue;
                float titleHeight = EnglishTextHeight(RiskPlayerText.RelicName(entry.Id), 25, 580, 42);
                float descriptionHeight = EnglishTextHeight(RiskPlayerText.RelicDescription(entry.Id), 19, 580, 72);
                Label(new Rect(8, y, 580, titleHeight), RiskPlayerText.RelicName(entry.Id), 25); y += titleHeight + 4;
                Label(new Rect(8, y, 580, descriptionHeight), RiskPlayerText.RelicDescription(entry.Id), 19, muted); y += descriptionHeight + 14;
            }
            GUI.EndScrollView();
            if (ModalButton(h - 118, "Back to Game")) CloseModal();
        }

        static string AchievementText(RiskAchievementDefinition entry) => RiskPlayerText.AchievementDescription(entry.Id);

        static string DiscoveryText(string key)
        {
            if (string.IsNullOrEmpty(key)) return "Unknown appearance";
            string[] parts = key.Split(':');
            bool merchant = parts.Length >= 5 && parts[0] == "merchant";
            int stageOffset = merchant ? 2 : 1;
            if (parts.Length < stageOffset + 3 || !int.TryParse(parts[stageOffset], out int stage)) return "Discovered Appearances";
            string route = parts[stageOffset + 1] == "Sunfire" ? "Sunfire" : parts[stageOffset + 1] == "Moonsteel" ? "Moonsteel" : "Voidglass";
            return (merchant ? FamilyName(parts[1]) : "Forged Sword") + " · Form " + (stage + 1) + " · " + route +
                (parts[stageOffset + 2] == "awake" ? " · Awakened" : "");
        }
    }
}
