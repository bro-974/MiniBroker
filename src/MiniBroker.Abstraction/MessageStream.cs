namespace MiniBroker.Abstraction;

public class MessageStream
{
    public string Id { get; set; }
    public string Type { get; set; }
    public Context? Context { get; set; }
    public byte[] Payload { get; set; }
}