---
version: alpha
name: AeroPulse AeroUI
description: A compact, instrument-like sports HUD for Meta Quest, expressed as design tokens plus a small local uGUI component layer.
colors:
  ink: "#F5FAF9"
  secondary: "#B2C4C3"
  tertiary: "#778B8C"
  mint: "#7BE8C4"
  amber: "#FFB45C"
  glass-top: "#121A1E"
  glass-bottom: "#04080B"
  hairline: "rgba(255,255,255,0.12)"
  chip-fill: "rgba(9,13,15,0.62)"
  chip-stroke: "rgba(255,255,255,0.18)"
  button-fill: "rgba(255,255,255,0.035)"
  button-stroke: "rgba(255,255,255,0.16)"
typography:
  display:
    fontFamily: Inter
    fontWeight: 600
    fontSize: 95
    letterSpacing: -0.02em
    lineHeight: 1.05
  metric:
    fontFamily: Inter
    fontWeight: 600
    fontSize: 42
    letterSpacing: -0.02em
    lineHeight: 1.05
  wordmark:
    fontFamily: Inter
    fontWeight: 600
    fontSize: 19.5
    letterSpacing: 0.12em
  label:
    fontFamily: Inter
    fontWeight: 500
    fontSize: 13.5
    letterSpacing: 0.12em
  micro:
    fontFamily: Inter
    fontWeight: 500
    fontSize: 12
    letterSpacing: 0.1em
rounded:
  panel: 36
  pill: 999
spacing:
  panel-padding: 36
  header-height: 93
  content-height: 256
  footer-height: 81
  hairline: 1
components:
  glass-panel:
    width: 760
    height: 430
    radius: 36
    layers: [shadow, glass-base, graphite-tint, sheen, stroke]
    background: "static panorama cubemap, blurred by mip level; not live blur"
  status-chip:
    height: 34
    radius: 17
    fill: chip-fill
    stroke: chip-stroke
    text: micro, mint
  hero-metric:
    label: label, secondary
    value: display, ink
    unit: "13-14.5, medium, secondary"
  metric-row:
    label: label, secondary
    value: metric, ink
    unit: micro, tertiary
    divider: hairline
  ghost-button:
    width: 144
    height: 48
    radius: 24
    fill: button-fill
    stroke: button-stroke
    label: micro, ink
    hover: mint text plus mint 22% fill
  effort-meter:
    segments: 5
    segment: "13 x 4, pill"
    active: mint
    inactive: "white 14%"
---

# AeroPulse AeroUI

## 1. 设计意图

这不是一块“数据看板”，而是一块**佩戴者抬头就能读的仪表**。

v0.6.0 的三个等宽指标块把所有信息放在同一层级，视觉上更像 Web dashboard。v0.7.0 只保留一个视觉锚点：**心率**。配速和距离退到右列，像仪表台上的次级读数；HIDE 退成 ghost 控件，不再和主数据争夺注意力。

一句话：**一个主读数、一列辅助读数、一个明确动作。**

## 2. 与 v0.6.0 的差异

| 维度 | v0.6.0 | v0.7.0 |
|---|---|---|
| 信息结构 | 三个等宽 tile | 左侧 hero 心率 + 右侧两行指标 |
| 字体 | TMP 默认 Liberation Sans | Inter Regular / Medium / SemiBold |
| 主数字 | 68 | 95 |
| 辅助数字 | 68 | 42 |
| 标签 | 18，弱层级 | 12.8–13.5，强字距，明确弱层级 |
| 底板 | 单层深色玻璃 | 阴影 + 玻璃 + 石墨 tint + sheen + 描边 |
| 状态披露 | 大号胶囊 | 紧凑 chip，始终可见 |
| 关闭按钮 | 大面积青绿色填充 | ghost pill + 细描边 + hover 才变 mint |
| 入场 | 无 | 180 ms 仅透明度渐入 |
| 输入/跟随 | 自由射线 + 捏合 + 柔和跟随 | 完全不变 |

## 3. 构图

面板固定为 **760 × 430** 设计单位，四周 36 内边距。

```text
┌──────────────────────────────────────────────────────┐
│  ◉ AEROPULSE                       ● SIMULATED DATA  │  header 93
│    RUN OVERVIEW                                      │
│  ──────────────────────────────────────────────────  │
│                                                      │
│  HEART RATE          │  PACE                         │
│  142  BPM            │  5:20  /KM                    │  content 256
│  EFFORT ▬▬▬▭▭ STEADY │  ─────────────────            │
│  ~~~~ pulse ~~~~     │  DISTANCE                     │
│                      │  3.20  KM                     │
│  ──────────────────────────────────────────────────  │
│  ◉ SOFT FOLLOW ON                        HIDE  ⌄     │  footer 81
└──────────────────────────────────────────────────────┘
```

