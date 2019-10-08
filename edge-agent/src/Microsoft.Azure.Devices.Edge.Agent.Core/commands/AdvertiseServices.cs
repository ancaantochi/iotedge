// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Planners
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Makaretu.Dns;

    class AdvertiseServices : ICommand
    {
        readonly IDictionary<string, ServiceInfo> services;
        readonly IServiceRegistry serviceRegistry;

        public AdvertiseServices(IServiceRegistry serviceRegistry, IDictionary<string, ServiceInfo> services)
        {
            this.services = services;
            this.serviceRegistry = serviceRegistry;
        }

        public string Show() => "AdvertiseServices";

        public string Id { get; } = "AdvertiseServices";

        public Task ExecuteAsync(CancellationToken token)
        {
            foreach (KeyValuePair<string, ServiceInfo> service in this.services)
            {
                this.serviceRegistry.AddService(service.Value);
            }
            return Task.CompletedTask;
        }

        public Task UndoAsync(CancellationToken token)
        {
            foreach (KeyValuePair<string, ServiceInfo> service in this.services)
            {
                this.serviceRegistry.RemoveService(service.Value);
            }
            return Task.CompletedTask;
        }
    }
}
