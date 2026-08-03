using System.Text.Json.Serialization;

namespace TessitoreGM.Core;

public readonly record struct FactId
{
    [JsonConstructor]
    public FactId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A fact id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public static FactId ForOrder(OrderId orderId) =>
        new($"order:{orderId.Value}");

    public override string ToString() => Value;
}
