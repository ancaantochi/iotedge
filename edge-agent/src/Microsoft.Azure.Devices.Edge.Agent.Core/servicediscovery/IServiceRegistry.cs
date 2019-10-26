// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.ServiceDiscovery
{
    using System.Threading.Tasks;

    public interface IServiceRegistry
    {
        Task<bool> AddService(string instanceName, ServiceInfo service);

        Task<bool> RemoveService(string instanceName, ServiceInfo service);

        Task<bool> UpdateService(string instanceName, ServiceInfo service);
    }
}
