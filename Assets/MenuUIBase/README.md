# Menu UI Base

A small, reusable Unity foundation for main-menu navigation, scene loading and
persistent audio. Runtime code is isolated in its own assembly and does not know
anything about the included demo scenes.

## Quick start

1. Open `Assets/MenuUIBase/Demo/Scenes/MenuUIBase_MenuDemo.unity`.
2. Enter Play Mode.
3. Change the audio sliders, open the second scene and return to the menu.
4. Replace the demo UI with your own art while keeping the runtime components.

The editor menu **Tools > Menu UI Base > Rebuild Demo Scenes** recreates both
demo scenes from code.

## Runtime components

- `AudioManager`: persistent music and sound-effects service with saved volumes.
- `AudioCue`: data asset for configuring reusable audio playback.
- `SceneLoader`: asynchronous scene loading and application exit.
- `PanelNavigator`: panel history with open, back and reset operations.
- `VolumeSliderBinding`: connects a UI Slider to master, music or SFX volume.
- `AudioCuePlayer`: plays an AudioCue from a UnityEvent.
- `PauseController`: optional pause-panel and time-scale handling.
- `SceneButton`: Inspector-friendly scene actions.

## Design notes

- Runtime, demo and editor code use separate assembly definitions.
- Services prevent duplicates and survive scene changes with `DontDestroyOnLoad`.
- Music does not restart when the requested clip is already playing.
- Volume preferences use namespaced PlayerPrefs keys.
- The demo generates tiny procedural clips at runtime; no audio files are bundled.
- The demo supports the Input System when installed and falls back to the classic
  UI input module when it is not available.

See `Documentation/USAGE.md` for integration examples.
