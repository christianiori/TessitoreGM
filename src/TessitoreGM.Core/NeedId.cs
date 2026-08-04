using System.Text.Json.Serialization;

namespace TessitoreGM.Core;

public readonly record struct NeedId
{
    [JsonConstructor]
    public NeedId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A need id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
