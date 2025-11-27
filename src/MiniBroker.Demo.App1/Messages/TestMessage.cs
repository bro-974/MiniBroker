namespace MiniBroker.Demo.App1.Messages;

public class HealthCheck1 : IHealthCheck<int>
{
    public int GetStatus()
    {
        return 1;
    }
}

public class HealthCheck2 : IHealthCheck<bool>
{
    public bool GetStatus()
    {
        return true;
    }
}

public interface IHealthCheck<T>
{
    T GetStatus();
}