# MediaSFU Mediasoup Client Unity Usage

This package supplies the generic mediasoup/WebRTC client layer for Unity.

Use it directly when your application already has its own mediasoup signaling service and only needs a Unity WebRTC/media transport client. Use `com.mediasfu.unity` when you want MediaSFU room creation, join flows, Socket.IO signaling, and MediaSFU cloud/community behavior.

## Install

Install through Unity Package Manager using the Git URL:

```text
https://github.com/MediaSFU/mediasfu-mediasoup-client-unity.git
```

The package depends on Unity's Newtonsoft JSON package:

```json
"com.unity.nuget.newtonsoft-json": "3.2.2"
```

## Create A Native Plugin Device

```csharp
using MediaSFU.Unity;

var result = MediaSfuNativePluginWebRtcEngine.TryCreateWebRtcDevice(
    new MediaSfuNativePluginWebRtcEngineOptions
    {
        EnableVerboseLogging = true
    });

if (!result.Success)
{
    UnityEngine.Debug.LogError(result.Detail);
    return;
}

IMediaSfuWebRtcDevice device = result.Value;
```

## Load RTP Capabilities

```csharp
await device.EnsureCapabilitiesLoadedAsync(roomRtpCapabilitiesJson);
```

## Send Transport Flow

```csharp
await device.Engine.InitializeSendTransportAsync(sendTransport);
var connectParametersJson = await device.Engine.CreateSendTransportConnectParametersJsonAsync();

// Send connectParametersJson to your mediasoup signaling service, then complete:
await device.Engine.CompleteSendTransportConnectAsync(success: true);

var produceRequest = await device.Engine.CreateProduceRequestAsync(
    MediaSfuTrackKind.Audio,
    new MediaSfuLocalMediaRequest
    {
        Room = new MediaSfuRoom { RoomName = "demo-room" },
        RoomValidation = new MediaSfuRoomValidation
        {
            RtpCapabilitiesJson = roomRtpCapabilitiesJson,
            SecureCode = secureCode
        },
        LocalParticipant = new MediaSfuParticipant
        {
            ParticipantId = participantId,
            DisplayName = displayName
        }
    });

// Send produceRequest.RtpParametersJson to your signaling service and bind the returned producer id:
await device.Engine.BindProducerAsync(MediaSfuTrackKind.Audio, producerId);
```

## Receive Transport Flow

```csharp
var remoteProducer = new MediaSfuRemoteProducerEvent
{
    ProducerId = remoteProducerId,
    IsLevel = "1"
};

await device.Engine.InitializeReceiveTransportAsync(remoteProducer, recvTransport);
var recvConnectParametersJson = await device.Engine.CreateReceiveTransportConnectParametersJsonAsync(remoteProducer);

// Send recvConnectParametersJson to your signaling service, then complete:
await device.Engine.CompleteReceiveTransportConnectAsync(remoteProducer, success: true);

await device.Engine.BindConsumerAsync(
    remoteProducer,
    new MediaSfuConsumeResponse
    {
        ConsumerId = consumerId,
        ProducerId = remoteProducerId,
        Kind = "video",
        RtpParametersJson = consumerRtpParametersJson
    });
```

## Native Plugins

`Runtime/Plugins/` contains the platform binaries consumed by `MediaSfuNativePluginWebRtcEngine`:

- Windows: `Windows/x86_64/MediaSFUNativeBridge.dll`
- Android: `libMediaSFUNativeBridge.so`
- iOS: `MediaSFUNativeBridge.xcframework` and `WebRTC.xcframework`
- macOS: `libMediaSFUNativeBridge.dylib`

Keep the matching binary for your target Unity player platform and architecture. If the runtime folder for a target is missing, the native engine cannot attach.

For Linux x86_64 players, use `Linux/x86_64/` as the destination folder after building `libMediaSFUNativeBridge.so` against a compatible Linux WebRTC and mediasoup client backend.

Regenerate plugin outputs from the SDK repo using `scripts/build_unity_native_bridge_package.sh` when native bridge sources change.