- 左侧 hero 占约 55% 宽，心率数字是唯一的大字号。
- 右侧列宽约 40%，用一条 1 px 竖线分开；PACE 与 DISTANCE 之间用 1 px 横线分开。
- 页脚只保留跟随状态和关闭动作，不再放长句操作说明。
- 所有内容都在 36 px 安全边距内；HIDE 的碰撞体为 220 × 90，远大于视觉尺寸。

## 4. 颜色

| Token | 值 | 用途 |
|---|---|---|
| `ink` | `#F5FAF9` | 主数字、主标签、按钮文字 |
| `secondary` | `#B2C4C3` | 次级标签、单位、页脚 |
| `tertiary` | `#778B8C` | 最弱辅助信息 |
| `mint` | `#7BE8C4` | 在线/正常、激活段、hover |
| `amber` | `#FFB45C` | 预留给未来真实心率区间；本轮不用于装饰 |
| `glass-top` / `glass-bottom` | `#1E2B30` → `#080E12` | 石墨玻璃的上下渐变 |
| `hairline` | 白 12% | 分隔线 |

规则：

- mint 只表示“正常/可用/被选中”，不拿来大面积铺底。
- 心率数字保持白色；未来接入真实传感器后，才考虑用 amber 表示区间，而不是现在虚构区间。
- 红色不使用。HUD 不是告警面板。

## 5. 字体

Inter，SIL Open Font License 1.1。

| 角色 | 字号 | 字重 | 字距 | 示例 |
|---|---|---|---|---|
| display | 95 | SemiBold 600 | -2% | `142` |
| metric | 42 | SemiBold 600 | -2% | `5:20` / `3.20` |
| wordmark | 19.5 | SemiBold 600 | +12% | `AEROPULSE` |
| label | 12.8–13.5 | Medium 500 | +12% | `HEART RATE` / `PACE` |
| micro | 11.5–12.8 | Medium 500 | +5–12% | `SIMULATED DATA` / `SOFT FOLLOW` |
| unit | 12–14.5 | Medium 500 | +10% | `BPM` / `/KM` |

数字使用 Inter 的默认等宽数字形态；TMP 不额外启用 tabular figures。当前固定样例不涉及滚动数字，暂不引入等宽数字表。

## 6. 组件

### Glass panel

五层，从后到前：

1. **Shadow**：840 × 510 的软阴影，向下偏移 8，白色乘 0.78，避免面板像贴纸。
2. **Glass base**：JetStyle `RoundedRect` + `AeroPulse/JetStyleGlass`，采样静态全景 cubemap 的 mip 3.2，作为磨砂近似。
3. **Graphite tint**：760 × 430 的深色渐变，保证在亮天空前文字仍有对比度。
4. **Sheen**：顶部白色渐隐，最大约 12%。
5. **Stroke**：1 px 白 26% 外框 + 顶部高光 + 底部暗边。

边界：这是**静态全景采样**，不是实时相机模糊，也不是 Passthrough 磨砂。真实世界背景下的效果需要真机与 Passthrough 版本另行验证。

### Status chip

- 190 × 34，深色 fill + 1 px 白 18% 环。
- 左侧 7 px mint 圆点。
- 文案固定为 `SIMULATED DATA`，不可被隐去。
- 这是口径钉子：演示数据必须始终可识别。

### Hero metric

- 标签在上，数字在中，单位在数字右侧偏下。
- 数字 95，左对齐；单位不跟数字同一基线，而是略低于数字中线，形成仪表感。
- 数字下方保留 `EFFORT` 五段条和 `STEADY` 文案；段条只表达定性状态，不冒充真实心率区间。

### Metric row

- 标签 12.2 / 次级色；数值 42 / 主色；单位 11.5 / 三级色。
- 两行之间 1 px 分隔线；数值左对齐，单位紧随数值右侧。
- 不使用卡片套卡片；右列直接落在玻璃上。

### Ghost button

- 144 × 48，24 圆角，白 3.5% fill + 白 16% 环。
- 默认白字；射线悬停/命中时环与文字变 mint，fill 提到 mint 22%。
- 视觉按钮小，碰撞体 220 × 90，保持手部指向容错。
- 右侧 `⌄` 表示“收起”，不新增第二个关闭图标。

### Motion

