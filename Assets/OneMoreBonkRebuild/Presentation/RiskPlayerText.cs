using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneMoreBonk.Rebuild.Presentation
{
    /// <summary>English display text only. Never mutates rules, saves or rewards.</summary>
    public static class RiskPlayerText
    {
        [Serializable] sealed class DisplayCatalogue { public DisplayRelic[] Relics = Array.Empty<DisplayRelic>(); }
        [Serializable] sealed class DisplayRelic { public string Id = ""; public string Description = ""; public bool Inactive = false; }
        static readonly Dictionary<string, DisplayRelic> relics = LoadRelics();

        static Dictionary<string, DisplayRelic> LoadRelics()
        {
            var result = new Dictionary<string, DisplayRelic>(StringComparer.Ordinal);
            TextAsset asset = Resources.Load<TextAsset>("Risk/player-text-en");
            DisplayCatalogue catalogue = asset == null ? null : JsonUtility.FromJson<DisplayCatalogue>(asset.text);
            if (catalogue?.Relics != null)
                foreach (DisplayRelic entry in catalogue.Relics)
                    if (entry != null && !string.IsNullOrEmpty(entry.Id)) result[entry.Id] = entry;
            return result;
        }

        public static string RelicName(string id)
        {
            string name = RiskRelicData.Find(id)?.NameEn;
            return !string.IsNullOrEmpty(name) ? name : "Unknown relic";
        }
        public static string RelicDescription(string id) => id != null && relics.TryGetValue(id, out DisplayRelic entry)
            ? entry.Description : "Description unavailable.";

        public static string FamilyName(string id)
        {
            switch (id)
            {
                case "citadel": return "Citadel Sword";
                case "holy": return "Holy Sword";
                case "sawblade": return "Sawblade";
                case "dragonfang": return "Dragonfang";
                case "runeblade": return "Runeblade";
                case "splitblade": return "Splitblade";
                case "worldtree": case "world_tree": case "world-tree": return "World Tree Sword";
                case "seafood": case "fish": case "fishblade": return "Fishblade";
                case "funerary": return "Funerary Sword";
                default: return "Standard Sword";
            }
        }

        public static string OutcomeLabel(ForgeOutcome outcome)
        {
            switch (outcome)
            {
                case ForgeOutcome.Break: return "Break";
                case ForgeOutcome.Damage: return "Damage";
                case ForgeOutcome.Hold: return "Hold";
                case ForgeOutcome.Success: return "Success";
                case ForgeOutcome.Great: return "Great";
                default: return "Unknown result";
            }
        }
        public static string OutcomeLabel(ForgeOutcome? outcome) => outcome.HasValue ? OutcomeLabel(outcome.Value) : "No result";

        public static string AchievementName(string id)
        {
            switch (id)
            {
                case "FORGE20_CITADEL": return "Citadel +20";
                case "FORGE20_HOLY": return "Holy Sword +20";
                case "FORGE20_SAW": return "Sawblade +20";
                case "FORGE20_DRAGON": return "Dragonfang +20";
                case "FORGE20_RUNE": return "Runeblade +20";
                case "FORGE20_SPLIT": return "Splitblade +20";
                case "FORGE20_WORLDTREE": return "World Tree +20";
                case "FORGE20_FISH": return "Fishblade +20";
                case "FORGE20_FUNERARY": return "Funerary Sword +20";
                case "FORGE_CLEAR": return "Contract Complete";
                case "FORGE_BROKEN10": return "Ten Broken Swords";
                case "FORGE_FRENZY5": return "Five Frenzies";
                case "FORGE_TRIPLETRIBUTE": return "Triple Payment";
                case "FORGE_LASTCHANCE": return "Last Chance";
                case "FORGE_SOLDOUT": return "Sold Out";
                case "FORGE_OVERTIME3": return "Three More Orders";
                case "FORGE_FIRST10": return "First +10";
                case "FORGE_FIRSTDEATH": return "First Failed Contract";
                case "FORGE_FIRSTFRENZY": return "First Frenzy";
                default: return "Achievement";
            }
        }

        public static string AchievementDescription(string id)
        {
            RiskAchievementDefinition entry = RiskProgression.FindAchievement(id);
            if (entry == null) return "Achievement details unavailable.";
            switch (entry.Condition)
            {
                case "surviving-family-grade": return "Reach +" + entry.Threshold + " with a surviving " + FamilyName(entry.Family) + ".";
                case "survive-contract-round": return "Complete the 21-order contract.";
                case "actual-breaks-in-contract": return "Actually break 10 swords in one contract.";
                case "frenzy-starts-in-contract": return "Enter Frenzy 5 times in one contract.";
                case "payment-times-effective-quota": return "Pay at least 3 times the actual order target in one delivery.";
                case "full-payment-at-anger": return "Pay an order in full at Anger 2.";
                case "purchase-all-stock-in-one-visit": return "Buy every unit of merchant stock during one visit.";
                case "full-extended-payments-in-contract": return "Pay 3 extended orders in full in one contract.";
                case "surviving-grade": return "Reach +10 with a surviving sword for the first time.";
                case "third-unprotected-underpayment": return "Lose a contract to Anger for the first time.";
                case "first-nontutorial-frenzy": return "Enter Frenzy for the first time.";
                default: return "Achievement details unavailable.";
            }
        }

        /// <summary>Translate known legacy notices. Already-English messages pass through unchanged.</summary>
        public static string Message(string source)
        {
            if (string.IsNullOrEmpty(source)) return source ?? "";
            if (messages.TryGetValue(source, out string translated)) return translated;
            foreach (KeyValuePair<string, string> prefix in messagePrefixes)
                if (source.StartsWith(prefix.Key, StringComparison.Ordinal))
                    return prefix.Value + Message(source.Substring(prefix.Key.Length));
            // Unknown operating-system/SDK messages can vary by device language.
            // Do not expose unreviewed Chinese text as a fake translation.
            return ContainsHan(source) ? "This action could not be completed. Please try again." : source;
        }

        static bool ContainsHan(string value)
        {
            foreach (char character in value)
                if ((character >= '\u3400' && character <= '\u9fff') || (character >= '\uf900' && character <= '\ufaff')) return true;
            return false;
        }

        static readonly Dictionary<string, string> messages = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Google官方测试广告（无生产收益）", "Google test ads (no production revenue)" },
            { "Google官方测试广告仅在Android设备启用，编辑器不发模拟奖励", "Google test ads are available on Android devices only. The Editor does not grant simulated rewards." },
            { "Google官方测试广告仅在Android设备启用", "Google test ads are available on Android devices only." },
            { "已获奖励待保存，请重试保存，无需重看", "Reward earned, save pending. Retry saving without watching again." },
            { "已有一条广告请求，请等待或取消加载", "An ad request is already in progress. Wait or cancel loading." },
            { "游戏存档尚未打开", "The player save is not open yet." },
            { "正在加载Google官方测试广告，可在展示前取消", "Loading a Google test ad. You may cancel before it starts." },
            { "测试广告初始化失败，未发奖励，可稍后重试", "The test ad could not initialize. No reward was granted. Please try later." },
            { "测试广告暂不可用，未消耗解锁或龙鳞额度", "The test ad is unavailable. No slot unlock or Dragon Scale allowance was used." },
            { "测试广告已失效，请稍后重试", "The test ad has expired. Please try later." },
            { "测试广告展示失败，未获奖励", "The test ad could not be shown. No reward was earned." },
            { "正在展示Google官方测试广告", "Showing a Google test ad." },
            { "官方测试奖励已保存：永久开启下一遗物格", "Test reward saved: the next relic slot is permanently unlocked." },
            { "官方测试奖励已保存：龙鳞×1已入库，请手动使用", "Test reward saved: 1 Dragon Scale added to inventory. Arm it manually." },
            { "测试广告已关闭，未获奖励，未消耗额度", "The test ad closed without a reward. Your allowance was not used." },
            { "没有待取消请求", "There is no pending request to cancel." },
            { "未获奖待请求已关闭，额度未消耗", "The unearned request was closed. Your allowance was not used." },
            { "已经获得奖励，请重试保存，无需重看", "The reward was already earned. Retry saving without watching again." },
            { "请先关闭系统广告；应用不会替您取消正在展示的广告", "Close the ad first. The game cannot cancel an ad that is already showing." },
            { "测试广告请求已取消，未消耗解锁或龙鳞额度", "The test ad request was cancelled. No slot unlock or Dragon Scale allowance was used." },
            { "等待同一笔已获奖励回调，不会重新播放", "Waiting for the existing earned reward. The ad will not play again." },
            { "请先关闭系统广告", "Close the ad first." },
            { "同一笔官方测试奖励已保存，无需重看", "The existing test reward was saved. No need to watch again." },
            { "测试广告初始化超时，未发奖励，可手动重试", "Test ad initialization timed out. No reward was granted. You may retry." },
            { "测试广告加载超时，未发奖励，可手动重试", "Test ad loading timed out. No reward was granted. You may retry." },
            { "广告尚未就绪，未发放奖励。可继续游戏。", "Ads are not ready. No reward was granted. You can keep playing." },
            { "保存已恢复", "Save completed. You can continue." },
            { "碎裂", "Break" }, { "损伤", "Damage" }, { "不变", "Hold" }, { "成功", "Success" }, { "大成功", "Great" },
            { "入神", "Frenzy" }, { "防碎", "Break protection" }, { "复制", "Duplicated" }
        };

        // Longest semantic prefixes first; only the remaining diagnostic text recurses.
        static readonly KeyValuePair<string, string>[] messagePrefixes =
        {
            Pair("已获测试奖励，尚未保存；请重试保存，无需重看。", "Test reward earned, save pending. Retry saving without watching again. "),
            Pair("已获奖励仍待保存，可继续重试。", "The earned reward is still awaiting a save. You may retry. "),
            Pair("取消状态尚未保存，可稍后重试取消。", "Cancellation has not been saved. You may retry cancelling. "),
            Pair("测试广告初始化异常：", "Test ad initialization failed: "),
            Pair("测试广告加载异常：", "Test ad loading failed: "),
            Pair("测试广告状态异常：", "Test ad state check failed: "),
            Pair("测试广告展示异常：", "Test ad display failed: "),
            Pair("请求尚未关闭：", "The request is not closed: "),
            Pair("操作未完成：", "Action not completed: "),
            Pair("保存待重试：", "Save needs retrying: "),
            Pair("广告未启动：", "The ad did not start: "),
            Pair("未锤击：", "Hammer not used: ")
        };
        static KeyValuePair<string, string> Pair(string source, string translated) => new KeyValuePair<string, string>(source, translated);
    }
}
