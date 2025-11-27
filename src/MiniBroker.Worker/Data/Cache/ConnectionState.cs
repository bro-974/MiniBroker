namespace MiniBroker.Worker.Data.Cache;

public record ConnectionDto(string Name, string Remote)
{
    public bool IsConnected { get; set; }
}