- 打开时只做 180 ms CanvasGroup 透明度渐入，不做缩放、位移或持续呼吸。
- 柔和跟随参数保持 v0.6.0：18° 死区、0.45 s 延迟、1.2 m/s 平移、55°/s 旋转；指向面板或捏合时暂停。
- 不为“看起来高级”增加持续动画；VR 中持续运动容易干扰阅读。

## 7. 交互与可读性

- 保留自由射线、扩大命中区、同手捏合确认；不改输入栈。
- 面板根节点继续使用 760 × 430 的 BoxCollider；HIDE 单独使用 220 × 90 的 BoxCollider。
- 所有装饰图层 `raycastTarget = false`，不会挡住 HIDE 或入口。
- 主数字与背景的对比度优先于玻璃透明度；亮天空场景下宁可加深 tint，也不牺牲数字。
- 文案不依赖颜色单独传达状态：mint 段条旁始终有 `STEADY` 文本。

## 8. 实现映射

| 设计层 | 文件 |
|---|---|
| tokens | `Assets/AeroUI/AeroUiTokens.cs` |
| fonts | `Assets/AeroUI/AeroUiFonts.cs` |
| 程序化/生成的视觉资产 | `Assets/AeroUI/AeroUiSprites.cs`、`Assets/Resources/AeroUI/Textures/` |
| 组件与布局 | `Assets/AeroUI/AeroHudView.cs` |
| 入场 | `Assets/AeroUI/HudReveal.cs` |
| 接入现有手势 | `Assets/PinchHudExperiment.cs` |
| 玻璃 shader | `Assets/Resources/JetStyle/AeroGlass.shader` |
| 字体资产 | `Assets/Resources/AeroUI/Fonts/Inter-*_SDF.asset` |

`DESIGN.md` 是设计源；`AeroUI` 是这份源在 Unity uGUI 里的轻量实现。它不是第三方组件库，也不试图替代 Meta Horizon OS UI Set。

## 9. Do / Don't

**Do**

- 用一个主数字建立视觉锚点。
- 先保证 142 / 5:20 / 3.20 在真实头显里可读，再谈玻璃折射。
- 把 `SIMULATED DATA` 当作固定组件，不当临时标注。
- 用留白和字重分层，而不是继续加卡片边框。

**Don't**

- 不要回到三个等宽 tile。
- 不要把 HIDE 做成大面积高饱和按钮。
- 不要在数字上叠加噪点、强折射或彩色渐变。
- 不要把静态 cubemap 说成实时模糊或 Passthrough 磨砂。
- 不要为了视觉引入 Apple SF Pro；Inter 已足够接近，且许可清晰。

## 10. 证据边界

- v0.7.0 的预览是 **Unity 编辑器相机预览**，不是 Quest 实机截图。
- 本轮未连接头显，未安装 v0.7.0；实机可读性、双眼一致性、长时间舒适度和 GPU 性能待测。
- 玻璃仍为静态全景近似；真实 Passthrough、动态手部和真实世界背景下的模糊未实现。
- 数据仍为固定模拟值：142 bpm、5:20 min/km、3.20 km、STEADY。没有接真实传感器、GPS 或路线。
- 设计 token 是当前实现口径；后续若接真实传感器或地图，先改 `DESIGN.md`，再改组件。


## v0.7.1 Quest 清晰度修正（2026-11-13）

本节覆盖前文 v0.7.0 的小字号/ghost 按钮规则：HIDE 使用 Inter SemiBold 24、居中、间距 2，按钮 176×64，背景深色 90% alpha；删除小下箭头。所有其余标签至少 16，TMP Extra Padding 开启。装饰心电细线移除、分隔线至少 2.5。

移动 URP：renderScale 1.0、4x MSAA；AeroUI 装饰 PNG 使用 mipmaps + Trilinear + anisotropy 4。SDF atlas 保持无 mipmap。增加像素预算，需 Quest 短测可读性/闪纹/流畅度，不将配置调整视为实机问题已修复。输入和柔和跟随参数保持原样。


## v0.7.2 面板透明度微调（2026-11-13）

仅更改 GlassBase Image alpha：1.0→0.85，Tint Image alpha：1.0→0.5。玻璃 Shader 的0.88 alpha、PNG 像素、文字/按钮/描边/阴影、字体与尺寸、输入/跟随及移动渲染质量不变；不降低根 CanvasGroup 的常态 alpha，渐入结束仍为1。

