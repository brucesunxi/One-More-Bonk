# One More Bonk — minimal rebuild

独立重建版：借鉴已研究的 ForgeTax 核心闭环，复用已核验的规则与原创表现资产，重写最小局流程。

## 当前状态

当前已具备可开局、锻造、交易、交单、失败重开与保存续玩的独立测试版；设备验收仍在进行。原项目继续独立保留。

- `main`：已确认规则、范围与开发约束。
- `rebuild/minimal-core`：内核、最小Session、独立存档、现有原创UI/剑素材和官方测试广告的重建工作分支。
- 五页为锻造、订单、剑柜、遗物和商人；不带旧天赋、XP或副单体系。
- 使用Google官方测试广告，尚不能据构建成功声称真实广告领奖已通过。

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
```

依赖查找方式见 `sh Tools/verify-core.sh --help`。测试产物只写入已忽略的`Tests/Headless/.build/`与`Tests/Ads/.build/`；JSON反序列化字段的CS0649警告为当前已知警告。来源哈希及已验证范围见[核心来源记录](Docs/core-provenance.json)。

Unity菜单`One More Bonk Rebuild`提供原生存档验证、独立场景与Android开发构建；广告子菜单可重新配置官方测试ID并解析原生依赖。构建输出位于已忽略的`Builds/Android/`，应用包名`com.brucesunxi.onemorebonk.rebuild`，不会覆盖旧包。

## 来源与验证边界

规则来自现有独立C#实现、公开研究模型和已确认补丁差异；不是原Steam源码或完整二进制等价声明。概率向量检查只覆盖给定状态的抽签输出，不能替代完整遗物生命周期、实际获取、广告、真机与整局平衡验证。

本仓库只迁入明确白名单。Unity缓存、旧版本构建、个人视频、本机绝对路径和签名凭据不进入Git。
