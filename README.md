# One More Bonk — minimal rebuild

独立重建版：借鉴已研究的 ForgeTax 核心闭环，复用已核验的规则与原创表现资产，重写最小局流程。

## 当前状态

当前候选为Android测试版`0.1.1 / versionCode 7`，已构建、覆盖安装并运行。采用「设计多款上线版游戏UI」对话的明亮庭院、薄荷控台和中央暖金圆钮方向，五概率在平放剑体上方，十孔灯逐格点亮；完成英文主流程及98件目录说明，加入可关闭的锤击音效和Android关键结果触感。原项目继续独立保留。

当前范围以[本轮报告](Docs/Validation/ITERATION-02.md)及[构建7清单](Docs/Validation/build-0.1.1-7.json)为准；此前0.1.0的实机广告证据保留在[历史测试报告](Docs/Validation/TEST-REPORT-0.1.0.md)，不改记成本轮重播。

此前[闭环与原生保存增量验收](Docs/Validation/ITERATION-01.md)包含独立Android受控7组293项；本轮Session333、经济9200、广告受控111项重跑通过，新增反馈波形/分类47项与原生98件英文资源覆盖校验。13个业务源文件保持不变。已备好[陌生玩家试玩流程](Docs/Playtest/FIRST-ROUND.md)和[海外Android发布清单](Docs/Release/OVERSEAS-ANDROID.md)，真人体验与正式发布适配继续推进。

- `main`：已确认规则、范围与开发约束。
- `rebuild/minimal-core`：内核、最小Session、独立存档、现有原创UI/剑素材和官方测试广告的重建工作分支。
- 构建7已查看锻造、订单、剑柜、遗物和商人未到访态首屏；商人到访/购买、完整状态和设备矩阵仍待验，不把首屏观察写成全部功能通过。
- 版本4两处Google官方测试广告均已实播、earned并保存；槽位2→3且冷启动保留，龙鳞入库后未自动武装。
- 版本5已验证更新保留既有槽位/凭据/龙鳞、手动武装后下一锤消耗，以及结束旧契约后免费新开时保留永久槽、重置龙鳞领取预算。
- 仍是测试版：正式广告ID/收入、设备矩阵与全面平衡待验证；完整原版rewind继续延期。

## 已确认规则

- 原概率/价值/遗物结果链、有限供剑、主订单、金币与剑信用、原解锁闭环。
- 遗物上限8格，初始2格；第3–8格逐个广告解锁，同一玩家存档跨局永久保留，最多6次成功槽位奖励。
- 龙鳞广告每契约最多成功领取1次，进入原库存，由玩家提前手动使用；与永久槽位独立记账。
- 不带入旧XP、天赋、副单信用、预约和多重碎后恢复体系。

详见[执行契约与公式](Docs/forge-rules.md)、[范围](Docs/SCOPE.md)、[架构](Docs/ARCHITECTURE.md)、[执行顺序](Docs/MILESTONES.md)。

## 开发基线

- Unity `6000.3.17f1`，URP `17.3.0`；先沿用已知版本。
- `rebuild/minimal-core`分支以仓库根目录作为Unity项目，入口为`Assets/Scenes/RebuildWorkshop.unity`。
- 在工作分支运行下方命令，重跑133组概率向量和6组保护顺序检查。脚本需要本机Unity附带的Mono/C#编译器与Newtonsoft依赖；不启动Unity编辑器，不读取旧项目。
- Google Mobile Ads `11.6.0`、EDM `2.1.0`固定供应在`Packages/Vendor`，许可与来源同目录保存。仅使用公开测试配置。

```sh
# macOS：将占位符替换为已安装的 Unity.app 完整路径。
UNITY_EDITOR_ROOT="<Unity.app的完整路径>" sh Tools/verify-core.sh
UNITY_EDITOR_ROOT="<Unity.app的完整路径>" sh Tools/verify-effects.sh
UNITY_EDITOR_ROOT="<Unity.app的完整路径>" sh Tools/verify-session.sh
UNITY_EDITOR_ROOT="<Unity.app的完整路径>" sh Tools/verify-ads-adapter.sh
# 先用Unity编译当前工程；此项验证实际生成程序集中的反馈数学与分类。
UNITY_EDITOR_ROOT="<Unity.app的完整路径>" sh Tools/verify-feedback.sh
```

依赖查找方式见 `sh Tools/verify-core.sh --help`。测试产物只写入已忽略的`Tests/Headless/.build/`与`Tests/Ads/.build/`；JSON反序列化字段的CS0649警告为当前已知警告。来源哈希及已验证范围见[核心来源记录](Docs/core-provenance.json)。

Unity菜单`One More Bonk Rebuild`提供原生存档验证、独立场景与Android开发构建；广告子菜单可重新配置官方测试ID并解析原生依赖。构建输出位于已忽略的`Builds/Android/`，应用包名`com.brucesunxi.onemorebonk.rebuild`，不会覆盖旧包。

## 来源与验证边界

规则来自现有独立C#实现、公开研究模型和已确认补丁差异；不是原Steam源码或完整二进制等价声明。概率、独立效果回归、Session与广告检查分别有范围，既有实机成功路径也不代替完整设备/故障矩阵或整局平衡验证。

本仓库只迁入明确白名单。Unity缓存、旧版本构建、个人视频、本机绝对路径和签名凭据不进入Git。
