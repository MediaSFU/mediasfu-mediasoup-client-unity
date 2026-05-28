# Unity Native Plugin Outputs

`Runtime/Plugins/` is the package-side destination for the native binaries loaded by `MediaSfuNativePluginWebRtcEngine`.

Expected layout:

- `Windows/x86_64/MediaSFUNativeBridge.dll`
- `macOS/libMediaSFUNativeBridge.dylib`
- `Android/arm64-v8a/libMediaSFUNativeBridge.so`
- `Android/x86_64/libMediaSFUNativeBridge.so`
- `iOS/MediaSFUNativeBridge.xcframework`
- `iOS/WebRTC.xcframework`

Keep the platform and architecture folder that matches your Unity player build. If the matching binary is missing, the native media engine cannot attach.

Linux x86_64 builds should stage `libMediaSFUNativeBridge.so` in `Linux/x86_64/` after building the native bridge against a compatible Linux WebRTC and mediasoup client backend.

Build and package the bridge from the SDK repo with:

```sh
MEDIASFU_UNITY_CLIENT_PACKAGE_DIR="../mediasfu-mediasoup-client-unity" ./scripts/build_unity_native_bridge_package.sh
```

That script builds the shared standalone bridge from `unity-native-bridge/` and stages the native outputs that are available on the current build host.