按中心采样的Tint alpha约0.686、Sheen约0.0078估算，原背景中心透射权重约3.7%，调整后约16.4%；这是源alpha叠加的中心近似，不是头显光学透过率或整卡统一透明度。保持可读性背板，待亮天空/暗山体/草地和转头时的实机体验确认。


## v0.8.0 前方地图（2026-11-13，第2步）

沿用v0.7.2透明底板和v0.7.1文字/渲染设置，卡片760×430、缩放0.0015、柔和跟随参数不变。共用页眉/SIMULATED标记/页脚；底部MAP/DATA切换两种内容，HIDE保持原位置/尺寸/命中区。

地图是5个虚构站点的北向上示意路线：TRAILHEAD→RIDGE→LOOKOUT→SPRING→FINISH。圆环=当前位置，菱形=下一站（同时有文字，颜色非唯一编码）。右栏显示当前位置/下一站与DEMO进度，ADVANCE推进，抵达终点变RESET。单一AeroRouteDemo实例驱动标记/文字，关闭、重开和MAP/DATA切换保留当前会话的位置；重启应用回到起点。未接GPS/地图服务，不声称真实方位/路网/地物配准。

地图标签至少16、右栏站点22、路线线宽5、稀疏辅助线2.5；新增按钮字号22，沿用捏合门控与hover反馈。按钮碰撞区互不重叠，隐藏地图的ADVANCE碰撞体随层级关闭。天空模式、手腕与拖拽不在本轮。


## v0.9.0 左手手背轻触入口（2026-11-13）

用户将手背触碰入口提前到天空地图之前：左手手背58×38mm小HUD片，右手IndexTip靠近并轻触后打开已有HUD；打开后可放下双手，原射线负责面板按钮。收起后入口回到左手，默认前方光球与未打开面板时的射线隐藏；不增加系统菜单或自动抬腕召唤。

跟随左手Palm位置，使用XR Hands root rotation的+Y手背方向（官方包XRHandOrientationUtility定义PalmDirection=-Y），法线偏移14mm；手背朝向头显且距离0.2–1.0m时显示。接触范围58×38mm，指尖在法线方向离开超过28mm后武装，进入-6～14mm接触带并连续60ms后触发；以上均为实验参数，需真机校准，并非硬件检测到的真实皮肤接触。追踪无效/失焦/关闭组件会清空武装，恢复时先离开再触碰。

新入口无Collider/射线选取；不要求捏合。旧光球只保留在Editor回归夹具中，Android包无入口模式开关。主卡、地图和状态保持沿用v0.8.0；尚未加入天空总览。


## v0.9.1 手背开关与会话设置（2026-11-14）

手背按钮默认显示高度35mm，可选25/35/45mm。显示位置与Palm+14mm接触代理分离，提高显示不改变实际手背触碰区域；原接触带和离开后武装门控保留。按钮在大卡打开时继续显示，标注HIDE，再次离开/轻触可收起；持续接触不重复切换，手追无效/失焦仍隐藏与清空武装。

旧LateUpdate对任意overCard或pressed都会暂停跟随，容易因射线停在卡片空白或自然捏合一直不动。新规则只在实际Button被指向、且卡片仍在视线35度内时暂停；看开超过35度不被旧按钮目标锁住。

页脚SETTINGS进入同卡设置页，DONE回上一个数据/地图页面。FOLLOW MODE依次SOFT FOLLOW（18度/0.45s）、QUICK FOLLOW（10度/0.15s）、FIXED HERE（当前场景位置固定）；PANEL DISTANCE循环1.8/2.2/1.4m，改变后回到当前前方；HAND PAD HEIGHT循环NORMAL35/HIGH45/LOW25mm。RECENTER回面前，RESET DEFAULTS恢复SOFT/1.8m/35mm。所有值为会话内设置，重启恢复默认，不使用跨启动持久化。透明底板、字体、主地图状态与射线新捏合门控不变。


## v0.10.0 天空区域总览（2026-11-14）

独立透明SkyMapLayer，无大底板；地形等高轮廓、水系与支流为自编示意地理图，少量站名与当前/下一站标记，当前路线优先。保留原全景材质/颜色，放在头显前上方天空空白；打开时按当前朝向放置，此后保持会话世界位置。DETAIL回前方细看，RECALL按当前朝向召回，HIDE收起；与前方地图共用AeroRouteDemo，同步位置/终点/重置，不复制状态。

站点与地理轮廓为虚构示例，常显SCHEMATIC REGION / SIMULATED POSITION，无经纬度/道路/山峰配准声明。实际视野面积和可读性待最终Quest验收；Editor相机预览仅作实现证据。
