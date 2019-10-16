// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Planners
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Makaretu.Dns;

    class UpdateServices : ICommand
    {
        readonly IDictionary<string, ServiceInfo> services;
        readonly IServiceRegistry serviceRegistry;
        private IModule currentModule;
        private IModule m;

        public UpdateServices(IServiceRegistry serviceRegistry, IDictionary<string, ServiceInfo> services)
        {
            this.services = services;
            this.serviceRegistry = serviceRegistry;
        }

        public UpdateServices(IModule currentModule, IModule m)
        {
            this.currentModule = currentModule;
            this.m = m;
        }

        public string Show() => "UpdateServices";

        public string Id { get; } = "UpdateServices";

        public Task ExecuteAsync(CancellationToken token)
        {
            foreach (KeyValuePair<string, ServiceInfo> service in this.services)
            {
                this.serviceRegistry.UpdateService(service.Key, service.Value);
            }
            return Task.CompletedTask;
        }

        public Task UndoAsync(CancellationToken token)
        {
            //TODO: implement
            foreach (KeyValuePair<string, ServiceInfo> service in this.services)
            {
                this.serviceRegistry.AddService(service.Key, service.Value);
            }
            return Task.CompletedTask;
        }
    }
}
