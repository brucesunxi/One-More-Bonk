using System;
using UnityEngine;

namespace OneMoreBonk.Rebuild.Presentation
{
    public sealed partial class RebuildWorkshop
    {
        void Home()
        {
            Panel(new Rect(28, 165, 664, Mathf.Min(730, height - 310)));
            Label(new Rect(62, 195, 596, 68), "One More Bonk", 46, ink, TextAnchor.MiddleCenter);
            var result = view.LastResult;
            if (result == null)
            {
                Label(new Rect(62, 300, 596, 208), "Forge an ordinary sword into a legend.\nEvery hit is a risk. Deliver orders to keep going.\nSurvive 21 orders, then finish or forge on.", 27, ink, TextAnchor.MiddleCenter);
            }
            else
            {
                string title = result.Kind == "complete" ? "Contract Complete" : result.Kind == "failed" ? "Your Contract Has Ended" : "Contract Closed";
                Label(new Rect(62, 284, 596, 58), title, 31, result.ContractCleared ? gold : ink, TextAnchor.MiddleCenter);
                Label(new Rect(72, 358, 576, 190), "Orders fulfilled: " + result.PassedOrders + " · Best +" + result.Best +
                    "\nHits: " + result.TotalHits + " · Swords broken: " + result.BrokenSwords +
                    "\nFrenzies: " + result.Frenzies + " · Gold left: " + Money(result.Gold), 25, ink, TextAnchor.MiddleCenter);
            }
            Label(new Rect(62, 570, 596, 70), "Relic slots: " + view.Profile.UnlockedRelicSlots + "/8 · Contracts cleared: " + view.Profile.Completed, 23, muted, TextAnchor.MiddleCenter);
            if (Button(new Rect(82, 675, 556, 98), "Start a New Contract · Free", !session.PersistenceBlocked, true))
            { page = Page.Forge; lastStrike = null; Execute(session.TryStart); }
        }

        void Body()
        {
            var run = view.Run;
            float contentHeight;
            switch (page)
            {
                case Page.Forge: contentHeight = 1400; break;
                case Page.Orders: contentHeight = 450 + run.Swords.Count * 154; break;
                case Page.Cabinet: contentHeight = 130 + run.Swords.Count * 218; break;
                case Page.Relics: contentHeight = RelicOffersBottom(); break;
                default: contentHeight = 320 + run.MerchantOffers.Count * 256; break;
            }
            Rect viewport = new Rect(16, 140, 688, height - 264);
            contentHeight = Mathf.Max(contentHeight, viewport.height);
            int index = (int)page;
            scroll[index] = GUI.BeginScrollView(viewport, scroll[index], new Rect(0, 0, 668, contentHeight), false, true);
            if (page != Page.Forge) Panel(new Rect(0, 0, 668, contentHeight));
            switch (page)
            {
                case Page.Forge: ForgePage(); break;
                case Page.Orders: OrdersPage(); break;
                case Page.Cabinet: CabinetPage(); break;
                case Page.Relics: RelicsPage(); break;
                case Page.Merchant: MerchantPage(); break;
            }
            GUI.EndScrollView();
        }

