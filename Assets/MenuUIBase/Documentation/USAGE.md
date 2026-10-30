# Integration guide

## Persistent audio

`AudioManager` is created automatically before the first scene loads. You can
also place one in a bootstrap scene when you want to assign preconfigured
AudioSource components. Duplicate instances destroy themselves.

Create reusable cues from **Assets > Create > Menu UI Base > Audio Cue** and
assign them to `AudioCuePlayer`, or call the service directly:

```csharp
using MenuUIBase.Audio;

AudioManager.Instance.PlayMusic(menuMusic, true, 0.5f);
AudioManager.Instance.PlaySoundEffect(buttonClick);
```

The stored keys are:

- `MenuUIBase.Audio.Master`
- `MenuUIBase.Audio.Music`
- `MenuUIBase.Audio.SoundEffects`

## Panel navigation

Add `PanelNavigator` to a UI controller object. Assign the initial page and all
pages in the Inspector. Buttons can call:

- `Open(GameObject)` to enter a page.
- `Back()` to return to the previous page.
- `ResetToInitialPanel()` to discard navigation history.
- `CloseAll()` to hide every registered page.

## Scene loading

Use `SceneButton` from a UnityEvent or call:

```csharp
using MenuUIBase.Scenes;

SceneLoader.Instance.LoadScene("Gameplay");
```

Add every target scene to Build Settings. The loader ignores duplicate requests,
resets `Time.timeScale` and exposes progress through `LoadProgressChanged`.

## Packaging

Export only `Assets/MenuUIBase` for an Asset Store-style package. The runtime has
no dependency on the demo. Projects without the Input System can delete the Demo
folder or rebuild it after import; the editor builder selects the available UI
input module.

For a production release, replace the procedural demo audio and legacy UI Text
components with the target project's font and art system.
