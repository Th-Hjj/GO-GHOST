/// <summary>
/// ASSIST RULES (the single source of truth for slot counts).
/// Each level GRANTS some slots on top of the previous levels:
///   Level 1: +1 AI, +1 body     (total 1 AI, 1 body)
///   Level 2: +1 AI, +2 body     (total 2 AI, 3 body)
///   Level 3: +1 AI, +2 body     (total 3 AI, 5 body)
/// To rebalance, only edit the two arrays below (index = level).
/// Totals are summed across all levels up to the current one, so jumping 0 -> 3 still grants everything.
/// </summary>
public static class AssistRules
{
    public const int MaxLevel = 3;

    static readonly int[] aiGrant   = { 0, 1, 1, 1 };
    static readonly int[] bodyGrant = { 0, 1, 2, 2 };

    public static int TotalAiSlots(int level)
    {
        int n = 0;
        for (int i = 1; i <= level && i < aiGrant.Length; i++) n += aiGrant[i];
        return n;
    }

    public static int TotalBodySlots(int level)
    {
        int n = 0;
        for (int i = 1; i <= level && i < bodyGrant.Length; i++) n += bodyGrant[i];
        return n;
    }
}
