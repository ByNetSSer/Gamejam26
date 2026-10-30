using System.Collections.Generic;
using UnityEngine;
namespace MenuUIBase.Audio
{
    [CreateAssetMenu(menuName = "Menu UI Base/Music Playlist")]
    public sealed class MusicPlaylist : ScriptableObject
    {
        [SerializeField] AudioCue[] tracks;
        [SerializeField, Min(0f)] float crossfadeSeconds = 4f;
        public float CrossfadeSeconds => Mathf.Max(0f, crossfadeSeconds);
        public AudioCue Pick(AudioClip previous)
        {
            // Deduplicate by clip: two cues referencing the same song are not alternatives.
            var valid = new List<AudioCue>();
            var clips = new HashSet<AudioClip>();
            if (tracks != null) foreach (var cue in tracks)
                if (cue != null && cue.Channel == AudioChannel.Music && cue.Clip != null && clips.Add(cue.Clip)) valid.Add(cue);
            if (valid.Count == 0) return null;
            if (valid.Count > 1) valid.RemoveAll(cue => cue.Clip == previous);
            return valid[Random.Range(0, valid.Count)];
        }
        public bool HasTracks
        {
            get
            {
                if (tracks != null) foreach (var cue in tracks)
                    if (cue != null && cue.Channel == AudioChannel.Music && cue.Clip != null) return true;
                return false;
            }
        }
    }
}
