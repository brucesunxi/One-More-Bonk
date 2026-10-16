# 首轮陌生玩家试玩

目标：用5–8名未玩过本作的海外Android玩家，发现闭环中的理解、操作与情绪卡点。先回答“能否理解并愿意继续”，不以这轮样本估计通关率、留存或广告收入。当前默认包为`0.1.0 / versionCode 5`；发包前核对[构建清单](../Validation/build-0.1.0-5.json)与[主测试报告](../Validation/TEST-REPORT-0.1.0.md)。若换包，每场记录新的版本和BuildId。

每人约20–30分钟，可随时提前结束。主持人一名、观察记录一份即可，不新增埋点、奖励、问卷系统或游戏功能。

## 样本与准备

- 招募5–8名陌生玩家，避免都来自开发者朋友或已经听过玩法说明的人。记录是否看过演示；看过者单列，不混作首次接触。
- 当前包以简体中文为主。海外不等于英文读者：正式理解观察优先找能读当前UI的人；不懂中文者可以记录安装、第一印象和语言卡点，但不把语言障碍算成概率/规则设计失败，也不把主持人翻译后的表现算成独立理解。下方英文任务不代表游戏已完整英文适配。
- 使用玩家自愿参加的Android设备。只需记录Android大版本；只有兼容问题确有必要时才另补设备型号。不收设备标识、账号或联系方式。
- 先确认安装包身份、能启动且为官方测试广告配置。测试广告不代表生产广告或真实收入，不承诺正式服务、收入或未来奖励。玩家不必看广告、点击广告或故意输掉来配合研究。
- 默认使用新本地档案、初始2槽。重开契约会保留永久槽，不能把已有3槽的旧档重开后记成“新账号2槽”。不为赶进度给自然组补钱、补剑、补遗物或暗改概率。

## 自然账号与预设对照分开

| 记录组 | 起点 | 用途与限制 |
| --- | --- | --- |
| `natural_2` | 全部5–8人先用默认初始2槽，正常赠礼与供给 | 观察实际新手闭环。这里“自然”指默认权益，不表示完全没有任务提示；广告解锁后仍记这场原组，同时记录实际槽位变化 |
| `preset_2` / `preset_4` / `preset_8` | 可选追加短场；主持人事先准备、可合法载入的隔离测试档 | 仅观察容量下的构筑/替换理解与压力，不代表玩家通过广告获得权益，不计算广告接受度或真实成长速度 |

预设短场不是首轮必做。若没有现成合适测试档，本轮只跑`natural_2`，不临时增加调槽菜单或发奖通路。若做，每人自然场结束后最多追加一个5–8分钟对照场，轮换分配2/4/8，保留自然场在前造成的熟悉效应。5–8人无法给三个组提供可靠统计。

对照档尽量保持同一订单、剑/等级、金币、信用、报价、已装备的同两件遗物与随机起点，仅容量不同；记录无法保持的差异。明确告诉玩家“这是预设测试权益”，不播放虚假领奖过程、不伪造广告凭据。更高容量本身不等于固定百分比战力，短场差异也不等于长期平衡结论。

## 主持人规则

开场只说：“我们在测试游戏，不是在测试你。请按自己的想法玩，想停随时停；有看不懂的地方请说出来。”不先讲最优收手等级、哪件遗物最好、如何保通关或何时该看广告。

允许沉默思考。停顿约30秒时可问“你现在在找什么？”；明显卡住约60秒后，先记下位置与原话，再问是否需要帮助。依次使用并记录：

1. `none`：不提示；或原样重复任务目标。
2. `neutral`：“你以为接下来会发生什么？”“你还想找什么信息？”
3. `interface_help`：应玩家请求解释一个按钮/词语或指出入口，不推荐具体策略。翻译也计入帮助。
4. `technical_help`：安装、崩溃或输入问题处理。误给了策略提示则记`strategy_hint`，后续决策不能当未引导证据。

玩家误解概率或价值时先记录，不立即纠正。任务后再说明事实。不得要求故意不足额交单、故意碎剑、连看广告、夸奖游戏或重开来“完成测试”。看广告与否、输赢和评价都不换取额外奖励。

## 自然场任务与观察

任务逐项给，不一次解释整条最优路径。每项未达到也有价值：写`not_reached`并记录资源、时间、语言或操作原因，不靠主持人造资源补齐。

| 顺序 | 给玩家的中立目标 | 观察内容 |
| --- | --- | --- |
| 1 开局 | “开始一份新契约，选你想要的开局物品。” | 赠礼能否理解、是否知道选择会生效；选择理由，不判断选得好坏 |
| 2 看懂一锤 | “先看看界面，说说你觉得下一锤可能发生什么，这把剑的价值是什么意思。” | 能否区分五结果、概率与当前价值；是否把抽签碎裂率误当龙鳞保护后的最终损失率；保留原话 |
| 3 自己决定 | “接下来按你的想法玩。你可以继续敲，也可以停下来做别的。” | 第一次收手/继续的理由、是否找到其他动作、风险带来的兴奋/烦躁/麻木；不替玩家选停止点 |
| 4 第一单 | “准备好后，找到并交付第一张订单；还没准备好也可以继续。” | 目标、选剑、金币与交付消耗能否理解；不教他故意失败。记录自主理解、帮助后理解或未到达 |
| 5 信用与遗物 | “如果想要报价中的一件遗物，你会怎么做？想换时再实际操作。” | 是否区分金币/剑信用，是否理解换剑会失去实体剑；买不起或不想买也记录 |
| 6 满架选择 | 仅自然达到满架并有相关报价时问：“现在若想换一件，你会怎么做？” | 是否找到显式替换，是否误以为必须看广告；标明这是任务后观察，不能记成自然发现。未满架不强行填满 |
| 7 继续意愿 | “现在你更想继续这份契约、重新开始一份，还是结束试玩？为什么？” | 实际选择与原因，区分“不懂所以停”“不喜欢风险”“累了/没时间”“已经满足”等；不催重开 |

