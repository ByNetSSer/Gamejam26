using System;
using System.Collections.Generic;
using MenuUIBase.Audio;
using UnityEngine;
[RequireComponent(typeof(RoomManager))]
public sealed class GameplayMusicDirector : MonoBehaviour
{
    [SerializeField] GameplayMusicSettings settings;
    RoomManager rooms;
    RoomEnemyRoster roster;
    bool? intenseSelected;
    bool ended;
    bool inSafeRoom = true;
    readonly List<IDisposable> subscriptions = new List<IDisposable>();
    void Awake()
    {
        rooms = GetComponent<RoomManager>();
        roster = GetComponent<RoomEnemyRoster>();
        if (settings == null) settings = Resources.Load<GameplayMusicSettings>("Music/GameplayMusicSettings");
    }
    void OnEnable()
    {
        if (settings == null) return;
        ended = false;
        MusicPlaylistPlayer.Instance.SetBase(settings.safeRooms, true);
        subscriptions.Add(rooms.Events.Subscribe<RoomEnteredEvent>(e =>
        {
            inSafeRoom = e.Context.Kind != RoomKind.Intermediate;
            SelectBase();
        }));
        subscriptions.Add(rooms.Events.Subscribe<RunLostEvent>(_ => End()));
        subscriptions.Add(rooms.Events.Subscribe<RunWonEvent>(_ => End()));
        subscriptions.Add(rooms.Events.Subscribe<TowerGenerationFailedEvent>(_ => End()));
    }
    void SelectBase()
    {
        if (ended) return;
        if (inSafeRoom)
        {
            intenseSelected = null;
            // Safe-room music replaces gameplay and any temporary combat requests atomically.
            MusicPlaylistPlayer.Instance.SetBase(settings.safeRooms, true);
            return;
        }
        bool intense = rooms.TowerNumber >= Mathf.Max(1, settings.intenseFromTower) &&
            roster != null && roster.AccumulatedEnemyCount >= Mathf.Max(1, settings.intenseFromAccumulatedEnemies) &&
            settings.intense != null && settings.intense.HasTracks;
        intenseSelected = intense;
        var playlist = intense ? settings.intense : settings.calm;
        MusicPlaylistPlayer.Instance.SetBase(playlist);
    }
    void Update()
    {
        if (ended || settings == null) return;
        if (!inSafeRoom)
        {
            if (roster == null) roster = GetComponent<RoomEnemyRoster>();
            bool intense = rooms.TowerNumber >= Mathf.Max(1, settings.intenseFromTower) &&
                roster != null && roster.AccumulatedEnemyCount >= Mathf.Max(1, settings.intenseFromAccumulatedEnemies) &&
                settings.intense != null && settings.intense.HasTracks;
            if (intenseSelected != intense) SelectBase();
        }
        if (!inSafeRoom && rooms.TimerRunning && rooms.TimeRemaining <= settings.urgentSeconds)
            MusicPlaylistPlayer.Instance.RequestOverride("tower.last-minute", settings.lastMinute, 100);
        else MusicPlaylistPlayer.Instance.ClearOverride("tower.last-minute");
    }
    void End() { ended = true; AudioManager.Instance.StopMusic(1.6f); }
    void OnDisable()
    {
        foreach (var subscription in subscriptions) subscription.Dispose();
        subscriptions.Clear();
        if (AudioManager.HasInstance) MusicPlaylistPlayer.Instance.Suspend();
    }
}
