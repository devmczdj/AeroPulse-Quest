# GameActivity 键盘兼容修复

版本：v0.24.6（Android code 45），2026-11-20。

## 现象

Quest 2 触发系统键盘时，Unity 报告：

```text
OPEN requested
OPEN visible status=Visible visible=True
```

但系统服务同时返回：

```text
ImeTracker ... onFailed at PHASE_IME_ON_SHOW_SOFT_INPUT_TRUE
```

所以应用误以为键盘已经接管输入，而系统实际没有绘制键盘。

## 根因

当前 APK 使用 Unity 6 的 `UnityPlayerGameActivity`。Quest OS 2.7 上存在已知兼容问题：GameActivity 会让输入框请求系统键盘后立刻失败。Meta 社区记录的建议修复是切换 Android Application Entry Point 为传统 Activity：

[Update 2.7 causes input fields to fail in Unity App](https://communityforums.atmeta.com/questions/179235/replies/1205176)

## 实现

- `ProjectSettings.asset` 的 `androidApplicationEntry` 改为 `1`。
- `AeroKeyboardSetup.Configure()` 和 `AeroUiBuild.Build()` 都显式设置 `AndroidApplicationEntry.Activity`。
- NativeKeyboard 回归要求 Application Entry Point 必须为 Activity。
- 保留 v0.24.5 的3秒键盘打开超时、取消按钮和射线恢复路径。

## 构建证据

APK Manifest：

```text
com.unity3d.player.UnityPlayerActivity
```

构建结果：

- 0错误，4个既有Editor警告
- APK 50,168,865字节
- SHA-256：`ed238ff1247ed74ebb7e687e1a4624e6322b18eeda788d8dda526e834accb5b9`
- 签名、ZIP完整性和zipalign通过
- Quest 2安装与冷启动通过

最终仍需人工确认系统键盘是否实际出现，以及中文输入法是否可正常使用。
