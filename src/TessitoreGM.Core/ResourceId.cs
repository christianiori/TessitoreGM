using System.Text.Json.Serialization;

namespace TessitoreGM.Core;

public readonly record struct ResourceId
{
    [JsonConstructor]
    public ResourceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "A resource id cannot be empty.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
