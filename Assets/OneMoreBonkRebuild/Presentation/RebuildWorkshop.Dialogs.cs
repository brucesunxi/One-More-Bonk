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

        void ModalTitle(string title) => Label(new Rect(24, 16, 608, 66), title, 34, ink, TextAnchor.MiddleCenter);
        void CloseModal() { dialog = Dialog.None; skipClockFrame = true; ClearPress(); }
        bool ModalButton(float y, string text, bool primary = false) => Button(new Rect(28, y, 600, 82), text, !commandBusy, primary);

        void StarterDialog(float h)
        {
            ModalTitle("开局赠礼 · 三选一");
            Label(new Rect(28, 85, 600, 58), "选一件遗物，开启这次锻造之旅。", 22, muted, TextAnchor.MiddleCenter);
            modalScroll = GUI.BeginScrollView(new Rect(18, 154, 620, h - 174), modalScroll,
                new Rect(0, 0, 600, view.Run.ShopOffers.Count * 278));
            string[] offers = view.Run.ShopOffers.ToArray();
            for (int i = 0; i < offers.Length; i++) OfferCard(new Rect(0, i * 278, 594, 262), offers[i], true);
            GUI.EndScrollView();
        }

        void FatalDialog(float h)
        {
            var pending = view.Run.PendingBreak;
            long eventId = pending.EventId;
            ModalTitle("剑碎裂了");
            Sprite(new Rect(50, 108, 556, 210), "universal-break");
            Label(new Rect(34, 330, 588, 146), SwordName(pending.PreSword) + "\n锤前价值 " + Exact(pending.Result.ValueBefore) +
                "\n这把剑已碎裂，可换下一把继续。", 26, ink, TextAnchor.MiddleCenter);
            string effects = "已拥有的遗物仍可能带来碎裂收益。";
            if (pending.Result.Procs != null)
                foreach (var proc in pending.Result.Procs) effects += "\n" + proc.Text;
            Label(new Rect(40, 490, 576, Mathf.Max(80, h - 680)), effects, 22, muted, TextAnchor.UpperCenter);
            if (ModalButton(h - 116, "接受损失并继续", true)) Execute((out string reason) => session.TryAcceptBreak(eventId, out reason));
        }

        void ClearDialog(float h)
        {
            string id = view.Run.Id;
            ModalTitle("21单契约完成");
            Label(new Rect(40, 132, 576, 230), "你活过了21单。\n合格订单 " + view.Run.PassCount + "单\n最高 +" + view.Run.Best +
                " · 累计 " + view.Run.TotalHits + "锤\n金币 " + Exact(view.Run.Gold), 31, ink, TextAnchor.MiddleCenter);
            Label(new Rect(42, 390, 572, 140), "可结束契约，永久发现和已开槽保留。\n也可继续延期挑战后续订单。", 25, muted, TextAnchor.MiddleCenter);
            if (ModalButton(h - 220, "继续延期挑战", true)) { Execute((out string reason) => session.TryContinueExtended(id, out reason)); page = Page.Forge; }
            if (ModalButton(h - 118, "结束契约")) Execute((out string reason) => session.TryEndContract(id, out reason));
        }

        void RewardPending(float h)
        {
            var request = view.PendingReward;
            ModalTitle(adVisible ? "广告进行中" : "广告请求待处理");
            string text = request == null ? "等待广告服务返回。" : request.Placement == RiskRewardPlacement.RelicSlot ?
                "请求永久开启第 " + request.TargetSlot + " 个遗物槽。" : "请求本契约龙鳞×1。";
            Label(new Rect(40, 155, 576, 230), text + "\n完整观看并获得奖励后即可领取。\n提前关闭或暂时没有广告，不消耗领取次数。", 28, ink, TextAnchor.MiddleCenter);
            Label(new Rect(42, 414, 572, 160), "若已获奖励但保存未完成，请重试保存，无需再次观看。", 24, muted, TextAnchor.MiddleCenter);
            if (!adVisible)
            {
                if (RewardAwaitingSave && ModalButton(h - 320, "重试保存已获奖励", true))
                {
                    if (RewardRetryRequested != null) RewardRetryRequested.Invoke();
                    else Notify("广告服务尚未接入，不能伪造已获奖励。");
                }
                if (ModalButton(h - 220, "刷新会话状态")) RefreshFromSession();
                if (!RewardAwaitingSave && request != null && ModalButton(h - 118, "取消未获奖的待处理请求"))
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
            ModalTitle("确认交付第 " + capturedRound + " 单");
            var run = view.Run;
            Label(new Rect(34, 128, 588, 280), "消耗已选 " + run.OrderIds.Count + " 把剑\n当前交付价值 " + Exact(session.OrderValue) +
                "\n实际目标 " + Exact(run.Quota) + "\n" + (session.OrderValue >= run.Quota ? "达到目标" : "不足目标：愤怒将上升，请确认") +
                "\n当前愤怒 " + run.Anger + "/3", 29, session.OrderValue >= run.Quota ? ink : danger, TextAnchor.MiddleCenter);
            Label(new Rect(40, 448, 576, 100), "交付后此单立即结算。所选实体剑和碎料贡献将被消耗。", 24, muted, TextAnchor.MiddleCenter);
            if (ModalButton(h - 220, "确认交付", true)) Execute((out string reason) => session.TrySubmitOrder(capturedRunId, capturedRound, out reason));
            if (ModalButton(h - 118, "返回选择")) CloseModal();
        }

        void SellDialog(float h)
        {
            var sword = view.Run.Swords.Find(s => s.Id == selectedDialogSword);
            ModalTitle("换取剑信用");
            if (sword == null) { CloseModal(); return; }
            int credits = RiskEconomy.SwordCreditsForTrade(sword, view.Run.Context, view.Run.AnyReroll, view.Run.HookUsed);
            Label(new Rect(40, 165, 576, 290), SwordName(sword) + "\n价值 " + Exact(RiskForgeRules.Value(sword, view.Run.Context)) +
                "\n消耗这把剑，获得 " + credits + " 剑信用。\n此操作不会获得金币。", 29, ink, TextAnchor.MiddleCenter);
            if (ModalButton(h - 220, "确认换取 " + credits + " 剑信用", true)) Execute((out string reason) => session.TrySellForCredit(selectedDialogSword, out reason));
            if (ModalButton(h - 118, "保留这把剑")) CloseModal();
        }

        void RelicDetail(float h)
        {
            var entry = RiskRelicData.Find(selectedDialogRelic);
            if (entry == null) { CloseModal(); return; }
            ModalTitle(entry.Name);
            Label(new Rect(36, 118, 584, h - 380), PlayerDescription(entry.Description), 28, ink);
            bool owned = view.Run?.Relics.Contains(entry.Id) == true;
            if (owned && ModalButton(h - 220, "丢弃此遗物")) Show(Dialog.Discard);
            if (!owned && view.Run?.ShopOffers.Contains(entry.Id) == true && ModalButton(h - 220, "购买 · " + entry.Price + "剑信用", true))
            {
                if (view.Run.Relics.Count >= view.Profile.UnlockedRelicSlots) Show(Dialog.Replace);
                else Execute((out string reason) => session.TryBuyRelic(entry.Id, null, capturedRound, out reason));
            }
            if (ModalButton(h - 118, "返回")) CloseModal();
        }

        void DiscardDialog(float h)
        {
            ModalTitle("确认丢弃遗物");
            Label(new Rect(40, 165, 576, 280), RiskRelicData.Name(selectedDialogRelic) + "\n丢弃后效果立即移除，不退还剑信用。\n若只是想购买新遗物，可在报价中显式替换。", 28, ink, TextAnchor.MiddleCenter);
            if (ModalButton(h - 220, "确认丢弃", true)) Execute((out string reason) => session.TryDiscardRelic(selectedDialogRelic, out reason));
            if (ModalButton(h - 118, "保留")) CloseModal();
        }

        void ReplaceDialog(float h)
        {
            string incoming = selectedDialogRelic; int round = capturedRound;
            var entry = RiskRelicData.Find(incoming);
            ModalTitle("满架 · 显式替换");
            Label(new Rect(32, 96, 592, 104), "购买 " + RiskRelicData.Name(incoming) + " · " + (entry?.Price ?? 0) + "剑信用\n选一件遗物丢弃，不退费。不需要广告。", 24, ink, TextAnchor.MiddleCenter);
            modalScroll = GUI.BeginScrollView(new Rect(18, 218, 620, h - 352), modalScroll,
                new Rect(0, 0, 596, view.Run.Relics.Count * 102));
            string[] relics = view.Run.Relics.ToArray();
            for (int i = 0; i < relics.Length; i++)
            {
                string outgoing = relics[i];
                if (Button(new Rect(8, i * 102, 580, 90), "丢弃 " + RiskRelicData.Name(outgoing) + " 并购买", true, true))
                    Execute((out string reason) => session.TryBuyRelic(incoming, outgoing, round, out reason));
            }
            GUI.EndScrollView();
            if (ModalButton(h - 118, "取消，保留当前遗物架")) CloseModal();
        }

        void SlotDialog(float h)
        {
            ModalTitle("永久解锁第 " + (view.Profile.UnlockedRelicSlots + 1) + " 格");
            Label(new Rect(40, 145, 576, 280), "完整观看一次广告，开启下一格。\n已开槽位跨契约、重启保留。\n槽位增加摆放空间，遗物需另行获取。\n当前 " + view.Profile.UnlockedRelicSlots + "/8格，最多8格。", 28, ink, TextAnchor.MiddleCenter);
            RewardAvailability(new Rect(40, 450, 576, 130));
            if (ModalButton(h - 220, RewardProviderReady && RewardRequested != null ? "观看广告，永久开启下一格" : "广告未就绪（不发奖）", true)) RequestReward(RiskRewardPlacement.RelicSlot);
            if (ModalButton(h - 118, "继续使用当前槽位")) CloseModal();
        }

        void WardDialog(float h)
        {
            ModalTitle("领取龙鳞×1");
            Label(new Rect(40, 140, 576, 290), "每次契约可免费领取一次龙鳞。\n领取后进入道具库存，可与已有龙鳞叠加。\n请回锻造页手动使用，保护下一锤。\n这一锤无论何种结果都会消耗龙鳞。", 28, ink, TextAnchor.MiddleCenter);
            RewardAvailability(new Rect(40, 450, 576, 130));
            if (ModalButton(h - 220, RewardProviderReady && RewardRequested != null ? "观看广告领取龙鳞" : "广告未就绪（不发奖）", true)) RequestReward(RiskRewardPlacement.Ward);
            if (ModalButton(h - 118, "返回商人")) CloseModal();
        }

        void RewardAvailability(Rect r) => Label(r, RewardProviderReady && RewardRequested != null ?
            "本版本使用Google官方测试广告。\n完整观看后领取；提前关闭或暂无广告时可继续游戏。" :
            "广告暂未就绪，请稍后再试。\n仍可继续使用当前槽位和道具。", 23, muted, TextAnchor.MiddleCenter);

        void MenuDialog(float h)
        {
            ModalTitle("工坊菜单");
            Label(new Rect(40, 110, 576, 110), "永久遗物槽 " + view.Profile.UnlockedRelicSlots + "/8\n测试构建 " + Application.version + " · 原回溯暂未开放", 23, muted, TextAnchor.MiddleCenter);
            if (ModalButton(254, "图鉴与成就")) Show(Dialog.Codex);
            if (ModalButton(356, "保存并返回工坊", true))
            { Execute(session.TryCheckpoint, "已保存"); }
            if (view.Run != null && ModalButton(458, "结束当前契约")) Show(Dialog.End);
            if (ModalButton(h - 118, "返回游戏")) CloseModal();
        }

        void EndDialog(float h)
        {
            ModalTitle("结束当前契约？");
            Label(new Rect(40, 150, 576, 250), "本次订单、金币、剑与局内遗物将结束。\n永久已开槽和原发现/成就保留。\n想下次接着玩，请选择返回游戏后直接退出。", 28, ink, TextAnchor.MiddleCenter);
            if (ModalButton(h - 220, "确认结束当前契约", true)) Execute((out string reason) => session.TryEndContract(capturedRunId, out reason));
            if (ModalButton(h - 118, "返回游戏，保留进度")) CloseModal();
        }

        void CodexDialog(float h)
        {
            ModalTitle("图鉴与成就");
            var achievements = RiskProgression.Achievements();
            float contentHeight = 190 + achievements.Length * 108 + view.Profile.Discoveries.Count * 58 + RiskRelicData.All.Count * 130;
            modalScroll = GUI.BeginScrollView(new Rect(18, 94, 620, h - 225), modalScroll, new Rect(0, 0, 594, contentHeight));
            float y = 0;
            Label(new Rect(8, y, 580, 50), "成就 " + view.Profile.Achievements.Count + "/" + achievements.Length + " · 最高 +" + view.Profile.Best, 26); y += 58;
            foreach (var definition in achievements)
            {
                bool complete = view.Profile.Achievements.Contains(definition.Id);
                Label(new Rect(8, y, 580, 62), (complete ? "✓ " : "○ ") + AchievementText(definition), 22, complete ? mint : muted); y += 64;
                Label(new Rect(8, y, 580, 38), string.IsNullOrEmpty(definition.UnlockedRelicId) ? "" : "遗物开池：" + RiskRelicData.Name(definition.UnlockedRelicId), 19, muted); y += 44;
            }
            Label(new Rect(8, y, 580, 48), "已发现外观", 26); y += 52;
            foreach (string id in view.Profile.Discoveries) { Label(new Rect(8, y, 580, 52), DiscoveryText(id), 22, muted); y += 58; }
            Label(new Rect(8, y, 580, 48), "遗物规则目录", 26); y += 52;
            foreach (var entry in RiskRelicData.All)
            {
                if (entry.Disabled) continue;
                Label(new Rect(8, y, 580, 40), entry.Name, 25); y += 42;
                Label(new Rect(8, y, 580, 82), PlayerDescription(entry.Description), 19, muted); y += 88;
            }
            GUI.EndScrollView();
            if (ModalButton(h - 118, "返回游戏")) CloseModal();
        }

        static string AchievementText(RiskAchievementDefinition entry)
        {
            switch (entry.Condition)
            {
                case "surviving-family-grade": return FamilyName(entry.Family) + "幸存达到 +" + entry.Threshold;
                case "survive-contract-round": return "活过21单契约";
                case "actual-breaks-in-contract": return "单次契约真实碎裂10把剑";
                case "frenzy-starts-in-contract": return "单次契约进入入神5次";
                case "payment-times-effective-quota": return "一次交付达到实际目标的3倍";
                case "full-payment-at-anger": return "愤怒2时足额交付";
                case "purchase-all-stock-in-one-visit": return "一次到访买光商人全部库存";
                case "full-extended-payments-in-contract": return "单次契约延期后足额交付3单";
                case "surviving-grade": return "首次幸存达到 +10";
                case "third-unprotected-underpayment": return "首次因愤怒结束契约";
                case "first-nontutorial-frenzy": return "首次进入入神";
                default: return "完成原始成就条件";
            }
        }

        static string DiscoveryText(string key)
        {
            if (string.IsNullOrEmpty(key)) return "未知外观";
            string[] parts = key.Split(':');
            bool merchant = parts.Length >= 5 && parts[0] == "merchant";
            int stageOffset = merchant ? 2 : 1;
            if (parts.Length < stageOffset + 3 || !int.TryParse(parts[stageOffset], out int stage)) return "已发现外观";
            string route = parts[stageOffset + 1] == "Sunfire" ? "日焰" : parts[stageOffset + 1] == "Moonsteel" ? "月钢" : "虚晶";
            return (merchant ? FamilyName(parts[1]) : "红温剑") + " · 第" + (stage + 1) + "形态 · " + route +
                (parts[stageOffset + 2] == "awake" ? " · 觉醒" : "");
        }
    }
}
