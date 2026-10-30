using UnityEngine;

// Temporary status display until the tower and shop UI exist.
public sealed class RoomDebugView : MonoBehaviour
{
    [SerializeField] RoomManager manager;
    public void SetManager(RoomManager value) => manager = value;
    void OnGUI()
    {
        if (manager == null) return;
        GUI.Box(new Rect(350, 12, 270, 65), manager.Status);
        if (manager.IsFinished && !manager.IsLost) GUI.Label(new Rect(360, 40, 250, 20), manager.IsWon ? "Victoria conseguida" : manager.IsFinalTower ? "Activa el botón de victoria" : "Visita la tienda");
        else if (manager.IsLost) GUI.Label(new Rect(360, 40, 250, 20), "Vida agotada; reinicio pendiente");
    }
}
