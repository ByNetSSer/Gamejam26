using System.Collections.Generic;
using UnityEngine;
namespace MenuUIBase.Audio
{
    // Playlist policy is separate from the two reusable music voices in AudioManager.
    [DisallowMultipleComponent]
    public sealed class MusicPlaylistPlayer : MonoBehaviour
    {
        struct Request { public MusicPlaylist Playlist; public int Priority; public long Order; }
        readonly Dictionary<string, Request> requests = new Dictionary<string, Request>();
        MusicPlaylist baseline, selected;
        AudioCue current;
        AudioManager audioManager;
        bool running;
        long order;
        public static MusicPlaylistPlayer Instance
        {
            get
            {
                var manager = AudioManager.Instance;
                if (manager == null) return null;
                var player = manager.GetComponent<MusicPlaylistPlayer>();
                return player != null ? player : manager.gameObject.AddComponent<MusicPlaylistPlayer>();
            }
        }
        public static bool TryGetExisting(out MusicPlaylistPlayer player)
        {
            player = null;
            if (!AudioManager.TryGetExisting(out var manager)) return false;
            player = manager.GetComponent<MusicPlaylistPlayer>();
            return player != null;
        }
        void Awake() => audioManager = GetComponent<AudioManager>();
        public void SetBase(MusicPlaylist playlist, bool clearOverrides = false)
        {
            if (clearOverrides) requests.Clear();
            baseline = playlist; running = true; Resolve();
        }
        public void RequestOverride(string key, MusicPlaylist playlist, int priority)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (playlist == null || !playlist.HasTracks) { ClearOverride(key); return; }
            if (requests.TryGetValue(key, out var existing) && existing.Playlist == playlist && existing.Priority == priority) return;
            requests[key] = new Request { Playlist = playlist, Priority = priority, Order = ++order };
            if (running) Resolve();
        }
        public void ClearOverride(string key) { if (requests.Remove(key) && running) Resolve(); }
        public void Suspend() { running = false; selected = null; requests.Clear(); }
        void Resolve()
        {
            MusicPlaylist next = baseline;
            int priority = int.MinValue; long latest = -1;
            foreach (var request in requests.Values)
                if (request.Priority > priority || (request.Priority == priority && request.Order > latest))
                { next = request.Playlist; priority = request.Priority; latest = request.Order; }
            if (next == selected && current != null && audioManager.CurrentMusicClip == current.Clip) return;
            if (next == selected && next != null && !next.HasTracks && current == null) return;
            selected = next;
            if (selected == null || !selected.HasTracks)
            {
                current = null;
                audioManager.FadePlaylistToSilence(selected != null ? selected.CrossfadeSeconds : 4f);
                return;
            }
            PlayNext(false);
        }
        void PlayNext(bool restart)
        {
            var cue = selected.Pick(current != null ? current.Clip : audioManager.CurrentMusicClip);
            if (cue == null) return;
            current = cue;
            float duration = Mathf.Min(selected.CrossfadeSeconds, cue.Clip.length * 0.5f);
            audioManager.PlayPlaylistTrack(cue.Clip, duration, cue.Volume, restart);
        }
        void Update()
        {
            if (!running || selected == null || current == null || audioManager.IsMusicTransitioning) return;
            float lead = Mathf.Min(selected.CrossfadeSeconds, current.Clip.length * 0.5f);
            if (audioManager.MusicRemaining <= lead) PlayNext(true);
        }
    }
}
