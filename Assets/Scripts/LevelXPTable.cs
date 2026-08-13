using System;
using System.Collections.Generic;

public sealed class LevelXPTable
{
    private readonly Dictionary<int, int> needXPByLevel;

    private LevelXPTable(Dictionary<int, int> needXPByLevel)
    {
        this.needXPByLevel = needXPByLevel;
    }

    public int MaxDefinedLevel { get; private set; }

    public bool TryGetNeedXP(int level, out int needXP)
    {
        return needXPByLevel.TryGetValue(level, out needXP);
    }

    public static LevelXPTable Parse(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            throw new FormatException("LevelXP.csv is empty.");

        string[] lines = csv.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int headerLineIndex = FindNextContentLine(lines, 0);
        if (headerLineIndex < 0)
            throw new FormatException("LevelXP.csv has no header row.");

        string[] headers = SplitRow(lines[headerLineIndex]);
        int levelColumn = FindColumn(headers, "Level");
        int needXPColumn = FindColumn(headers, "NeedXP");
        Dictionary<int, int> values = new Dictionary<int, int>();
        int maxLevel = 0;

        for (int lineIndex = headerLineIndex + 1; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex].Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            string[] fields = SplitRow(line);
            int requiredColumns = Math.Max(levelColumn, needXPColumn) + 1;
            if (fields.Length < requiredColumns)
                throw new FormatException($"LevelXP.csv row {lineIndex + 1} does not have enough columns.");

            if (!int.TryParse(fields[levelColumn].Trim(), out int level) || level < 1)
                throw new FormatException($"LevelXP.csv row {lineIndex + 1}: Level must be an integer of at least 1.");

            if (!int.TryParse(fields[needXPColumn].Trim(), out int needXP) || needXP < 1)
                throw new FormatException($"LevelXP.csv row {lineIndex + 1}: NeedXP must be a positive integer.");

            if (values.ContainsKey(level))
                throw new FormatException($"LevelXP.csv row {lineIndex + 1}: Level {level} is duplicated.");

            values.Add(level, needXP);
            maxLevel = Math.Max(maxLevel, level);
        }

        if (values.Count == 0)
            throw new FormatException("LevelXP.csv has no level data.");

        for (int level = 1; level <= maxLevel; level++)
        {
            if (!values.ContainsKey(level))
                throw new FormatException($"LevelXP.csv is missing Level {level}. Levels must be continuous from 1.");
        }

        return new LevelXPTable(values) { MaxDefinedLevel = maxLevel };
    }

    private static int FindNextContentLine(string[] lines, int startIndex)
    {
        for (int i = startIndex; i < lines.Length; i++)
        {
            string line = lines[i].Trim().TrimStart('\uFEFF');
            if (line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private static int FindColumn(string[] headers, string name)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            if (string.Equals(headers[i].Trim().TrimStart('\uFEFF'), name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        throw new FormatException($"LevelXP.csv is missing the '{name}' column.");
    }

    private static string[] SplitRow(string row)
    {
        // LevelXP contains numeric fields only. Reject quoted commas explicitly so a malformed
        // table does not silently produce a different progression.
        if (row.IndexOf('"') >= 0)
            throw new FormatException("LevelXP.csv does not support quoted fields.");

        return row.Split(',');
    }
}
