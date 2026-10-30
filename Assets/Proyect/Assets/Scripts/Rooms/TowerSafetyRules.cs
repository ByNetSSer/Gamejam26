// Shared game-loop rules; room and tower indexes are one-based.
public static class TowerSafetyRules
{
    public static bool IsProtectedRoom(int roomIndex) => roomIndex <= 2;
    public static bool RequiresStartingCurse(int towerNumber) => towerNumber > 0 && towerNumber % 2 == 0;
    public static bool IsFinalTower(int towerNumber, int totalTowers) => towerNumber >= System.Math.Max(1, totalTowers);
    public static bool CanDeclareVictory(int towerNumber, int totalTowers, bool started, bool completed, bool lost, bool failed, bool alreadyWon)
        => IsFinalTower(towerNumber, totalTowers) && started && completed && !lost && !failed && !alreadyWon;
}
