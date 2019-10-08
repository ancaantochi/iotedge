// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class ServiceInfo
    {
        [JsonConstructor]
        public ServiceInfo(string instanceName, string type, string protocol, int ttl, ushort port, IDictionary<string, string> metadata)
        {
            this.InstanceName = instanceName;
            this.Type = type;
            this.Protocol = protocol;
            this.Ttl = ttl;
            this.Metadata = metadata;
            this.Port = port;
        }

        [JsonProperty(Required = Required.Always, PropertyName = "instanceName")]
        public string InstanceName { get;}

        [JsonProperty(Required = Required.Always, PropertyName = "type")]
        public string Type { get; }

        [JsonProperty(PropertyName = "protocol")]
        public string Protocol { get; }

        [JsonProperty(PropertyName = "port")]
        public ushort Port { get; }

        [JsonProperty(Required = Required.AllowNull, PropertyName = "ttl")]
        public int Ttl { get; }

        [JsonProperty(Required = Required.AllowNull, PropertyName = "priority")]
        public int Priority { get; }

        [JsonProperty(Required = Required.AllowNull, PropertyName = "weight")]
        public int Weight { get; }

        [JsonProperty(PropertyName = "metadata")]
        public IDictionary<string, string> Metadata { get; }
    }
}
