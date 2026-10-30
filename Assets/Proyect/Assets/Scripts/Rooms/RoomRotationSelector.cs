using System;

[Flags]
public enum HatchRotations
{
    None = 0,
    Forward = 1,
    Right = 2,
    Backward = 4,
    Left = 8,
    All = Forward | Right | Backward | Left
}

// Uniformly select an enabled quarter turn, using the same seed as room selection.
public static class RoomRotationSelector
{
    public static int ChooseQuarterTurns(Random random, HatchRotations allowed)
    {
        if (random == null) throw new ArgumentNullException(nameof(random));
        int mask = (int)allowed & (int)HatchRotations.All;
        int count = 0;
        for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) count++;
        if (count == 0) return 0;
        int selected = random.Next(count);
        for (int i = 0; i < 4; i++)
            if ((mask & (1 << i)) != 0 && selected-- == 0) return i;
        return 0;
    }
}