广告只在玩家自然看到入口时观察。先不指着入口问“要不要看”；在其作出选择后再问：“刚才你注意到那个选择了吗？是什么让你选/没选？”若整场未遇到，记`not_seen`，不能算拒绝。加载失败、无填充和取消属于投放/技术结果，不能直接算意愿低。预设开槽不计为观看或接受广告。

## 可直接发给参与者的英文任务

> This is an early Android test build. We are testing the game, not you. Please play in your own way and say what you are thinking. You may stop at any time. The current UI mainly uses Simplified Chinese; please tell us if language blocks you. Ads are optional official test ads. You do not need to watch or click an ad, lose on purpose, or give a positive review.
>
> 1. Start a new contract and choose a starting item you want.
> 2. Before the next hit, tell us what you think could happen and what the sword's value means.
> 3. Play as you wish. Decide for yourself whether to hit again or stop and do something else.
> 4. When you feel ready, find and submit the first order. You may keep playing if you are not ready.
> 5. If you want one of the offered relics, show us how you would get it. You do not have to buy one.
> 6. If your relic shelf becomes full: how would you change one relic for another?
> 7. Would you now prefer to continue, start a new contract, or stop testing? Why?
>
> There are no right answers. If you cannot find an action, please tell us what you expected instead.

仅预设场补充：

> This short session uses a prepared test profile with preset relic slots. You did not earn these slots by watching ads. Please tell us how you would choose or replace relics with the space available.

## 记录与隐私

用[反馈CSV](feedback-template.csv)，一行对应一位玩家的一场；自然场与预设场分别建行。玩家仅用`P01`等匿名编号，场次用`P01-N1`、`P01-C4`等研究编号，不抄游戏内部Profile、Run或广告凭据ID。主持人预填版本/BuildId，玩家不必提供技术哈希。

| 字段组 | 填写口径 |
| --- | --- |
| `participant_id` / `session_code` / `session_date` | 匿名编号、独立场次、日期；同人追加对照保留同编号 |
| `build_version` / `version_code` / `build_id` | 实际安装包，不用“最新版”代替；换包另记 |
| `cohort` / `starting_slots` / `entitlement_origin` | 上表四组；来源填`natural_default`或`test_preset`。自然场内真实广告变化另记`reward_observed` |
| `ui_language` / `reads_ui` / `android_major` | UI语言；阅读填`independent`、`with_help`或`unreadable`；只记录必要兼容信息 |
| 理解与决策 | `first_order_understanding`填`unprompted`、`after_help`、`not_understood`或`not_reached`；概率/价值与收手理由保留原话 |
| 交易与替换 | 记录实际动作/结果或`not_reached`，不把没钱、没看到报价和不想买混为操作失败 |
| `ad_*` / `reward_observed` | 分开记录是否看到、是否自愿选、拒绝理由、加载/播放/earned保存结果；未知写`unknown`，不从关闭动画猜领奖 |
| 退出与问题 | 记录退出阶段、本人原因、最大卡点、帮助级别与情绪原话；bug引用用匿名问题编号，不填私人文件路径 |

`unprompted`只表示未解释规则或操作步骤，不表示没有给任务目标。统一使用以下简短值，其他细节写原话/备注：`first_order_result=full_payment/underpayment/not_reached`；`replacement_result=found_and_replaced/not_found/declined/blocked/not_reached`；`restart_choice=continue/restart/stop/undecided`；`ad_opportunity=not_seen/natural_entry/preset_only`；`ad_choice=watch/decline/leave/not_applicable`；`ad_result=not_started/load_failed/no_fill/closed_without_earned/earned_saved/save_pending/unknown`。不要为填满表格猜测未观察到的结果。

不收真名、邮箱、电话、住址、登录账号、广告/设备ID或完整设备日志。录屏另征同意，拒绝不影响参与；仅录游戏界面，避免通知等个人内容。意外录入的个人信息先删去，再分享匿名片段。原始个人录屏/日志不进入仓库；CSV只保留必要匿名观察。

## 复盘与改动优先级

每场结束先把“观察事实/原话”和“设计者解释”分开。5–8场后归并前三个阻塞点，保留反例，不用一次情绪反应立刻改概率。

| 优先级 | 依据 | 首轮处理 |
| --- | --- | --- |
| P0 | 崩溃、存档/权益丢失、重复发奖、无法继续的真实死路 | 保留可复现步骤与版本，暂停相关路径，先修正确性；一次清楚的复现也足够 |
| P1 | 多人反复看不懂目标/两货币、找不到收手/交付/替换、误以为广告强制 | 优先调整信息、按钮文案/状态与反馈；不立即新增系统或赠送资源 |
| P2 | 可继续玩但节奏、视觉/音效或文案偏好不合适 | 结合原话提出一个小改动，下一轮单独观察 |

对每项建议写“谁在什么状态遇到什么→建议改哪里→下轮怎样观察改善”。如中途修包，后续场次记录新BuildId，不能把前后版本当同一条件。

本轮只报告人数与到达条件，例如“到达满架的3人中，2人主动找到替换”；不把未到达者放进分母，也不把同一人的自然场和对照场当两名独立玩家。小样本、语言、主持帮助、学习顺序、测试广告环境与随机好运/坏运都可能影响结果。不能据此给出稳定胜率、A/B显著性、广告转化率、留存/收入预测或完整平衡结论。