        void ForgePage()
        {
            var run = view.Run;
            var sword = Selected;
            // Short screens give up scene height, keeping the same type and touch sizes.
            float compact = Mathf.Clamp(1000 - (height - 264), 0, 120);
            Panel(new Rect(0, 0, 668, 174));
            Label(new Rect(22, 12, 624, 50), sword == null ? "Your Anvil Is Empty" : SwordName(sword), 29);
            Label(new Rect(22, 65, 624, 43), sword == null ? "Choose a sword from your cabinet." :
                "Value  " + Money(selectedValue) + " Gold   ·   Peak +" + sword.PeakGrade, 26);
            Label(new Rect(22, 116, 624, 48), "Order target: " + Money(run.Quota) + "   ·   Selected: " + Money(session.OrderValue), 22, muted);

            string[] outcomes = { "Break", "Damage", "Hold", "Success", "Great" };
            Color[] colors = { new Color(.88f, .30f, .29f), new Color(.85f, .48f, .19f), new Color(.23f, .65f, .54f),
                new Color(.22f, .57f, .83f), new Color(.62f, .39f, .78f) };
            bool hasNextHit = sword != null && sword.Grade < 99;
            string chanceTitle = sword == null ? "Draw Chances · No Sword Selected" : sword.Grade >= 99 ? "Max Grade · No Further Hits" :
                sword.Locked || sword.InTribute ? "Draw Chances · Forging Paused" : "Next Hit · Draw Chances";
            Label(new Rect(8, 188, 650, 36), chanceTitle, 23);
            for (int i = 0; i < 5; i++)
            {
                Rect r = new Rect(i * 134, 230, 128, 96); Panel(r);
                Label(new Rect(r.x + 4, r.y + 4, 120, 35), outcomes[i], 22, colors[i], TextAnchor.MiddleCenter);
                Label(new Rect(r.x + 2, r.y + 42, 124, 38), hasNextHit ? (probabilities[i] / 10.0).ToString("0.0") + "%" : "—", 25, colors[i], TextAnchor.MiddleCenter);
            }

            Sprite(new Rect(28, 445 - compact * .6f, 612, 195 - compact * .4f), "anvil-stone");
            float impactAge = Time.unscaledTime - lastImpactAt;
            float shake = impactAge < .18f ? Mathf.Sin(impactAge * 100) * (1 - impactAge / .18f) * 5 : 0;
            if (sword != null && previewTexture != null && previewWeapon != null)
                GUI.DrawTexture(new Rect(12 + shake, 338, 644, 285 - compact), previewTexture, ScaleMode.ScaleToFit, true);
            if (lastStrike != null && impactAge >= 0 && impactAge < .9f)
            {
                bool blocked = !lastStrike.Fatal && !string.IsNullOrEmpty(lastStrike.ProtectionId);
                Color color = blocked ? mint : colors[(int)lastStrike.Outcome];
                color.a = Mathf.Clamp01((.9f - impactAge) * 2);
                Label(new Rect(80, 372 - impactAge * 28, 508, 68), (blocked ? "Blocked!" : RiskPlayerText.OutcomeLabel(lastStrike.Outcome)) +
                    (lastStrike.Grade > lastStrike.PreviousGrade ? "  +" + (lastStrike.Grade - lastStrike.PreviousGrade) : ""), 40, color, TextAnchor.MiddleCenter);
            }
            if (!string.IsNullOrEmpty(viewerError)) Label(new Rect(28, 380, 612, 90), RiskPlayerText.Message(viewerError), 20, danger);
            if (sword == null)
                Label(new Rect(40, 365, 588, 110), "Each order has a limited sword supply.\nCheck your cabinet or the merchant.\nSubmit an order to move on if anger stays below 3.", 24, muted, TextAnchor.MiddleCenter);

            Rect dock = new Rect(0, 650 - compact, 668, 310);
            Sprite(dock, "dock-mint");
            DrawCharge(dock, sword?.Charge ?? 0, sword?.FrenzyActive ?? false);
            if (Button(new Rect(30, dock.y + 140, 172, 108), "Change\nSword", Active)) Go(Page.Cabinet);
            if (Button(new Rect(466, dock.y + 140, 172, 108), "To\nCabinet", Active)) Go(Page.Cabinet);
            Hammer(new Rect(229, dock.y + 54, 210, 210));
            Label(new Rect(203, 969 - compact, 264, 35), "Charge " + (sword?.Charge ?? 0) + "/10", 23, ink, TextAnchor.MiddleCenter);

            string stateText = sword == null ? "Choose a sword to begin." :
                sword.Locked ? "Locked: forging, items and trading are disabled." :
                sword.InTribute ? "Selected for delivery. Unselect it before forging." :
                sword.Grade >= 99 ? "+99 reached. This sword cannot be forged further." :
                sword.FrenzyActive ? "Frenzy · " + sword.FrenzyRemaining.ToString("0.0") + "s left" :
                sword.Charge >= 10 ? "Fully charged. Your next legal hit starts Frenzy." : "Tap for one hit. Hold to keep forging.";
            Label(new Rect(12, 1014 - compact, 644, 58), stateText, 21, muted, TextAnchor.MiddleCenter);
            string armed = sword == null ? "No sword selected" :
                "Dragon Scale: " + (sword.Effects.WardArmed ? "armed" : "—") + " · Chalk: " + (sword.Effects.ChalkArmed ? "armed" : "—") +
                " · Powder: " + (sword.Effects.PowderArmed ? "armed" : "—");
            Label(new Rect(0, 1084 - compact, 668, 54), armed, 20, sword?.Effects.WardArmed == true ? mint : muted, TextAnchor.MiddleCenter);

            string[] items = { "ward", "chalk", "powder", "starshard" };
            for (int i = 0; i < items.Length; i++)
            {
                string id = items[i]; int count = run.Consumables.Find(x => x.Id == id)?.Count ?? 0;
                if (Button(new Rect(i * 168, 1150 - compact, 160, 122), ItemName(id) + " ×" + count + "\nUse", Active && count > 0 && sword != null))
                    Execute((out string reason) => session.TryUseConsumable(id, out reason), ItemName(id) + " used");
            }
            string resultText = lastStrike == null ? "A fine sword is worth knowing when to stop." :
                "Last hit: " + (!lastStrike.Fatal && !string.IsNullOrEmpty(lastStrike.ProtectionId) ? "Blocked" : RiskPlayerText.OutcomeLabel(lastStrike.Outcome)) +
                "  +" + lastStrike.PreviousGrade + " → +" + lastStrike.Grade +
                (!string.IsNullOrEmpty(lastStrike.ProtectionId) ? "\nProtection: " + (lastStrike.ProtectionId == "ward" ? ItemName("ward") : RiskPlayerText.RelicName(lastStrike.ProtectionId)) : "");
            Label(new Rect(10, 1290 - compact, 648, 80), resultText, 22, ink, TextAnchor.MiddleCenter);
        }

