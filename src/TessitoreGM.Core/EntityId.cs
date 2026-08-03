using System.Text.Json.Serialization;

namespace TessitoreGM.Core;

public readonly record struct EntityId
{
    [JsonConstructor]
    public EntityId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("An entity id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
