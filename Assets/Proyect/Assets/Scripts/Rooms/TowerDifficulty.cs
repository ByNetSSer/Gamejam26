using UnityEngine;

// Room count follows the selected difficulty; the remaining curves stay configurable.
[System.Serializable]
public sealed class TowerDifficulty
{
    [Tooltip("Porcentaje adicional de velocidad máxima por cada torre superada. 0,03 = 3 %.")]
    [SerializeField, Range(0f, 0.05f)] float speedPercentPerTower = 0.03f;
    [SerializeField, Min(0f)] float spawnChancePerTower = 0.1f;
    [SerializeField, Range(0f, 1f)] float initialLargeRoomChance = 0f;
    [SerializeField, Range(0f, 1f)] float largeRoomChancePerTower = 0.1f;
    [SerializeField, Min(1)] int mediumRoomsStartTower = 4;
    [SerializeField, Range(0f, 1f)] float initialMediumRoomChance = 0.1f;
    [SerializeField, Range(0f, 1f)] float mediumRoomChancePerTower = 0.07f;
    public int Rooms(int baseRooms, int tower) => Mathf.Clamp(baseRooms + Mathf.Max(0, tower - 1) * MenuUIBase.Core.RunDifficulty.ExtraRooms, 2, MenuUIBase.Core.RunDifficulty.MaximumRooms);
    public float SpeedBonus(int tower) => Mathf.Max(0, tower - 1) * Mathf.Clamp(speedPercentPerTower, 0f, 0.05f);
    public float SpawnMultiplier(int tower) => 1f + Mathf.Max(0, tower - 1) * spawnChancePerTower;
    public float LargeRoomChance(int tower) => Mathf.Clamp01(initialLargeRoomChance + Mathf.Max(0, tower - 1) * largeRoomChancePerTower);
    public float MediumRoomChance(int tower) => tower < mediumRoomsStartTower ? 0f : Mathf.Clamp01(initialMediumRoomChance + (tower - mediumRoomsStartTower) * mediumRoomChancePerTower);
}
