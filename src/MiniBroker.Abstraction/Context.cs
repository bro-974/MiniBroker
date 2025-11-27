namespace MiniBroker.Abstraction;

public class Context
{
    public string Source { get; set; }
    public string Destinataire { get; set; }
    public bool PersistMessage { get; set; }
}