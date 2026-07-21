# Duplicate controller-device handling

NexaStation now has a controller-device resolver between backend enumeration and semantic UI actions.

## Goal

One physical controller must not generate duplicate UI actions through multiple representations.

The common Phase 12 risk is:

1. A physical DualSense appears through a Windows controller path.
2. A translation layer also exposes it as an Xbox/XInput-compatible controller.
3. Both paths produce Confirm/Cancel/navigation events.

## Resolver behavior

- Candidate devices are normalized into `ControllerBackendDevice`.
- The resolver builds a non-personal identity key from bounded evidence such as vendor ID, product ID, backend, display name, and virtual-device markers.
- If one physical and one virtual representation share identity evidence, the resolver chooses one active device and suppresses the duplicate by default.
- Automatic mode prefers the physical PlayStation representation over a virtual Xbox representation when both are present.
- A user preference can choose physical, virtual Xbox, or disabled representation.
- Two physical controllers are not collapsed merely because they share a model name.

## Privacy boundaries

The resolver does not store serial numbers, Bluetooth MAC addresses, raw HID paths, or controller logs.

When evidence is insufficient, diagnostics must show the devices separately and let the user choose.
