using System;
using UnityEngine;

namespace MenuUIBase.Audio
{
    [CreateAssetMenu(fileName = "AudioBank", menuName = "Menu UI Base/Audio Bank")]
    public sealed class AudioBank : ScriptableObject
    {
        [Serializable] public struct Entry { public string id; public AudioCue cue; }
        [SerializeField] Entry[] entries = Array.Empty<Entry>();
        public AudioCue Find(string id)
        {
            foreach (Entry entry in entries)
                if (string.Equals(entry.id, id, StringComparison.Ordinal)) return entry.cue;
            return null;
        }
        public void Play(string id)
        {
            AudioCue cue = Find(id);
            if (cue != null) AudioManager.Instance.Play(cue);
        }
        public void PlayAt(string id, Vector3 position)
        {
            AudioCue cue = Find(id);
            if (cue != null) AudioManager.Instance.PlayAt(cue, position);
        }
        void OnValidate()
        {
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (Entry entry in entries)
                if (string.IsNullOrWhiteSpace(entry.id) || !ids.Add(entry.id))
                    Debug.LogWarning("AudioBank: cada entrada necesita un ID único y no vacío.", this);
        }
    }
}