        void Hammer(Rect rect)
        {
            bool ready = CanHammer && GUI.enabled;
            bool pressed = ready && hammerHeld && heldSwordId == (Selected?.Id ?? 0);
            Rect face = pressed ? new Rect(rect.x + 3, rect.y + 4, rect.width - 6, rect.height - 6) : rect;
            Color old = GUI.color; GUI.color = ready ? Color.white : new Color(1, 1, 1, .45f);
            Sprite(face, "button-hammer"); GUI.color = old;
            double remaining = session.CooldownRemaining;
            Label(new Rect(face.x, face.y + 58, face.width, 65), remaining > .01 ? remaining.ToString("0.0") + "s" : "BONK", 39, ink, TextAnchor.MiddleCenter);
            Label(new Rect(face.x + 12, face.y + 119, face.width - 24, 48), "Hold to repeat", 18, ink, TextAnchor.MiddleCenter);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && ready && rect.Contains(e.mousePosition))
            { hammerHeld = true; holdFired = false; heldSwordId = Selected.Id; pressStarted = Time.unscaledTime; e.Use(); }
            else if (e.type == EventType.MouseDrag && hammerHeld && !rect.Contains(e.mousePosition)) ClearPress();
            else if (e.type == EventType.MouseUp && e.button == 0 && hammerHeld)
            {
                bool tap = !holdFired && rect.Contains(e.mousePosition);
                ClearPress(); if (tap) Strike(); e.Use();
            }
        }

        void OrdersPage()
        {
            var run = view.Run;
            Panel(new Rect(0, 0, 668, 212));
            Label(new Rect(22, 10, 624, 48), "Current Order · Anger " + run.Anger + "/3", 30, run.Anger >= 2 ? danger : ink);
            Label(new Rect(22, 65, 624, 46), "Deliver " + Money(session.OrderValue) + " / Target " + Money(run.Quota), 30);
            Label(new Rect(22, 117, 624, 80), "Effective target: " + Exact(run.Quota) + " · Base: " + Exact(run.BaseQuota) +
                "\nSelected: " + run.OrderIds.Count + " swords · Scrap: " + Money(run.ScrapValue), 21, muted);
            for (int i = 0; i < run.Swords.Count; i++)
            {
                var sword = run.Swords[i]; int id = sword.Id; bool marked = sword.InTribute;
                float y = 230 + i * 154; Panel(new Rect(0, y, 668, 144));
                Label(new Rect(20, y + 12, 440, 42), SwordName(sword), 27);
                Label(new Rect(20, y + 63, 440, 55), "Value " + Exact(RiskForgeRules.Value(sword, run.Context)) +
                    (sword.Locked ? "\nLocked (frozen)" : ""), 21, muted);
                if (Button(new Rect(478, y + 26, 170, 92), sword.Locked ? "Locked" : marked ? "Unselect" : "Select", Active && !sword.Locked, marked))
                    Execute((out string reason) => session.TryMarkForOrder(id, !marked, out reason));
            }
            float bottom = 246 + run.Swords.Count * 154;
            if (Button(new Rect(0, bottom, 202, 82), "Select Unlocked", Active)) Execute((out string reason) => session.TryMarkAllForOrder(true, out reason));
            if (Button(new Rect(216, bottom, 182, 82), "Clear Selection", Active)) Execute((out string reason) => session.TryMarkAllForOrder(false, out reason));
            if (Button(new Rect(410, bottom, 258, 82), "Deliver Order", Active, true)) Show(Dialog.Submit);
            Label(new Rect(8, bottom + 94, 650, 88), "You may deliver below target, but anger rises. Delivery consumes the selected swords and advances the order. Locked swords stay unselected.", 21, muted);
        }

