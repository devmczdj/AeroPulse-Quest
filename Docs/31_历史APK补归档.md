# 历史 APK 补归档

补录日期：2026-11-20。

本轮补归档 `v0.22.2` 至 `v0.24.0` 共 16 个版本。所有 APK 都是当时实际生成的构建产物，补录时重新完成压缩包、签名、包名、版本号、文件大小和 SHA-256 校验；上传后又从 GitHub Release 下载回读，字节和哈希全部一致。

## 源码对应关系

- `v0.22.2` 至 `v0.22.6` 在 Git 历史中仍存在对应源码提交，标签准确指向当时的提交。
- `v0.22.7` 至 `v0.23.6` 当时没有独立提交；源码被后续修改覆盖。标签为了保留版本入口，统一补指向最终检查点 `9207672`。它们**不代表**逐版本可复现的精确源码快照。
- `v0.24.0` 的源码与统一检查点 `9207672` 一致。

补录没有改写这些版本的安装包，也没有把当前源码冒充为旧版本源码。后续每个通过验证的版本必须立即提交并推送到功能分支，再建立标签和预发布。

## 版本清单

| 版本 | Android code | 源码提交 | APK SHA-256 |
|---|---:|---|---|
| [v0.22.2](https://github.com/devmczdj/AeroPulse-Quest/releases/tag/v0.22.2) | 24 | `113b41f` | `9fa78368bc23786b2b6d205dfc8b74632ad2ae83772656d724ae829c5845c79d` |
| [v0.22.3](https://github.com/devmczdj/AeroPulse-Quest/releases/tag/v0.22.3) | 25 | `9189b07` | `438f9bf5077283e68460c72a9f8bcba60842de60e4dddb9c4bd8d4f91149da14` |
| [v0.22.4](https://github.com/devmczdj/AeroPulse-Quest/releases/tag/v0.22.4) | 26 | `9d8ed1d` | `edb62ce97c8c9daa9be79372d766b361e2fd0495e8e0d824181f674782412262` |
| [v0.22.5](https://github.com/devmczdj/AeroPulse-Quest/releases/tag/v0.22.5) | 27 | `469e1de` | `4a9863c5a9e6458a076fa1f64901b19d0ed2860a591c8d19d19988fc03e6414a` |
| [v0.22.6](https://github.com/devmczdj/AeroPulse-Quest/releases/tag/v0.22.6) | 28 | `780238a` | `3f2d10bbe74118c458ebf989f14e3f3aa584d01bb521eb3729269089d6a6273a` |
| [v0.22.7](https://github.com/devmczdj/AeroPulse-Quest/releases/tag/v0.22.7) | 29 | `9207672`，补指向 | `0a0abebbef83d593e6a7e2e601b3e60d2c29cac7e45ecba4a66469eaa0d11958` |
| [v0.22.8](https://github.com/devmczdj/AeroPulse-Quest/releases/tag/v0.22.8) | 30 | `9207672`，补指向 | `7c924fe75ce327b2d951e723d27d8d6d907b0c7fa7b0939b406bf3c36fdd75a9` |
| [v0.22.9](https://github.com/devmczdj/AeroPulse-Quest/releases/tag/v0.22.9) | 31 | `9207672`，补指向 | `27126f9cdebce2eea0378de6bd3e45061142df0b549bd2ccdf0f6f515e88ee1a` |
| [v0.23.0](https://github.com/devmczdj/AeroPulse-Quest/releases/tag/v0.23.0) | 32 | `9207672`，补指向 | `928659381bbee2a2900850b3461e432ac4c09833bb857a2f11798f98f7a7a3ae` |
| [v0.23.1](https://github.com/devmczdj/AeroPulse-Quest/releases/tag/v0.23.1) | 33 | `9207672`，补指向 | `e346d8f4d2b3811268b8d4723d8149f0e074a08ea59de287902bcb510b397273` |
| [v0.23.2](https://github.com/devmczdj/AeroPulse-Quest/releases/tag/v0.23.2) | 34 | `9207672`，补指向 | `600f03423f87e1dc08745e5c3df9667cfa4711b6e28d2d2439b7d3d4fbc6c41a` |
| [v0.23.3](https://github.com/devmczdj/AeroPulse-Quest/releases/tag/v0.23.3) | 35 | `9207672`，补指向 | `47b5eaf8ea99a84e535ebd1e434179a65a49c95f87a16eca15636fff270d703b` |
| [v0.23.4](https://github.com/devmczdj/AeroPulse-Quest/releases/tag/v0.23.4) | 36 | `9207672`，补指向 | `c661fbe5fa96cfa99f42ae0828d13004152baa2291e6dd5fec03526281e36d07` |
| [v0.23.5](https://github.com/devmczdj/AeroPulse-Quest/releases/tag/v0.23.5) | 37 | `9207672`，补指向 | `bc3680d1ee39437e47fa040a048696e15e987a57e648a03df7f4b6879bde572e` |
| [v0.23.6](https://github.com/devmczdj/AeroPulse-Quest/releases/tag/v0.23.6) | 38 | `9207672`，补指向 | `be7d719e3926a9a0d987fde6137c64477b03f6f524e4b2c831cbdd9c1333921d` |
| [v0.24.0](https://github.com/devmczdj/AeroPulse-Quest/releases/tag/v0.24.0) | 39 | `9207672` | `c52a1f11f0da73ac6380d589ae48b7d3b34a09c3767830900a6fa1eafc155831` |

## 校验记录

- 16/16 APK 的 ZIP 压缩数据完整，APK 签名可验证。
- 16/16 APK 的包名均为 `com.three.aeropulsemvp.glass`。
- 16/16 APK 的 `versionName` 与文件名一致，`versionCode` 从 24 连续到 39。
- 16/16 GitHub Release 都是预发布，附件状态均为 `uploaded`。
- 16/16 GitHub 服务器摘要、远程下载文件摘要和本地 SHA-256 三方一致。
- 下载核验使用的临时副本已删除。
