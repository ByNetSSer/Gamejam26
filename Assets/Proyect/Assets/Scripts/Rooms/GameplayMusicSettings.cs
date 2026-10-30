using MenuUIBase.Audio;
using UnityEngine;
[CreateAssetMenu(menuName = "Game/Gameplay Music Settings")]
public sealed class GameplayMusicSettings : ScriptableObject
{
    public MusicPlaylist safeRooms;
    public MusicPlaylist calm;
    public MusicPlaylist intense;
    public MusicPlaylist lastMinute;
    [Min(1)] public int intenseFromRoom = 3;
    [Min(1f)] public float urgentSeconds = 60f;
}
