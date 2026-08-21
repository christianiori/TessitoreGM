using System.Text.Json.Serialization;

namespace TessitoreGM.Core;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WeatherCondition
{
    Clear,
    Cloudy,
    Rain,
    Storm
}
