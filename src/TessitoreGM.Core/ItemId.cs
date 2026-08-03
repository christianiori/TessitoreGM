namespace TessitoreGM.Core;

public readonly record struct ItemId
{
    public ItemId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("An item id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
