using UnityEngine;

// Provisional curve; tower 1 preserves the scene's configured size.
[System.Serializable]
public sealed class TowerDifficulty
{
    [SerializeField, Min(0)] int extraRoomsPerTower = 1;
    [SerializeField, Min(2)] int maximumRooms = 20;
    [Tooltip("Porcentaje adicional de velocidad máxima por cada torre superada. 0,03 = 3 %.")]
    [SerializeField, Range(0f, 0.05f)] float speedPercentPerTower = 0.03f;
    [SerializeField, Min(0f)] float spawnChancePerTower = 0.1f;
    [SerializeField, Range(0f, 1f)] float initialLargeRoomChance = 0f;
    [SerializeField, Range(0f, 1f)] float largeRoomChancePerTower = 0.1f;
    public int Rooms(int baseRooms, int tower) => Mathf.Clamp(baseRooms + Mathf.Max(0, tower - 1) * extraRoomsPerTower, 2, Mathf.Max(2, maximumRooms));
    public float SpeedBonus(int tower) => Mathf.Max(0, tower - 1) * Mathf.Clamp(speedPercentPerTower, 0f, 0.05f);
    public float SpawnMultiplier(int tower) => 1f + Mathf.Max(0, tower - 1) * spawnChancePerTower;
    public float LargeRoomChance(int tower) => Mathf.Clamp01(initialLargeRoomChance + Mathf.Max(0, tower - 1) * largeRoomChancePerTower);
}
