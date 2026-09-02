namespace GtaHeistPlanner.DataExtractor;

public sealed class LookupTable(IReadOnlyList<string> values, string name)
{
    public string Resolve(int index)
    {
        if (index < 0 || index >= values.Count)
        {
            throw new InvalidDataException(
                $"{name} index {index} is outside the valid range 0..{values.Count - 1}.");
        }

        return values[index];
    }
}
