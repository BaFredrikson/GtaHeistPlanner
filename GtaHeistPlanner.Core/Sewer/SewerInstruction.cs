namespace GtaHeistPlanner.Core.Sewer;

public readonly record struct SewerInstruction
{
    public SewerInstruction(int chamber, char tunnel)
    {
        if (chamber <= 0) throw new ArgumentOutOfRangeException(nameof(chamber));
        if (!char.IsLetter(tunnel)) throw new ArgumentException("Tunnel must be a letter.", nameof(tunnel));
        Chamber = chamber;
        Tunnel = char.ToUpperInvariant(tunnel);
    }

    public int Chamber { get; }
    public char Tunnel { get; }
    public override string ToString() => $"{Chamber}{Tunnel}";
}
