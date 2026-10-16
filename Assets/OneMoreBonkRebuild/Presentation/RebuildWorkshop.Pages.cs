using System;
using UnityEngine;

namespace OneMoreBonk.Rebuild.Presentation
{
    public sealed partial class RebuildWorkshop
    {
        void Home()
        {
            Panel(new Rect(28, 165, 664, Mathf.Min(730, height - 310)));
            Label(new Rect(62, 195, 596, 68), "再敲一锤", 46, ink, TextAnchor.MiddleCenter);
            var result = view.LastResult;
            if (result == null)
            {
                Label(new Rect(62, 300, 596, 208), "把普通剑锻成传奇。\n每一锤都有风险，交付订单继续前行。\n21单后可结束契约或继续延期。", 27, ink, TextAnchor.MiddleCenter);
            }
            else
            {
                string title = result.Kind == "complete" ? "契约完成" : result.Kind == "failed" ? "这次契约结束了" : "已结束契约";
                Label(new Rect(62, 284, 596, 58), title, 31, result.ContractCleared ? gold : ink, TextAnchor.MiddleCenter);
                Label(new Rect(72, 358, 576, 190), "完成订单 " + result.PassedOrders + " · 最高 +" + result.Best +
                    "\n累计锤击 " + result.TotalHits + " · 真实碎裂 " + result.BrokenSwords +
                    "\n入神 " + result.Frenzies + "次 · 结余金币 " + Money(result.Gold), 25, ink, TextAnchor.MiddleCenter);
            }
            Label(new Rect(62, 570, 596, 70), "永久遗物槽 " + view.Profile.UnlockedRelicSlots + "/8  ·  已完成契约 " + view.Profile.Completed, 23, muted, TextAnchor.MiddleCenter);
            if (Button(new Rect(82, 675, 556, 98), "免费开始新契约", !session.PersistenceBlocked, true))
            { page = Page.Forge; lastStrike = null; Execute(session.TryStart); }
        }

