using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public enum SpawnShape
{
    CIRCLE,
    LINE,
    CROWD,
    SQUARE,
    TORNADO
}

[Serializable]
public sealed class StageMonsterRule
{
    public int StageId { get; private set; }
    public string MonsterId { get; private set; }
    public float SpawnStartSec { get; private set; }
    public float WaveIntervalSec { get; private set; }
    public int WaveSizeStart { get; private set; }
    public int WaveSizeGrowth { get; private set; }
    public int WaveSizeMax { get; private set; }
    public int WaveCount { get; private set; }
    public int TotalBudget { get; private set; }
    public int MaxAliveCap { get; private set; }
    public float MonsterHP { get; private set; }
    public float AttackDamage { get; private set; }
    public SpawnShape SpawnShape { get; private set; }

    public static List<StageMonsterRule> ParseForStage(string csv, int stageId)
    {
        List<List<string>> rows = ParseRows(csv);
        List<StageMonsterRule> results = new List<StageMonsterRule>();
        if (rows.Count == 0)
        {
            return results;
        }

        Dictionary<string, int> columns = BuildColumnMap(rows[0]);
        RequireColumns(columns);

        for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            List<string> row = rows[rowIndex];
            if (row.Count == 0 || (row.Count == 1 && string.IsNullOrWhiteSpace(row[0])))
            {
                continue;
            }

            try
            {
                int parsedStageId = ParseInt(Get(row, columns, "StageId"), "StageId");
                if (parsedStageId != stageId)
                {
                    continue;
                }

                StageMonsterRule rule = new StageMonsterRule
                {
                    StageId = parsedStageId,
                    MonsterId = Get(row, columns, "MonsterId").Trim(),
                    SpawnStartSec = ParseFloat(GetSpawnStart(row, columns), "SpwanStartSec"),
                    WaveIntervalSec = ParseFloat(Get(row, columns, "WaveIntervalSec"), "WaveIntervalSec"),
                    WaveSizeStart = ParseInt(Get(row, columns, "WaveSizeStart"), "WaveSizeStart"),
                    WaveSizeGrowth = ParseInt(Get(row, columns, "WaveSizeGrowth"), "WaveSizeGrowth"),
                    WaveSizeMax = ParseInt(Get(row, columns, "WaveSizeMax"), "WaveSizeMax"),
                    WaveCount = GetOptionalInt(row, columns, "WaveCount", int.MaxValue),
                    TotalBudget = ParseInt(Get(row, columns, "TotalBudget"), "TotalBudget"),
                    MaxAliveCap = ParseInt(Get(row, columns, "MaxAliveCap"), "MaxAliveCap"),
                    MonsterHP = GetOptionalFloat(row, columns, "MonsterHP", 0f),
                    AttackDamage = GetOptionalFloat(row, columns, "AttackDamage", -1f),
                    SpawnShape = ParseSpawnShape(Get(row, columns, "SpawnShape"))
                };

                rule.Validate(rowIndex + 1);
                results.Add(rule);
            }
            catch (Exception exception)
            {
                throw new FormatException($"StageMonster.csv row {rowIndex + 1}: {exception.Message}", exception);
            }
        }

        return results;
    }

    private void Validate(int rowNumber)
    {
        if (string.IsNullOrWhiteSpace(MonsterId))
            throw new FormatException("MonsterId is empty.");
        if (SpawnStartSec < 0f)
            throw new FormatException("SpwanStartSec cannot be negative.");
        if (WaveIntervalSec <= 0f)
            throw new FormatException("WaveIntervalSec must be greater than zero.");
        if (WaveSizeStart < 0 || WaveSizeGrowth < 0 || WaveSizeMax < 0 || WaveCount < 0 || TotalBudget < 0 || MaxAliveCap < 0)
            throw new FormatException($"Spawn counts cannot be negative (row {rowNumber}).");
        if (MonsterHP < 0f)
            throw new FormatException("MonsterHP cannot be negative. Use 0 to keep the prefab value.");
        if (AttackDamage < -1f)
            throw new FormatException("AttackDamage cannot be less than -1. Use -1 to keep the prefab value.");
    }

    private static string GetSpawnStart(List<string> row, Dictionary<string, int> columns)
    {
        string header = columns.ContainsKey("SpwanStartSec") ? "SpwanStartSec" : "SpawnStartSec";
        return Get(row, columns, header);
    }

    private static void RequireColumns(Dictionary<string, int> columns)
    {
        string[] required =
        {
            "StageId", "MonsterId", "WaveIntervalSec", "WaveSizeStart",
            "WaveSizeGrowth", "WaveSizeMax", "TotalBudget", "MaxAliveCap", "SpawnShape"
        };

        foreach (string name in required)
        {
            if (!columns.ContainsKey(name))
                throw new FormatException($"StageMonster.csv is missing the '{name}' column.");
        }

        if (!columns.ContainsKey("SpwanStartSec") && !columns.ContainsKey("SpawnStartSec"))
            throw new FormatException("StageMonster.csv is missing the 'SpwanStartSec' column.");
    }

    private static Dictionary<string, int> BuildColumnMap(List<string> header)
    {
        Dictionary<string, int> columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Count; i++)
        {
            string name = header[i].Trim().TrimStart('\uFEFF');
            if (!string.IsNullOrEmpty(name))
                columns[name] = i;
        }
        return columns;
    }

    private static string Get(List<string> row, Dictionary<string, int> columns, string name)
    {
        int index = columns[name];
        if (index >= row.Count)
            throw new FormatException($"'{name}' has no value.");
        return row[index];
    }

    private static int ParseInt(string value, string name)
    {
        if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            throw new FormatException($"'{name}' must be an integer, but was '{value}'.");
        return result;
    }

    private static float ParseFloat(string value, string name)
    {
        if (!float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            throw new FormatException($"'{name}' must be a number, but was '{value}'.");
        return result;
    }

    private static SpawnShape ParseSpawnShape(string value)
    {
        string normalized = value.Trim();
        if (Enum.TryParse(normalized, true, out SpawnShape result) &&
            Enum.IsDefined(typeof(SpawnShape), result) &&
            string.Equals(result.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            return result;

        throw new FormatException(
            $"'SpawnShape' must be one of {string.Join(", ", Enum.GetNames(typeof(SpawnShape)))}, but was '{value}'.");
    }

    private static int GetOptionalInt(
        List<string> row,
        Dictionary<string, int> columns,
        string name,
        int fallback)
    {
        if (!columns.ContainsKey(name))
            return fallback;

        string value = Get(row, columns, name);
        return string.IsNullOrWhiteSpace(value) ? fallback : ParseInt(value, name);
    }

    private static float GetOptionalFloat(
        List<string> row,
        Dictionary<string, int> columns,
        string name,
        float fallback)
    {
        if (!columns.ContainsKey(name))
            return fallback;

        string value = Get(row, columns, name);
        return string.IsNullOrWhiteSpace(value) ? fallback : ParseFloat(value, name);
    }

    private static List<List<string>> ParseRows(string csv)
    {
        List<List<string>> rows = new List<List<string>>();
        List<string> row = new List<string>();
        StringBuilder field = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < csv.Length; i++)
        {
            char current = csv[i];
            if (current == '"')
            {
                if (quoted && i + 1 < csv.Length && csv[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (current == ',' && !quoted)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if ((current == '\n' || current == '\r') && !quoted)
            {
                if (current == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                    i++;
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = new List<string>();
            }
            else
            {
                field.Append(current);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        if (quoted)
            throw new FormatException("StageMonster.csv contains an unclosed quoted field.");

        return rows;
    }
}
