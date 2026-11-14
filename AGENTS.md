# AeroPulse Quest 协作约定

1. 先读README、Docs/02_分步计划.md、Docs/08_迭代记录.md和CHANGELOG。按用户最新反馈确定本轮范围。
2. Unity工作项目即本仓库。保留Assets的.meta、Packages锁文件与ProjectSettings；不提交Library/Temp/Builds、APK、凭据或大量旧ZIP。
3. 每项修复使用短期分支、明确提交。更新CHANGELOG与Docs/08_；新增文档使用两位编号，并更新README相关入口。
4. 新版本保留源码提交/tag与APK SHA-256对应。Editor注入检查不等于Quest验收；记录用户原话、条件、未验证范围。
5. 保留当前自由射线、新捏合门控、手背触碰释放后武装；不擅自重新引入射线吸附或恢复后自动点击。
6. 不把固定模拟数据/示意路线说成真实体征、GPS或地物配准。硬件重量和光学等仍是设计目标。课程演示沿用HUD截图/GIF，不追加实拍宣传视频。
7. 构建用Unity6000.6.8f1与Android支持，AeroUiBuild.Build输出Builds。外部归档由调用者设置AEROPULSE_ARCHIVE_DIR；Gradle代理按本机真实配置显式传入，不硬编码其他人的路径或端口。
8. 可用已有Editor检查验证；只跑与改动相关的必要检查。保留旧tag，不强推、不重写已发布历史或擅自改可见性。

9. 2026-11-14用户授权按Docs/10_自动迭代计划.md自主完成全部开发，中间不请求逐版Quest验收；每版使用0.大版本.小版本tag/私有Release并下载核验。最终集中交付验收，不编造真人反馈。旧ZIP需云端逐个回读SHA一致后才清理。遇到不明SDK/构建/渲染问题优先用Aside/Agent Reach查官方和现成方法，失败后可用官方API等后备。

10. 2026-11-14用户最新范围：后续专注Unity功能、交互、画质/性能、APK构建与Quest 2测试；不主动制作路演PPT、众筹/商业展示材料。旧展示文件仅作历史归档，除非用户再次明确要求，不继续扩展。
