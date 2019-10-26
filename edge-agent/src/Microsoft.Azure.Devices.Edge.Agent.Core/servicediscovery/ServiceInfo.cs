// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.ServiceDiscovery
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class ServiceInfo : IEquatable<ServiceInfo>
    {
        [JsonConstructor]
        public ServiceInfo(string serviceName, string protocol, int ttl, ushort port, IDictionary<string, string> metadata)
        {
            this.ServiceName = serviceName;
            this.Protocol = protocol;
            this.Ttl = ttl;
            this.Metadata = metadata;
            this.Port = port;
        }

        //[JsonProperty(Required = Required.Always, PropertyName = "instanceName")]
        //public string InstanceName { get;}

        [JsonProperty(Required = Required.Always, PropertyName = "serviceName")]
        public string ServiceName { get; }

        [JsonProperty(PropertyName = "protocol")]
        public string Protocol { get; }

        [JsonProperty(PropertyName = "port")]
        public ushort Port { get; }

        [JsonProperty(PropertyName = "ttl")]
        public int Ttl { get; }

        [JsonProperty(PropertyName = "priority")]
        public int Priority { get; }

        [JsonProperty(PropertyName = "weight")]
        public int Weight { get; }

        [JsonProperty(PropertyName = "metadata")]
        public IDictionary<string, string> Metadata { get; }

        public bool Equals(ServiceInfo other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(this.ServiceName, other.ServiceName) && string.Equals(this.Protocol, other.Protocol) && this.Port == other.Port && this.Ttl == other.Ttl && this.Priority == other.Priority && this.Weight == other.Weight && Equals(this.Metadata, other.Metadata);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((ServiceInfo)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (this.ServiceName != null ? this.ServiceName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Protocol != null ? this.Protocol.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ this.Port.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Ttl;
                hashCode = (hashCode * 397) ^ this.Priority;
                hashCode = (hashCode * 397) ^ this.Weight;
                hashCode = (hashCode * 397) ^ (this.Metadata != null ? this.Metadata.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