        void Body()
        {
            var run = view.Run;
            float contentHeight;
            switch (page)
            {
                case Page.Forge: contentHeight = 1280; break;
                case Page.Orders: contentHeight = 450 + run.Swords.Count * 154; break;
                case Page.Cabinet: contentHeight = 130 + run.Swords.Count * 218; break;
                case Page.Relics: contentHeight = 490 + run.ShopOffers.Count * 212; break;
                default: contentHeight = 300 + run.MerchantOffers.Count * 222; break;
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
            Panel(new Rect(0, 0, 668, 450));
            if (sword == null)
            {
                Label(new Rect(30, 30, 608, 90), "砧台上还没有剑", 34, ink, TextAnchor.MiddleCenter);
                Label(new Rect(36, 145, 596, 140), "本单供剑有限。可检查剑柜、商人，\n或交付当前订单进入下一单。", 26, muted, TextAnchor.MiddleCenter);
                if (Button(new Rect(144, 320, 380, 85), "查看剑柜")) Go(Page.Cabinet);
            }
            else
            {
                Label(new Rect(25, 12, 460, 52), SwordName(sword), 29);
                if (Button(new Rect(510, 12, 140, 64), "换剑")) Go(Page.Cabinet);
                Sprite(new Rect(40, 246, 588, 185), "anvil-stone");
                float impactAge = Time.unscaledTime - lastImpactAt;
                float shake = impactAge < .18f ? Mathf.Sin(impactAge * 100) * (1 - impactAge / .18f) * 5 : 0;
                if (previewTexture != null && previewWeapon != null)
                    GUI.DrawTexture(new Rect(20 + shake, 65, 628, 300), previewTexture, ScaleMode.ScaleToFit, true);
                if (lastStrike != null && impactAge >= 0 && impactAge < .9f)
                {
                    string[] names = { "碎裂", "损伤", "不变", "成功", "大成功！" };
                    Color color = lastStrike.Outcome == ForgeOutcome.Great ? gold : lastStrike.Outcome == ForgeOutcome.Success ? mint :
                        lastStrike.Outcome == ForgeOutcome.Damage ? danger : muted;
                    color.a = Mathf.Clamp01((.9f - impactAge) * 2);
                    Label(new Rect(100, 115 - impactAge * 28, 468, 68), names[(int)lastStrike.Outcome] +
                        (lastStrike.Grade > lastStrike.PreviousGrade ? "  +" + (lastStrike.Grade - lastStrike.PreviousGrade) : ""), 40, color, TextAnchor.MiddleCenter);
                }
                if (!string.IsNullOrEmpty(viewerError)) Label(new Rect(30, 150, 608, 90), viewerError, 20, danger);
                Label(new Rect(25, 65, 608, 32), "本单目标 " + Money(run.Quota) + "  ·  待交付 " + Money(session.OrderValue), 20, muted);
                Label(new Rect(28, 365, 610, 40), "当前价值  " + Exact(selectedValue), 25, ink, TextAnchor.MiddleCenter);
                string stateText = sword.Locked ? "已封存：冻结锤击、用道具和交易" : sword.InTribute ? "已选入订单，取消交付标记后才能锤击" : sword.Grade >= 99 ? "已达 +99，终值不再锤击" :
                    sword.FrenzyActive ? "入神中 · 剩余 " + sword.FrenzyRemaining.ToString("0.0") + "秒" : sword.Charge >= 10 ? "充能已满：下一次合法锤击进入入神" : "单击一锤 · 按住连续锤击";
                Label(new Rect(22, 410, 624, 36), stateText, 20, muted, TextAnchor.MiddleCenter);
            }

            string[] outcomes = { "碎裂", "损伤", "不变", "成功", "大成功" };
            Color[] colors = { danger, new Color(.75f, .39f, .10f), muted, mint, gold };
            Label(new Rect(8, 467, 650, 36), "下一锤结果概率", 23);
            for (int i = 0; i < 5; i++)
            {
                Rect r = new Rect(i * 134, 510, 128, 110); Panel(r);
                Label(new Rect(r.x + 4, r.y + 4, 120, 35), outcomes[i], 22, colors[i], TextAnchor.MiddleCenter);
                Label(new Rect(r.x + 2, r.y + 40, 124, 36), sword == null ? "—" : (probabilities[i] / 10.0).ToString("0.0") + "%", 25, colors[i], TextAnchor.MiddleCenter);
            }

            Rect dock = new Rect(0, 646, 668, 310);
            Sprite(dock, "dock-mint");
            DrawCharge(dock, sword?.Charge ?? 0, sword?.FrenzyActive ?? false);
            Hammer(new Rect(238, 741, 192, 192));
            Label(new Rect(203, 930, 264, 35), "充能 " + (sword?.Charge ?? 0) + "/10", 23, ink, TextAnchor.MiddleCenter);
            string armed = sword == null ? "请选择一把剑" :
                (sword.Effects.WardArmed ? "本锤防碎已武装" : "龙鳞未武装") + "  ·  白垩 " + (sword.Effects.ChalkArmed ? "已武装" : "—") +
                "  ·  锻造粉 " + (sword.Effects.PowderArmed ? "已武装" : "—");
            Label(new Rect(0, 978, 668, 50), armed, 20, sword?.Effects.WardArmed == true ? mint : muted, TextAnchor.MiddleCenter);

            string[] items = { "ward", "chalk", "powder", "starshard" };
            for (int i = 0; i < items.Length; i++)
            {
                string id = items[i]; int count = run.Consumables.Find(x => x.Id == id)?.Count ?? 0;
                if (Button(new Rect(i * 168, 1042, 160, 91), ItemName(id) + " ×" + count + "\n锤前使用", Active && count > 0 && sword != null))
                    Execute((out string reason) => session.TryUseConsumable(id, out reason), ItemName(id) + "已使用");
            }
            string resultText = lastStrike == null ? "敲出好剑，也记得见好就收。" :
                "上次：" + outcomes[(int)lastStrike.Outcome] + "  +" + lastStrike.PreviousGrade + " → +" + lastStrike.Grade +
                (!string.IsNullOrEmpty(lastStrike.ProtectionId) ? "\n保护触发：" + RiskRelicData.Name(lastStrike.ProtectionId) : "");
            Label(new Rect(10, 1158, 648, 98), resultText, 22, ink, TextAnchor.MiddleCenter);
        }

        void Hammer(Rect rect)
        {
            bool ready = CanHammer && GUI.enabled;
            Color old = GUI.color; GUI.color = ready ? Color.white : new Color(1, 1, 1, .45f);
            Sprite(rect, "button-hammer"); GUI.color = old;
            double remaining = session.CooldownRemaining;
            Label(rect, remaining > .01 ? remaining.ToString("0.0") + "秒" : "锤", 49, ink, TextAnchor.MiddleCenter);
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
            Label(new Rect(22, 10, 624, 48), "当前订单  ·  愤怒 " + run.Anger + "/3", 30, run.Anger >= 2 ? danger : ink);
            Label(new Rect(22, 65, 624, 46), "交付 " + Money(session.OrderValue) + " / 目标 " + Money(run.Quota), 30);
            Label(new Rect(22, 117, 624, 80), "实际目标 " + Exact(run.Quota) + "  ·  基础目标 " + Exact(run.BaseQuota) +
                "\n已选 " + run.OrderIds.Count + "把；碎料贡献 " + Money(run.ScrapValue), 21, muted);
            for (int i = 0; i < run.Swords.Count; i++)
            {
                var sword = run.Swords[i]; int id = sword.Id; bool marked = sword.InTribute;
                float y = 230 + i * 154; Panel(new Rect(0, y, 668, 144));
                Label(new Rect(20, y + 12, 440, 42), SwordName(sword), 27);
                Label(new Rect(20, y + 63, 440, 55), "价值 " + Exact(RiskForgeRules.Value(sword, run.Context)) +
                    (sword.Locked ? "\n已封存（冻结）" : ""), 21, muted);
                if (Button(new Rect(478, y + 26, 170, 92), sword.Locked ? "已封存" : marked ? "取消交付" : "选入订单", Active && !sword.Locked, marked))
                    Execute((out string reason) => session.TryMarkForOrder(id, !marked, out reason));
            }
            float bottom = 246 + run.Swords.Count * 154;
            if (Button(new Rect(0, bottom, 202, 82), "全选未封存剑", Active)) Execute((out string reason) => session.TryMarkAllForOrder(true, out reason));
            if (Button(new Rect(216, bottom, 182, 82), "清空选择", Active)) Execute((out string reason) => session.TryMarkAllForOrder(false, out reason));
            if (Button(new Rect(410, bottom, 258, 82), "确认交付", Active, true)) Show(Dialog.Submit);
            Label(new Rect(8, bottom + 94, 650, 88), "不足目标也可交付，会增加愤怒。交付会消耗所选剑并结算下一单；封存剑不会被全选。", 21, muted);
        }

        void CabinetPage()
        {
            var run = view.Run;
            Label(new Rect(6, 0, 650, 65), "剑柜 " + run.Swords.Count + "把 · 封存会冻结剑", 27);
            if (run.Swords.Count == 0) Label(new Rect(20, 100, 628, 170), "剑柜已空。商人到访时可买剑，\n也可在订单页交付并领取下一单供剑。", 27, muted, TextAnchor.MiddleCenter);
            for (int i = 0; i < run.Swords.Count; i++)
            {
                var sword = run.Swords[i]; int id = sword.Id; bool locked = sword.Locked;
                float y = 80 + i * 218; Panel(new Rect(0, y, 668, 204));
                Label(new Rect(20, y + 10, 624, 42), (id == run.SelectedId ? "● " : "") + SwordName(sword), 28);
                Label(new Rect(20, y + 55, 624, 42), "价值 " + Money(RiskForgeRules.Value(sword, run.Context)) + " · 充能 " + sword.Charge + "/10" + (sword.InTribute ? " · 待交付" : ""), 22, muted);
                if (Button(new Rect(16, y + 113, 202, 76), "放上砧台", Active && !locked && !sword.InTribute))
                { Execute((out string reason) => session.TrySelect(id, out reason)); Go(Page.Forge); }
                if (Button(new Rect(230, y + 113, 202, 76), locked ? "解除封存" : "封存（冻结）", Active))
                    Execute((out string reason) => session.TrySetLocked(id, !locked, out reason));
                if (Button(new Rect(446, y + 113, 202, 76), "换剑信用", Active && !locked && !sword.InTribute))
                { selectedDialogSword = id; Show(Dialog.Sell); }
            }
        }

        void RelicsPage()
        {
            var run = view.Run;
            Label(new Rect(6, 0, 650, 48), "遗物架  " + run.Relics.Count + "/" + view.Profile.UnlockedRelicSlots + "  ·  最多8格", 29);
            for (int i = 0; i < 8; i++)
            {
                Rect r = new Rect((i % 4) * 168, 60 + (i / 4) * 150, 160, 138);
                if (i < run.Relics.Count)
                {
                    string id = run.Relics[i];
                    if (Button(r, (i + 1) + "\n" + RiskRelicData.Name(id), Active)) { selectedDialogRelic = id; Show(Dialog.RelicDetail); }
                }
                else if (i < view.Profile.UnlockedRelicSlots) { Panel(r); Label(r, "空槽 " + (i + 1), 23, muted, TextAnchor.MiddleCenter); }
                else if (Button(r, i == view.Profile.UnlockedRelicSlots ? "第 " + (i + 1) + " 格\n广告解锁\n永久保留" : "锁定 " + (i + 1) + "\n依次开启", Active && i == view.Profile.UnlockedRelicSlots)) Show(Dialog.SlotReward);
            }
            Label(new Rect(4, 369, 400, 52), "本单遗物报价", 29);
            bool free = run.Relics.Contains("voucher") && !run.VoucherUsed;
            if (Button(new Rect(414, 360, 254, 76), free ? "免费刷新" : "刷新 · " + Money(session.RerollPrice), Active && run.Round > 1))
                Execute((out string reason) => session.TryReroll(run.Round, out reason));
            for (int i = 0; i < run.ShopOffers.Count; i++) OfferCard(new Rect(0, 452 + i * 212, 668, 198), run.ShopOffers[i], false);
            if (run.ShopOffers.Count == 0) Label(new Rect(20, 465, 628, 80), "本单报价已取完。下一单会有新报价。", 24, muted);
        }

        void OfferCard(Rect r, string id, bool gift)
        {
            var entry = RiskRelicData.Find(id); if (entry == null) return;
            Panel(r);
            Label(new Rect(r.x + 18, r.y + 8, r.width - 36, 44), entry.Name + "  ·  " + (gift ? "开局赠礼" : entry.Price + " 剑信用"), 27);
            Label(new Rect(r.x + 20, r.y + 56, r.width - 40, r.height - 145), PlayerDescription(entry.Description), 21, muted);
            if (!gift && Button(new Rect(r.x + 16, r.yMax - 78, 150, 68), "详情"))
            { selectedDialogRelic = id; Show(Dialog.RelicDetail); }
            float buttonX = gift ? r.x + 16 : r.x + 176;
            float buttonWidth = gift ? r.width - 32 : r.width - 192;
            if (Button(new Rect(buttonX, r.yMax - 78, buttonWidth, 68), gift ? "选择这件赠礼" : "购买 / 满架时替换", !session.PersistenceBlocked && !adVisible, true))
            {
                if (view.Run.Relics.Count >= view.Profile.UnlockedRelicSlots)
                { selectedDialogRelic = id; Show(Dialog.Replace); }
                else Execute((out string reason) => session.TryBuyRelic(id, null, view.Run.Round, out reason), "已获得 " + entry.Name);
            }
        }

        void MerchantPage()
        {
            var run = view.Run;
            Label(new Rect(6, 0, 650, 52), "行脚商人", 32);
            if (run.Round % 3 != 0 || run.MerchantOffers.Count == 0)
            {
                Panel(new Rect(0, 76, 668, 250));
                Label(new Rect(30, 102, 608, 180), "商人每3单到访。\n下次到访：第 " + ((run.Round / 3 + 1) * 3) + " 单", 31, muted, TextAnchor.MiddleCenter);
                return;
            }
            Panel(new Rect(0, 65, 668, 198));
            AtlasIcon(new Rect(20, 84, 88, 88), 10);
            Label(new Rect(122, 77, 522, 56), "锤前准备 · 龙鳞×1", 28);
            Label(new Rect(122, 131, 522, 49), run.RewardedWardGranted ? "本契约已领取；库存中手动使用" : "每份契约仅1次，独立于永久槽位", 20, muted);
            if (Button(new Rect(122, 185, 522, 66), run.RewardedWardGranted ? "已领取" : RewardProviderReady && RewardRequested != null ? "观看广告领取" : "广告未就绪 · 查看说明", Active && !run.RewardedWardGranted, true)) Show(Dialog.WardReward);
            for (int i = 0; i < run.MerchantOffers.Count; i++)
            {
                var offer = run.MerchantOffers[i]; string id = offer.Id; int round = run.Round;
                float y = 285 + i * 222; Panel(new Rect(0, y, 668, 208));
                string title = offer.Kind == "sword" ? ItemName(id) + " · " + (offer.Family == "ordinary" ? "普通" : FamilyName(offer.Family)) + " +" + offer.Grade : ItemName(id);
                Label(new Rect(20, y + 10, 628, 40), title, 28);
                Label(new Rect(20, y + 57, 628, 65), ItemDescription(id), 21, muted);
                Label(new Rect(20, y + 142, 365, 44), "金币 " + Money(offer.Price) + " · 剩余 " + (offer.Stock - offer.Purchased), 22);
                if (Button(new Rect(420, y + 128, 228, 68), "购买", Active && offer.Purchased < offer.Stock && run.Gold >= offer.Price, true))
                    Execute((out string reason) => session.TryBuyMerchant(id, round, out reason), "已购买 " + ItemName(id));
            }
        }
    }
}
