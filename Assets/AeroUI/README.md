# AeroUI

AeroUI is the small local uGUI layer used by the v0.8.0 HUD and front map. It is not a
third-party component library and it does not replace the existing Meta Aim /
XR Hands input.

Design source: `../../DESIGN.md` (project root); each release archives its own copy.

- `AeroUiTokens.cs` — colors, panel size, state colors.
- `AeroUiFonts.cs` — Inter TMP assets from `Resources/AeroUI/Fonts`.
- `AeroUiSprites.cs` — UI sprites from `Resources/AeroUI/Textures`.
- `AeroHudView.cs` — shared shell, DATA/MAP switching and button hover states.
- `AeroRouteDemo.cs` — invented north-up route with one shared session position state.
- `AeroMapView.cs` — route, current/next markers, labels and ADVANCE/RESET.
- `HudReveal.cs` — 180 ms opacity-only entrance.

Fonts: Inter, SIL Open Font License 1.1 (see `Resources/AeroUI/Fonts/OFL.txt`).
Glass base: JetStyle Apple Vision Pro UI Kit, MIT (see
`Resources/JetStyle/LICENSE.md`).

The glass is a static panorama-cubemap approximation, not live scene or
passthrough blur. `AeroGlassOpacityController` combines a highlight-biased
7×5 gaze sample with gamma-correct white-text contrast, then applies one
alpha multiplier to registered `AeroGlassSurface` images. Manual mode stores
a 5%–60% choice in PlayerPrefs.

MAP uses the existing real hand ray and fresh-pinch gate. HIDE/reopen and DATA/MAP switching retain the route position; restart resets it. No GPS, map service or real-world registration is connected.
