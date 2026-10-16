# 最小架构

目标是隔离旧业务、保留已有正确性，不是全量重写算法。以下为实施边界，不代表Session、存档、UI或广告已完成。

## 三个边界

| 部分 | 职责 | 约束 |
| --- | --- | --- |
| 锤击内核 | 已核验规则、必要模型、RNG、遗物查表 | 不依赖旧Session、Career、UI、磁盘或SDK |
| 最小会话 | 库存、供剑、主订单、交易、原解锁、合法命令与一次提交 | 读取真实已解锁容量；不引入旧成长和迁移链 |
| Unity适配与表现 | 数据加载、存档IO、SDK生命周期、五页及剑显示 | 只读显示状态，发出命令，不直接改钱、RNG、槽位或库存 |

原`RiskRelicData`使用`Resources/JsonUtility`，不能原样搬入后宣称零Unity依赖。保留明确的Runtime加载适配，向内核提供单一规则目录/查表；不建通用registry。自包含测试的Unity shim放`Tests/Headless/`，绝不放`Assets/`。

原`RiskSession`、`ForgeSession`、`ContractSession`、Career及`RiskWorkshop`整类不迁入。旧页面partial仍共享业务，拆文件不等于已解耦。复用视觉资源与独立绘制方法，重新绑定最小状态。

## 状态与提交

- `Profile`：原永久发现/解锁所需记录、`UnlockedRelicSlots(2..8)`和最小领奖去重；没有XP或天赋。
- `Run`：订单、库存、货币、遗物/原道具、商人、RNG、待损失事件及本契约龙鳞已领。
- `SaveStore`：带schema与规则标识的完整玩家状态，保存失败阻止后续变更；奖励、额度和凭据不得拆成独立成功。

槽位请求绑定Profile、下一目标格、请求前容量与唯一requestId；龙鳞请求绑定Run、商人到访及requestId。两者有不同placement，只有一个SDK请求可在途。earned进入会话命令后校验并提交；回调、动画结束不直接发奖。写失败重试同一earned，不要求重看。

保留`PendingBreak`及原死亡结算边界。顶层动作只提交一次，不在`Commit`内嵌套另一个`Commit`；结果窗不得再次结算。

## 独立运行

新namespace使用`OneMoreBonk.Rebuild`。新入口场景、测试applicationId、产品名、存档子目录和构建输出均独立；不读写旧v07/v08档，不覆盖旧包或旧设置。测试身份与正式发布身份分开确认。

先锁定Unity `6000.3.17f1`和URP `17.3.0`，不同时升级引擎与SDK。按需要添加包；不整拷旧manifest、ProjectSettings或Build脚本。构建记录唯一buildId、代码/规则/资源hash与验证范围。

剑表现由家族、外观阶数、材质等只读描述驱动，不反向决定等级或经济。预览不得消耗游戏RNG。长按在松手、切页、失焦和广告返回时清除；菜单/广告统一暂停锻造钟。
