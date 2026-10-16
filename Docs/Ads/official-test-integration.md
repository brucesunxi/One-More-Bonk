# Google官方测试广告接入

2026-10-16。本轮目标是Android官方测试广告开发候选包，无生产广告位、盈利或SSV声明。没有点击即发奖的Demo通路，也没有旧碎后恢复业务。

## 接线

场景挂`OneMoreBonk.Rebuild.Ads.RebuildRewardedAds`，与`Presentation.RebuildWorkshop`同物体或由组件Start查找绑定。UI提供Session及RewardRequested/RewardRetryRequested/RewardCancelRequested事件；适配器使用SetRewardPresentation暂停/清长按，完成后Refresh显示权威状态。

两个placement分别交Session：永久槽位`relic-slot-unlock`（2→8，Profile最多6次）与商人`prestrike-ward-grant`（Run最多1次，ward入库后手动用）。SDK奖励数量/类型不替代游戏自己的固定合同。

加载阶段保持SetRewardPresentation(false)，由PendingReward阻止锻造，等待页仍可取消；真正Show才置true。Notice同步RewardAwaitingSave=HasPendingEarned，只有earned保存失败才显示该重试，取消保存失败保留取消入口，完成后清零。

生命周期为Begin持久请求→Initialize→Load→CanShowAd→Show。仅Show的earned回调生成本请求凭据；loaded、shown、closed、超时不发奖。回调先锁存earned信号，再经MobileAdsEventExecutor进入Unity主线程；关闭且回到前台后才提交。单请求、重复/过期回调不会再发。

保存失败保留同一earned凭据，按钮重试Session.TryRetryEarnedReward，不重看。取消/无填充的保存失败可在同一等待页重试取消，先恢复Checkpoint再取消。展示前可取消；系统视频展示中由用户关闭广告，应用不伪造关闭。初始化等待35秒，进入Load重新计30秒，均只计前台时间；展示期间没有随意超时取消。失焦不取消请求，锻造时间由UI和Session共同暂停。

开发包/编辑器日志以`[RebuildAds]`标出阶段、placement和状态，区分原生/主线程回调，加载失败记录错误码和脱敏消息。不记录设备、档案、契约、请求或奖励凭据ID。单纯超时不能证明网络故障。官方11.6的MobileAds.Instance会初始化EventExecutor，无须重复创建；本地日志同时记录executor是否活跃。

## 固定配置与执行

Packages/Vendor保留GMA11.6.0、EDM2.1.0源包及许可；[来源与hash](../../Packages/Vendor/provenance.json)。GMA的name/version/EDM依赖与官方tagged package.json相符，SHA256与既有本地供应清单相符。网络读取registry元数据失败，未将其写成“重新下载官方digest比对通过”。

由构建负责人在Unity执行：
`OneMoreBonk.Rebuild.Editor.RebuildAdsSetup.ConfigureOfficialTestAndResolve`

它配置公开测试App ID `ca-app-pub-3940256099942544~3347511713`、Standard Android SDK和EDM，然后调用ResolveSync。rewarded单元固定`ca-app-pub-3940256099942544/5224354917`；两个placement共用官方测试创意，业务凭据和额度分别绑定。Android最小API24，编译SDK35或更高；构建设置由负责人统一管理。本组件不在编辑器发模拟奖励，正式启用限Android运行。

后续生产切换应另一次明确改动加入真实配置与发布验收；当前没有隐藏生产ID回退或自动盈利模式。

## 时序适用范围

[Google Android rewarded文档](https://developers.google.com/admob/android/rewarded)保证Google直投earned先于dismiss。[测试广告说明](https://developers.google.com/admob/unity/test-ads)指出样例单元只展示Google Ads。本次据此在关闭未earned时取消；已经收到earned但主线程队列倒序的情况由同步信号保护。

不能把这一保证推广到任意中介平台。未来引入mediation必须重新核验回调顺序，当前没有为未知第三方无限等待或补造奖励。冷启动只有已保存请求、没有收到/保留的earned证据时，不猜测奖励；可取消未获奖待请求。已收earned但磁盘持续不可写期间进程被杀，不承诺恢复内存证据，需独立冷启动/持久化产品方案。

## 已验证与未验证

受控事件harness位于Tests/Ads，Unity/GMA/UI替身只在测试编译中出现，绝不在Assets。运行：
`UNITY_EDITOR_ROOT=<Unity.app> sh Tools/verify-ads-adapter.sh`

当前79断言覆盖取消、无填充、earned、重复/过期、已收到earned后队列倒序、保存失败免重看、取消保存失败、失焦返回、CanShowAd/Show异常、前台加载超时及两奖励独立性。新增后台加载成功、回前台时CanShowAd=false或抛异常的两个分支：先在旧代码复现Complete(null)空引用，再验证守卫修复后安全取消、无奖、恢复可请求。另用真实Unity/GMA程序集编译新Assets源码检查API。结果见[validation.json](validation.json)。

这些不是设备广告实播证据。新包的依赖resolve、Android原生构建、网络加载/早退、真实earned、两奖励冷启动保留仍由负责人后续验证；旧项目实播记录不转算为新版本通过。没有运行生产配置、收入、SSV、mediation或iOS验证。

## 核验来源

- [11.6.0发布记录](https://github.com/googleads/googleads-mobile-unity/releases/tag/v11.6.0)：Android SDK25.5.0、minAPI24及EDM2.1.0。
- [tagged package.json](https://raw.githubusercontent.com/googleads/googleads-mobile-unity/v11.6.0/packages/com.google.ads.mobile/package.json)。
- [tagged rewarded示例](https://raw.githubusercontent.com/googleads/googleads-mobile-unity/v11.6.0/samples/HelloWorld/Assets/Scripts/RewardedAdController.cs)：Load/CanShow/Show/Destroy与主线程调度。
- [Android初始化](https://developers.google.com/admob/android/quick-start)：测试App ID。
- [11.6.0 MobileAds源码](https://raw.githubusercontent.com/googleads/googleads-mobile-unity/v11.6.0/source/plugin/Assets/GoogleMobileAds/Api/MobileAds.cs)：Instance负责初始化EventExecutor；Android初始化文档说明原生回调本身最多等待30秒。

当前Unity rewarded指南示例列出了与测试表不一致的ID；本实现采用官方测试表及固定tag示例一致的rewarded ID，不照抄该不一致值。
