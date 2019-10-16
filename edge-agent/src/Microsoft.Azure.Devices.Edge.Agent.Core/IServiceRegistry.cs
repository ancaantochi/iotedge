namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Threading.Tasks;

    public interface IServiceRegistry
    {
        Task<bool> AddService(string instanceName, ServiceInfo service);

        Task<bool> RemoveService(string instanceName, ServiceInfo service);

        Task<bool> UpdateService(string instanceName, ServiceInfo service);
    }
}
