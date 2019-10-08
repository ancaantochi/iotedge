namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    public interface IServiceRegistry
    {
        bool AddService(ServiceInfo service);
        bool RemoveService(ServiceInfo service);
        void Start();
        void Stop();
    }
}