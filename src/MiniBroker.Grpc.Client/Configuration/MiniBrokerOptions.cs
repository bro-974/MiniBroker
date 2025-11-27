namespace MiniBroker.Grpc.Client.Configuration;

public class MiniBrokerOptions
{
    public string Host { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    
    public bool EnableTypeSearchByName { get; set; } = false;
    
    public bool ThrowOnConnectionFailure { get; set; } = true;
}