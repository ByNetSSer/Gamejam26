using UnityEngine;

// One switch for the optional curse rules of this game mode.
[DisallowMultipleComponent]
[RequireComponent(typeof(RoomManager))]
public sealed class EnemyCopiesMode : MonoBehaviour
{
    [Tooltip("Las maldiciones solo invocan enemigos o añaden copias; Dontsee y el corredor solo se invocan una vez.")]
    public bool enemyCopiesOnly = true;
}
