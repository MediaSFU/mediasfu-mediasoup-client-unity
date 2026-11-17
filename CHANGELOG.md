# Changelog

## 0.1.0-preview.2

- Added `MediaSfuIceRelayFallback`, which restarts ICE once through a server-offered TURN relay when a send or receive transport stays unconnected, reports the outcome to the server, and signals when the network blocks the call. The transport-backed local and remote media backends attach it automatically.
- Added the optional `IMediaSfuIceRelayTransportControl` engine capability. The native plugin engine maps it to the `getTransportIceState`, `updateIceServers`, and `restartIce` native bridge operations; native bridges without these operations keep the previous behavior. The native bridges packaged in this release do not implement them yet, so the relay restart turns on with a bridge build that does.
- The native plugin engine passes the relay servers at transport creation only when the server's relay mode is `always`. The relay offer is never part of the transport payload.

## 0.1.0-preview.1

- Initial Unity mediasoup/WebRTC client package for MediaSFU Unity and custom Unity SFU integrations.
- Added native plugin bindings and platform plugin outputs for Windows, macOS, Android, and iOS.
- Added the Linux x86_64 package layout for projects that build a Linux native bridge.
- Added device, transport, producer, consumer, local media, and remote media abstractions for Unity media flows.
