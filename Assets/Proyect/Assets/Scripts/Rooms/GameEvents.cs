using UnityEngine;

public enum RoomKind { Initial, Intermediate, Final }
public enum RoomExitReason { Advanced, Completed, Defeated }
public enum RunLossReason { PlayerHealthDepleted, TimeExpired, Flupin, Dontsee, MaskCharger, Crusher, HazardousSurface }

// Scene references are valid during dispatch; retain Index/Kind for long-term records.
public readonly struct RoomContext
{
    public readonly RoomDefinition Room;
    public readonly int Index;
    public readonly int TotalRooms;
    public readonly RoomKind Kind;
    public RoomContext(RoomDefinition room, int index, int totalRooms, RoomKind kind)
    { Room = room; Index = index; TotalRooms = totalRooms; Kind = kind; }
}
public readonly struct TowerStartedEvent
{
    public readonly int TotalRooms;
    public readonly PlayerController Player;
    public TowerStartedEvent(int totalRooms, PlayerController player) { TotalRooms = totalRooms; Player = player; }
}
public readonly struct RoomEnteredEvent
{
    public readonly RoomContext Context;
    public RoomEnteredEvent(RoomContext context) { Context = context; }
}
public readonly struct RoomExitedEvent
{
    public readonly RoomContext Context;
    public readonly RoomExitReason Reason;
    public RoomExitedEvent(RoomContext context, RoomExitReason reason) { Context = context; Reason = reason; }
}
public readonly struct TowerCompletedEvent
{
    public readonly int TotalRooms;
    public readonly long Score;
    public TowerCompletedEvent(int totalRooms, long score) { TotalRooms = totalRooms; Score = score; }
}
public readonly struct RunLostEvent
{
    public readonly RoomContext Context;
    public readonly long Score;
    public readonly RunLossReason Reason;
    public RunLostEvent(RoomContext context, long score, RunLossReason reason) { Context = context; Score = score; Reason = reason; }
}
// Reserved for explicit final victory, not emitted merely for completing one tower.
public readonly struct RunWonEvent
{
    public readonly int TotalRooms;
    public readonly long Score;
    public RunWonEvent(int totalRooms, long score) { TotalRooms = totalRooms; Score = score; }
}

// A configuration failure is not a player defeat.
public readonly struct TowerGenerationFailedEvent
{
    public readonly string Message;
    public readonly int RoomsEntered;
    public TowerGenerationFailedEvent(string message, int roomsEntered) { Message = message; RoomsEntered = roomsEntered; }
}
