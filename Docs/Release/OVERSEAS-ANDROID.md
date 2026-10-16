# 海外Android发布准备

核验日期：2026-10-16。目标为Google Play首发、AdMob激励广告。本文只记录缺口和验收条件，不改变当前测试配置，也不代表获得生产访问或发布批准。

当前已知：0.1.0/build5为Development APK，包名`com.brucesunxi.onemorebonk.rebuild`，Android Debug签名，官方公开测试App ID及rewarded单元。用户尚未提供Play/AdMob账号、正式IDs、域名或密钥；其存在、归属及后台状态均待确认。已完成证据见[测试报告](../Validation/TEST-REPORT-0.1.0.md)。

## 账号与身份

- [ ] **生产访问**：由账号持有人确认Play账号类型、创建日期、身份/设备验证和Production实际权限，并确认AdMob账号验证、付款资料及政策中心状态。账号能登录、APK可安装或测试广告能播放，都不等于允许生产发布或正式投放。
- [ ] **条件性封闭测试**：若Play个人账号创建于2023-11-13之后，须至少12名测试者连续加入该应用的封闭测试满14天，达标后申请生产访问并提交测试反馈/准备情况；不会自动获批。先以实际账号后台确定是否适用，不将其推广到所有个人或组织账号。[官方条件](https://support.google.com/googleplay/android-developer/answer/14151465?hl=en)
- [ ] **正式应用身份和签名**：确定最终包名、商店名称、账号归属及版本号策略。由持有人配置Play App Signing，明确上传密钥与应用签名密钥的区别；上传密钥、密码及恢复资料由持有人安全备份，不入Git。当前Debug签名不能作为正式发布签名，也不承诺测试包/本地权益可直接迁到正式包。[签名说明](https://developer.android.com/studio/publish/app-signing)

## 包、语言与广告

- [ ] **独立Release AAB入口**：现`Assets/Editor/RebuildBuild.cs`的Bootstrap会重写测试包名，BuildAndroid固定`BuildOptions.Development`并输出APK。增加独立正式入口/配置，输出签名AAB、关闭Development及调试日志；避免仅手改PlayerSettings后又被Bootstrap覆盖。保留隔离验收包及其独立存档。最终检查实际产物的包名、版本、签名、`debuggable=false`和构建哈希。[App Bundle发布](https://developer.android.com/guide/app-bundle)
- [ ] **英文体验**：英文UI尚未完成。覆盖主流程、遗物/道具说明、风险和交付确认、广告请求/取消/保存失败、退出和新契约、存档错误；验证小屏折行和触控。商店说明、截图、支持联系和隐私页面与实际语言/功能相符，不必因此先引入大型多语言框架。
- [ ] **正式广告配置**：当前适配器固定公开测试App ID尾号`~3347511713`和rewarded单元尾号`/5224354917`，测试设置入口也会重写App ID。待持有人提供正式App ID/广告单元映射后，再以独立配置切换并验收；保留槽位永久2→8与每契约龙鳞1次的独立额度。发布检查须确认没有生产路径回落到公开测试ID。[官方测试配置边界](https://developers.google.com/admob/unity/test-ads)
- [ ] **UMP门控**：目前已带UMP依赖，但未实现同意流程。按目标地区和实际受众配置AdMob隐私消息；每次启动更新同意状态，按需展示表单/隐私选项，使用`CanRequestAds()`判断能否请求。考虑SDK初始化可能预加载，在所需选择完成前不得绕过门控；同意失败不能自动视作同意。[官方Unity流程](https://developers.google.com/admob/unity/privacy)

## 披露与正式投放

- [ ] **最小而真实的数据披露**：游戏当前使用本地Profile，不因此宣称“零数据收集”。逐项核对最终GMA/UMP版本及配置的IP/粗略位置、交互、诊断和设备标识等SDK行为；按实际用途完成Data safety、英文隐私政策及应用内入口。商店如实声明含广告、受众和内容分级；不新增首发不需要的账号、分析或采集SDK。[GMA数据说明](https://developers.google.com/admob/unity/privacy/play-data-disclosure)、[Play Data safety](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en)
- [ ] **网站、app-ads.txt和readiness**：确认持有人可管理的开发者网站，将网站链接加入商店资料，在网站发布由实际AdMob账号生成的app-ads.txt并核验抓取。关联正确、可公开下载的支持商店条目；在AdMob确认应用验证和app readiness结果。内部/私有测试或测试广告成功不等于正式广告已全面获准投放，不承诺审核耗时或收入。[app-ads.txt设置](https://support.google.com/admob/answer/9363762?hl=en)、[应用验证](https://support.google.com/admob/answer/14538460?hl=en)、[readiness](https://support.google.com/admob/answer/10564477?hl=en)

## 最终产物与恢复边界

- [ ] **Target API**：build5实际为min25/target36/ARM64。当前官方要求普通手机新应用/更新target API36或以上；正式构建核对最终manifest，并以发布时Play Console检查为准，不能仅凭`AndroidApiLevelAuto`或旧APK通过认定正式AAB通过。[当前要求](https://developer.android.com/google/play/requirements/target-sdk)
- [ ] **原生库与16KB**：对最终AAB及其生成的APK检查所有`.so`的ELF段对齐、ZIP对齐和Bundle的16KB配置，包含Unity/IL2CPP及第三方库；在16KB设备/模拟器启动并跑锻造、保存和广告。保留Play Console上传检查及预发布报告结果。ARM64、Unity版本号或4KB设备运行成功不能替代上述证据；本清单不依赖历史截止日期。[官方检查方法](https://developer.android.com/guide/practices/page-sizes)
- [ ] **闭环与本地恢复**：在最终Release候选重复未earned早退、失焦返回、初始化/加载取消、earned保存失败免重看重试、冷启动权益保留及21单闭环。已保存槽位属于当前本地Profile；无跨设备账号/云恢复承诺。earned未落盘时的重试依赖当前进程，进程被杀可能丢失该证据；备份回退也可能回退最近提交。上线前明确接受或单独解决这些边界，不将SSV、云后端或mediation强列为首发必需项。

完成判断：正式AAB、账号后台、语言/隐私/商店资料和设备验收各自留证；任何一项未知仍记为待核验。当前测试版实播记录不能替代正式配置、Play生成包或生产访问的验证。
