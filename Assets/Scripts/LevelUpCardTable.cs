using System;
using System.Collections.Generic;
using System.Text;

public enum LevelUpCardEffect
{
    ATKUP,
    HEAL,
    MAGNET,
    PLUS1,
    FIRERING,
    EXPLOSION,
    INCHANTFRIE
}

public sealed class LevelUpCardData
{
    public int Id { get; }
    public int Rate { get; }
    public int RequiredCardId { get; }
    public string Icon { get; }
    public string Description { get; }
    public LevelUpCardEffect Effect { get; }
    public int Value { get; }

    public LevelUpCardData(
        int id,
        int rate,
        int requiredCardId,
        string icon,
        string description,
        LevelUpCardEffect effect,
        int value)
    {
        Id = id;
        Rate = rate;
        RequiredCardId = requiredCardId;
        Icon = icon;
        Description = description;
        Effect = effect;
        Value = value;
    }
}

public sealed class LevelUpCardTable
{
    private readonly List<LevelUpCardData> cards;

    private LevelUpCardTable(List<LevelUpCardData> cards)
    {
        this.cards = cards;
    }

    public List<LevelUpCardData> GetEligibleCards(ISet<int> selectedCardIds)
    {
        List<LevelUpCardData> eligibleCards = new List<LevelUpCardData>();
        foreach (LevelUpCardData card in cards)
        {
            if (card.Rate <= 0 || selectedCardIds.Contains(card.Id))
                continue;

            if (card.RequiredCardId == 0 || selectedCardIds.Contains(card.RequiredCardId))
                eligibleCards.Add(card);
        }

        return eligibleCards;
    }

    public static LevelUpCardTable Parse(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            throw new FormatException("LevelUpCard.csv is empty.");

        string[] lines = csv.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int headerLineIndex = FindNextContentLine(lines, 0);
        if (headerLineIndex < 0)
            throw new FormatException("LevelUpCard.csv has no header row.");

        List<string> headers = SplitRow(lines[headerLineIndex]);
        int idColumn = FindColumn(headers, "ID");
        int rateColumn = FindColumn(headers, "Rate");
        int requiredColumn = FindColumn(headers, "Requirde");
        int iconColumn = FindColumn(headers, "Icon");
        int descColumn = FindColumn(headers, "Desc");
        int effectColumn = FindColumn(headers, "Effect");
        int valueColumn = FindColumn(headers, "Value");
        int requiredFieldCount = Math.Max(
            Math.Max(Math.Max(idColumn, rateColumn), Math.Max(requiredColumn, iconColumn)),
            Math.Max(Math.Max(descColumn, effectColumn), valueColumn)) + 1;

        List<LevelUpCardData> parsedCards = new List<LevelUpCardData>();
        HashSet<int> ids = new HashSet<int>();

        for (int lineIndex = headerLineIndex + 1; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex].Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            List<string> fields = SplitRow(lines[lineIndex]);
            if (fields.Count < requiredFieldCount)
                throw new FormatException($"LevelUpCard.csv row {lineIndex + 1} does not have enough columns.");

            int id = ParseInt(fields[idColumn], "ID", lineIndex, 1);
            int rate = ParseInt(fields[rateColumn], "Rate", lineIndex, 0);
            int requiredCardId = ParseInt(fields[requiredColumn], "Requirde", lineIndex, 0);
            int value = ParseInt(fields[valueColumn], "Value", lineIndex, 0);
            string icon = fields[iconColumn].Trim();
            string description = fields[descColumn].Trim();

            if (!ids.Add(id))
                throw new FormatException($"LevelUpCard.csv row {lineIndex + 1}: ID {id} is duplicated.");
            if (requiredCardId == id)
                throw new FormatException($"LevelUpCard.csv row {lineIndex + 1}: a card cannot require itself.");
            if (icon.Length == 0)
                throw new FormatException($"LevelUpCard.csv row {lineIndex + 1}: Icon is empty.");
            if (!Enum.TryParse(fields[effectColumn].Trim(), true, out LevelUpCardEffect effect))
                throw new FormatException($"LevelUpCard.csv row {lineIndex + 1}: unknown Effect '{fields[effectColumn]}'.");

            parsedCards.Add(new LevelUpCardData(
                id, rate, requiredCardId, icon, description, effect, value));
        }

        if (parsedCards.Count == 0)
            throw new FormatException("LevelUpCard.csv has no card data.");

        foreach (LevelUpCardData card in parsedCards)
        {
            if (card.RequiredCardId != 0 && !ids.Contains(card.RequiredCardId))
                throw new FormatException($"LevelUpCard.csv: card ID {card.Id} requires missing ID {card.RequiredCardId}.");
        }

        return new LevelUpCardTable(parsedCards);
    }

    private static int ParseInt(string field, string columnName, int zeroBasedLineIndex, int minimum)
    {
        if (!int.TryParse(field.Trim(), out int value) || value < minimum)
        {
            throw new FormatException(
                $"LevelUpCard.csv row {zeroBasedLineIndex + 1}: {columnName} must be an integer of at least {minimum}.");
        }

        return value;
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

    private static int FindColumn(IReadOnlyList<string> headers, string name)
    {
        for (int i = 0; i < headers.Count; i++)
        {
            if (string.Equals(headers[i].Trim().TrimStart('\uFEFF'), name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        throw new FormatException($"LevelUpCard.csv is missing the '{name}' column.");
    }

    private static List<string> SplitRow(string row)
    {
        List<string> fields = new List<string>();
        StringBuilder field = new StringBuilder();
        bool insideQuotes = false;

        for (int i = 0; i < row.Length; i++)
        {
            char character = row[i];
            if (character == '"')
            {
                if (insideQuotes && i + 1 < row.Length && row[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }
            }
            else if (character == ',' && !insideQuotes)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }

        if (insideQuotes)
            throw new FormatException("LevelUpCard.csv contains an unclosed quoted field.");

        fields.Add(field.ToString());
        return fields;
    }
}