        void CabinetPage()
        {
            var run = view.Run;
            Label(new Rect(6, 0, 650, 65), "Cabinet · " + run.Swords.Count + (run.Swords.Count == 1 ? " sword" : " swords") + " · Locking freezes a sword", 27);
            if (run.Swords.Count == 0) Label(new Rect(20, 100, 628, 170), "Your cabinet is empty. Buy a sword when the merchant visits, or submit this order to receive the next supply.", 27, muted, TextAnchor.MiddleCenter);
            for (int i = 0; i < run.Swords.Count; i++)
            {
                var sword = run.Swords[i]; int id = sword.Id; bool locked = sword.Locked;
                float y = 80 + i * 218; Panel(new Rect(0, y, 668, 204));
                Label(new Rect(20, y + 10, 624, 42), (id == run.SelectedId ? "● " : "") + SwordName(sword), 28);
                Label(new Rect(20, y + 55, 624, 42), "Value " + Money(RiskForgeRules.Value(sword, run.Context)) + " · Charge " + sword.Charge + "/10" + (sword.InTribute ? " · Selected for delivery" : ""), 22, muted);
                if (Button(new Rect(16, y + 113, 202, 76), "To Anvil", Active && !locked && !sword.InTribute))
                { Execute((out string reason) => session.TrySelect(id, out reason)); Go(Page.Forge); }
                if (Button(new Rect(230, y + 113, 202, 76), locked ? "Unlock" : "Lock (Freeze)", Active))
                    Execute((out string reason) => session.TrySetLocked(id, !locked, out reason));
                if (Button(new Rect(446, y + 113, 202, 76), "Trade for Credits", Active && !locked && !sword.InTribute))
                { selectedDialogSword = id; Show(Dialog.Sell); }
            }
        }

        void RelicsPage()
        {
            var run = view.Run;
            Label(new Rect(6, 0, 650, 48), "Relic Shelf  " + run.Relics.Count + "/" + view.Profile.UnlockedRelicSlots + " · Max 8 slots", 29);
            for (int i = 0; i < 8; i++)
            {
                Rect r = new Rect((i % 4) * 168, 60 + (i / 4) * 168, 160, 156);
                if (i < run.Relics.Count)
                {
                    string id = run.Relics[i];
                    if (Button(r, (i + 1) + "\n" + RiskPlayerText.RelicName(id), Active, false, 22)) { selectedDialogRelic = id; Show(Dialog.RelicDetail); }
                }
                else if (i < view.Profile.UnlockedRelicSlots) { Panel(r); Label(r, "Empty Slot " + (i + 1), 23, muted, TextAnchor.MiddleCenter); }
                else if (Button(r, i == view.Profile.UnlockedRelicSlots ? "Slot " + (i + 1) + "\nWatch Ad\nPermanent" : "Slot " + (i + 1) + "\nLocked", Active && i == view.Profile.UnlockedRelicSlots, false, 22)) Show(Dialog.SlotReward);
            }
            Label(new Rect(4, 399, 400, 52), "Relic Offers", 29);
            bool free = run.Relics.Contains("voucher") && !run.VoucherUsed;
            if (Button(new Rect(414, 390, 254, 76), free ? "Free Reroll" : "Reroll · " + Money(session.RerollPrice), Active && run.Round > 1))
                Execute((out string reason) => session.TryReroll(run.Round, out reason));
            float offerY = 482;
            foreach (string id in run.ShopOffers)
            {
                float cardHeight = RelicOfferHeight(id, 668, false);
                OfferCard(new Rect(0, offerY, 668, cardHeight), id, false);
                offerY += cardHeight + 14;
            }
            if (run.ShopOffers.Count == 0) Label(new Rect(20, 495, 628, 80), "No offers left for this order. New offers arrive with the next order.", 24, muted);
        }

