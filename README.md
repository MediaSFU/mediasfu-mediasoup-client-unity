# MediaSFU Mediasoup Client Unity

**MediaSFU Mediasoup Client Unity** is the Unity 2022.3+ mediasoup/WebRTC bridge used by MediaSFU Unity and custom Unity SFU apps. It provides C# transport abstractions, native plugin bindings, and platform binaries for Windows, macOS, Android, and iOS, with a Linux package layout available for teams building the native bridge on Linux.

---

<p align="center">
  <img src="https://www.mediasfu.com/logo192.png" width="100" alt="MediaSFU Logo">
</p>

<p align="center">
  <a href="https://www.mediasfu.com/">
    <img src="https://img.shields.io/badge/Website-4285F4?style=for-the-badge&logo=google-chrome&logoColor=white" alt="Website" />
  </a>
  <a href="https://www.mediasfu.com/forums">
    <img src="https://img.shields.io/badge/Community-Forum-blue?style=for-the-badge&logo=discourse&logoColor=white" alt="Community Forum" />
  </a>
  <a href="https://github.com/MediaSFU">
    <img src="https://img.shields.io/badge/GitHub-181717?style=for-the-badge&logo=github&logoColor=white" alt="GitHub" />
  </a>
</p>

<p align="center">
  <a href="https://opensource.org/licenses/MIT">
    <img src="https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square" alt="License: MIT" />
  </a>
  <a href="https://unity.com/releases/editor/whats-new/2022.3.0">
    <img src="https://img.shields.io/badge/Unity-2022.3%2B-black?style=flat-square&logo=unity&logoColor=white" alt="Unity 2022.3+" />
  </a>
  <a href="https://mediasoup.org/">
    <img src="https://img.shields.io/badge/mediasoup-client-Unity-00A7E1?style=flat-square" alt="mediasoup client Unity" />
  </a>
</p>

---

It is distinct from `com.mediasfu.unity`:

- `com.mediasfu.mediasoup-client-unity` is the client layer. It provides WebRTC device/transport/producer/consumer wrappers and native plugin bindings that can be used by any mediasoup-based SFU integration.
- `com.mediasfu.unity` is the MediaSFU SDK layer. It provides MediaSFU room creation, join flows, Socket.IO signaling, cloud/community endpoint handling, and app-level session state.

## Install

Install this package from Git through Unity Package Manager:

```text
https://github.com/MediaSFU/mediasfu-mediasoup-client-unity.git
```

For local development, install the checked-out package folder with Unity's "Add package from disk" flow.

## What it provides

- `IMediaSfuWebRtcEngine` and `IMediaSfuWebRtcDevice` abstractions.
- `MediaSfuNativePluginWebRtcEngine` P/Invoke wrapper over native plugin binaries.
- Local media and remote media bridge interfaces for produce/consume flows.
- Native plugin outputs for Windows x86_64, macOS, Android, and iOS under `Runtime/Plugins/`.
- Linux x86_64 has a package layout under `Runtime/Plugins/Linux/x86_64/` for projects that build and stage the native bridge for Linux players.
- Minimal transport DTOs for room RTP capabilities, remote producers, local participant identity, and operation results.

## Use with MediaSFU Unity

Use this package together with `com.mediasfu.unity` when you want MediaSFU room creation, room joining, Socket.IO signaling, moderation, recording controls, and Unity media transport in one project.

## Native plugins

`Runtime/Plugins/` contains the platform-specific binaries loaded by `MediaSfuNativePluginWebRtcEngine` at runtime.

- Windows x86_64: `Runtime/Plugins/Windows/x86_64/MediaSFUNativeBridge.dll`
- macOS: `Runtime/Plugins/macOS/libMediaSFUNativeBridge.dylib`
- Android: `Runtime/Plugins/Android/arm64-v8a/libMediaSFUNativeBridge.so` and `Runtime/Plugins/Android/x86_64/libMediaSFUNativeBridge.so`
- iOS: `Runtime/Plugins/iOS/MediaSFUNativeBridge.xcframework` and `Runtime/Plugins/iOS/WebRTC.xcframework`

Linux x86_64 projects can use `Runtime/Plugins/Linux/x86_64/` as the destination folder after building `libMediaSFUNativeBridge.so` against a matching desktop WebRTC and mediasoup client backend.

## Documentation

See [USAGE.md](USAGE.md) for integration examples.
