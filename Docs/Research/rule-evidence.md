# ForgeTax 规则证据核对

核对日期：2026-10-16。本文只读了公开页面和既有研究文件，未运行Steam原版、未观看新的商人实机，未改核心或Session。问题及验收输入见[rule-gaps.json](rule-gaps.json)。

## 证据边界

- **A：官方文本**，指开发者公告，独立于粉丝站的交叉确认；不等于二进制实测。
- **B：粉丝站模型/说明**，当前[主页](https://www.forgetax.tools/)仍自报Steam build `25755699`。本次没有证明其在线JS与旧存档逐字节相同。
- **M：公告镜像/构建索引**，辅助定位文章和版本，不能取代官方或实机。
- **L：本地旧审计**，有来源hash，范围见JSON；不因放入新仓就升级为当前事实。
- **P/I：本项目规则/设计推断**，须与原作事实分开。

Steam官方[新闻流](https://steamcommunity.com/app/5245810/allnews/)本次可完整读正文；部分单篇链接只返回标题/图像，因此JSON同时记录原链接、镜像和实际阅读入口。未找到更新于10月12日的可读官方公告。[SteamPeaks表](https://steampeaks.com/app/5245810/forgetax/patches)列最新public为`25883842`，但其页面数据更新时间为10月15日05:16 UTC，不能保证10月16日没有未公告构建。SteamDB的应用记录更新时间也不能当规则更新证据。

## 晚于网站标称build的差异

| 公告 | 当前可确认内容 | Session含义 |
| --- | --- | --- |
| 10月7日[A](https://steamcommunity.com/app/5245810/allnews/)、[M](https://steamdb.info/patchnotes/25771041/) | 起手免费池用Undertaker’s Chalice替换Leather Loop；后者仍在普通商店 | 新局赠礼使用补丁后的池；网站Chalice页仍写Starter gift No，不能直接当最新版起手规则 |
| 10月10日[A](https://steamcommunity.com/games/5245810/announcements/detail/712288859283524553)、[M](https://steamdb.info/patchnotes/25852576/) | 修复多剑存档校验；碎后按E保留已有回溯机会，右键仍可离砧 | 不应以过小库存校验丢合法档；回溯窗口不是收到碎裂后立即无条件结束 |
| 10月12日[A](https://steamcommunity.com/games/5245810/announcements/detail/712288859283524782)、[M](https://steamdb.info/patchnotes/25883842/) | 保护次数/充能与满槽弃置提示；终生契约免加热立即生效，+7从下次供给开始 | 不把现有全部剑即时改成+7；解锁、持有、即时效果和下轮供给分开 |

这些是已公告变化/澄清，没有证据表明10月7日至12日更改了基本破碎曲线或七种现役商人物品数值；“未见公告”不是证明没有未公告改动。11月锻造重构、剑能力、龙战与故事仍是开发计划，不进入当前原闭环。

## Session应先保持的经济边界

[贡值日历](https://www.forgetax.tools/tribute)及[进贡说明](https://www.forgetax.tools/guides/tribute)给出21日序列：

`1800,2100,2600,3350,4350,5700,7500,9800,12800,16700,21500,27750,35700,45800,58650,74650,94550,119350,149950,186850,232000`。

交付的是当日实际窗口内剑和合法残料；欠交也兑现已交价值为金币。普通换剑信用通常每实体1券，不按价值兑换；换券的剑不能再进贡。Chalice残料先入当日待交贡值，不直接记金币。[Chalice详情](https://www.forgetax.tools/relics/undertakers-chalice)

失败增加怒气，足额交付不会按“连败重置”清零。第三次欠交通常致死；Dragon’s Pardon可消失并把怒气留在2，遗忘香购买即时降1且每局一次。[Pardon](https://www.forgetax.tools/relics/dragons-pardon)、[遗忘香](https://www.forgetax.tools/merchant/incense-of-oblivion)

第21日完成的判据是**结算后仍生存**，不是21次足额：该日欠交但未达致死怒气也完成。官方21日公告亦确认；该公告旧的145000终值和少发1剑已被10月6日更新取代，不能整篇套用。[官方历史公告](https://steamcommunity.com/app/5245810/allnews/)、[日期为UTC10月1日的镜像](https://steamdb.info/patchnotes/25642952/)

网站称延期从232000按每日20%增长，且延期足额3次开终生契约。仅据表不能断言使用“逐日取整递推”：第28日表为831298、第29日997558；从第21日232000逐日`floor(previous*1.2)`会得831297、997556，已不相同。表值与未取整基准作幂再四舍五入相符，但尚未确认原生算法；不可随手以递推floor替代。[日历](https://www.forgetax.tools/tribute)、[终生契约](https://www.forgetax.tools/relics/lifetime-contract)

报价受折扣/结余后的有效Q还是基础Q、折扣与结余的先后/取整、已解锁遗物是否当局立即进池仍未独立确认。不得用旧手游选择冒充原作定论。页面还记载明日预览少算折扣/漏结余的显示问题；本项目应使用实际结算查询驱动预览，不为了复刻而主动制造错报。[减税密函](https://www.forgetax.tools/relics/sealed-rebate)、[王室零钱](https://www.forgetax.tools/relics/royal-change)

## 商人道具：现役七种，不是七个砧台按钮

以下是当前逐页重读的B级规则；10月4日官方公告独立确认“五砧台工具＋两个购买即时效果”及退役名单，但没有逐项公开全部价格/内部状态。[官方公告](https://steamcommunity.com/games/5245810/announcements/detail/712288224875120567)、[可读镜像](https://steamdb.info/patchnotes/25713030/)

| id/来源 | 使用与效果 | 首日/选中报价库存 | 价格参数：Q比例、最低G、系数 |
| --- | --- | --- | --- |
| [ward](https://www.forgetax.tools/merchant/cracked-dragon-scale) | 锤前武装，下一次被接受的锤无论结果都耗；挡Break，入神/满充能不可武装 | 3 / 3 | .30、500、1.4 |
| [rewind](https://www.forgetax.tools/merchant/regret-clockwork) | 事前持有，未离砧时撤销最近一锤，含碎裂 | 3 / 1 | .85、1500、1.8 |
| [chalk](https://www.forgetax.tools/merchant/red-chalk) | 下一锤Great票加倍，入神可用；不是最终Great概率直接乘2 | 6 / 3 | .40、700、1.4 |
| [powder](https://www.forgetax.tools/merchant/powder-pouch) | 下一普通锤S/G归Great，其余归Break；不是成功率保护 | 6 / 1 | .50、800、1.8 |
| [starshard](https://www.forgetax.tools/merchant/cold-star-shard) | 即时+3充能，入神/已满不能用；满10后下一锤才启动入神 | 6 / 3 | .50、800、1.4 |
| [oblivion](https://www.forgetax.tools/merchant/incense-of-oblivion) | 买下即时降怒1；须已有怒气，每局限一次 | 9 / 1 | 1.20、3500、1.8 |
| [delivery](https://www.forgetax.tools/merchant/ghost-delivery-note) | 买下登记下一次供给+2基础剑，不救当前空库存 | 6 / 3 | .65、1000、1.4 |

网站表与本地价格对照支持首件向上取到10G、同一报价每次续购×3的拟合；这不是原作代码证据。Q含义仍为gap。商人3、6、9…到访，第三日保证ward和rewind，之后不是每件必出；传奇的22%说明不足以当整次到访固定出现率。[商人页](https://www.forgetax.tools/merchant)

五旧工具oil/candle/steelcore/contractink/echodust已停售；本项目新档没有旧工具迁移需求。历史退款价不能当在售价格，也不要把退役效果重新加进首发。

### 空手不等于全库存为空

[Funerary](https://www.forgetax.tools/swords/family/funerary)、[World Tree](https://www.forgetax.tools/swords/family/world-tree)、[Fishblade](https://www.forgetax.tools/swords/family/fishblade)明确解释：商人直接把买到的剑交到手上，因此要求空手。网站[入门说明](https://www.forgetax.tools/guides/how-to-play)还说明剑架3位、地面、砧台和手里的未交剑跨夜保留；官方多剑存档修复与未交砧台剑跨日修复是旁证。原版可有多剑储备。

没有新实机证明原生购买函数具体检查哪一个字段；但将“空手”解释为`Inventory.Count==0`已与公开文字不符。手游可把物理交接简化为买入剑柜，保留多剑/跨日/真实库存与价格约束，并明确这是交互适配。不要仅因锻造页选中一剑就禁止买剑，也不要购入时覆盖该剑。

## 回溯目前能证明什么

网站仅明确事前有charge才记最近锤、仍在砧台可撤销任意最近结果。官方修过回溯/重启导致金币或票重复，也修过碎后按E误失机会。它证明存在跨状态的撤销需求，但**没有公开完整快照合同**。[回溯详情](https://www.forgetax.tools/merchant/regret-clockwork)、[官方更新流](https://steamcommunity.com/app/5245810/allnews/)

原作是否回退RNG、时钟、消耗品、保护次数、复制实体、残料/金币、继承队列与永久成就，必须逐项验证；不能凭“恢复了剑”认定全部一致。现项目继续按SCOPE暂缓回溯，保留明确缺项；`PendingBreak`只解决真实死亡提交，并不自动成为完整undo。

## 下一批可执行核验

先补会改变经济结果的输入：有折扣/结余时报价，非整数延期日，21日第3次欠交与Pardon组合，下一日供给/传承叠加，满当前槽位的买入替换。回溯单独做五结果、复制/金币/继承与重复输入的前后状态对照；未做就保持unknown，不为赶进度假定RNG回退或永久成就回滚。
