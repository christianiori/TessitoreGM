namespace TessitoreGM.Events;

public interface IWorldEvent
{
    DateTimeOffset OccurredAt { get; }
}