        float EnglishTextHeight(string text, int size, float width, float minimum = 0)
            => Mathf.Max(minimum, TextStyle(size, TextAnchor.UpperLeft).CalcHeight(new GUIContent(text), width) + 4);
        static string SwordCreditText(int amount) => amount + (amount == 1 ? " Sword Credit" : " Sword Credits");
        static string RelicOfferTitle(string id, bool gift)
            => RiskPlayerText.RelicName(id) + " · " + (gift ? "Starting Gift" : SwordCreditText(RiskRelicData.Find(id)?.Price ?? 0));
        float RelicOfferHeight(string id, float width, bool gift)
            => 140 + EnglishTextHeight(RelicOfferTitle(id, gift), 25, width - 36, 48) +
                EnglishTextHeight(RiskPlayerText.RelicDescription(id), 21, width - 40, 64);
        float RelicOffersBottom()
        {
            float bottom = 482;
            foreach (string id in view.Run.ShopOffers) bottom += RelicOfferHeight(id, 668, false) + 14;
            return Mathf.Max(560, bottom + 16);
        }

        void OfferCard(Rect r, string id, bool gift)
        {
            var entry = RiskRelicData.Find(id); if (entry == null) return;
            Panel(r);
            float titleHeight = EnglishTextHeight(RelicOfferTitle(id, gift), 25, r.width - 36, 48);
            Label(new Rect(r.x + 18, r.y + 12, r.width - 36, titleHeight), RelicOfferTitle(id, gift), 25);
            Label(new Rect(r.x + 20, r.y + titleHeight + 24, r.width - 40, r.height - titleHeight - 116), RiskPlayerText.RelicDescription(entry.Id), 21, muted);
            if (!gift && Button(new Rect(r.x + 16, r.yMax - 78, 150, 68), "Details"))
            { selectedDialogRelic = id; Show(Dialog.RelicDetail); }
            float buttonX = gift ? r.x + 16 : r.x + 176;
            float buttonWidth = gift ? r.width - 32 : r.width - 192;
            if (Button(new Rect(buttonX, r.yMax - 78, buttonWidth, 68), gift ? "Choose This Relic" : "Buy / Replace", !session.PersistenceBlocked && !adVisible, true))
            {
                if (view.Run.Relics.Count >= view.Profile.UnlockedRelicSlots)
                { selectedDialogRelic = id; Show(Dialog.Replace); }
                else Execute((out string reason) => session.TryBuyRelic(id, null, view.Run.Round, out reason), "Received " + RiskPlayerText.RelicName(entry.Id));
            }
        }

        void MerchantPage()
        {
            var run = view.Run;
            Label(new Rect(6, 0, 650, 52), "Traveling Merchant", 32);
            if (run.Round % 3 != 0 || run.MerchantOffers.Count == 0)
            {
                Panel(new Rect(0, 76, 668, 250));
                Label(new Rect(30, 102, 608, 180), "The merchant visits every 3 orders.\nNext visit: Order " + ((run.Round / 3 + 1) * 3), 31, muted, TextAnchor.MiddleCenter);
                return;
            }
            Panel(new Rect(0, 65, 668, 198));
            AtlasIcon(new Rect(20, 84, 88, 88), 10);
            Label(new Rect(122, 77, 522, 56), "Before You Forge · Dragon Scale ×1", 28);
            Label(new Rect(122, 131, 522, 49), run.RewardedWardGranted ? "Claimed this contract. Use it from your inventory." : "Once per contract. Separate from slot unlocks.", 20, muted);
            if (Button(new Rect(122, 185, 522, 66), run.RewardedWardGranted ? "Claimed" : RewardProviderReady && RewardRequested != null ? "Watch Ad to Claim" : "Ad Unavailable · Details", Active && !run.RewardedWardGranted, true)) Show(Dialog.WardReward);
            for (int i = 0; i < run.MerchantOffers.Count; i++)
            {
                var offer = run.MerchantOffers[i]; string id = offer.Id; int round = run.Round;
                float y = 285 + i * 256; Panel(new Rect(0, y, 668, 242));
                string title = offer.Kind == "sword" ? ItemName(id) + " · " + (offer.Family == "ordinary" ? "Ordinary" : FamilyName(offer.Family)) + " +" + offer.Grade : ItemName(id);
                Label(new Rect(20, y + 10, 628, 52), title, 27);
                Label(new Rect(20, y + 65, 628, 95), ItemDescription(id), 21, muted);
                Label(new Rect(20, y + 182, 365, 44), "Gold " + Money(offer.Price) + " · Left: " + (offer.Stock - offer.Purchased), 22);
                if (Button(new Rect(420, y + 166, 228, 68), "Buy", Active && offer.Purchased < offer.Stock && run.Gold >= offer.Price, true))
                    Execute((out string reason) => session.TryBuyMerchant(id, round, out reason), "Bought " + ItemName(id));
            }
        }
    }
}
