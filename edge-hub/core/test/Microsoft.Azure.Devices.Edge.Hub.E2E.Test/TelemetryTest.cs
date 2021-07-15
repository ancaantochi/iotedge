// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Integration]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    public class TelemetryTest
    {
        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        async Task SendTelemetryTest(ITransportSettings[] transportSettings)
        {
            int messagesCount = 10;
            TestModule sender = null;
            TestModule receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);

            try
            {
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender1", transportSettings);
                receiver = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver1", transportSettings);

                await receiver.SetupReceiveMessageHandler();

                Task<int> task1 = sender.SendMessagesByCountAsync("output1", 0, messagesCount, TimeSpan.FromMinutes(2));

                int sentMessagesCount = await task1;
                Assert.Equal(messagesCount, sentMessagesCount);

                double maxWait = TimeSpan.FromSeconds(5).TotalMilliseconds;
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                ISet<int> receivedMessages = receiver.GetReceivedMessageIndices();
                while (stopwatch.ElapsedMilliseconds < maxWait && messagesCount != receivedMessages.Count)
                {
                    receivedMessages = receiver.GetReceivedMessageIndices();
                }

                Assert.Equal(messagesCount, receivedMessages.Count);
            }
            finally
            {
                await this.Cleanup(rm, sender, receiver);
            }
        }

        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        async Task SendOneTelemetryMessageTest(ITransportSettings[] transportSettings)
        {
            int messagesCount = 1;
            TestModule sender = null;
            TestModule receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);

            try
            {
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender1", transportSettings);
                receiver = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver1", transportSettings);

                await receiver.SetupReceiveMessageHandler();

                Task<int> task1 = sender.SendMessagesByCountAsync("output1", 0, messagesCount, TimeSpan.FromMinutes(2));

                int sentMessagesCount = await task1;
                Assert.Equal(messagesCount, sentMessagesCount);

                double maxWait = TimeSpan.FromSeconds(3).TotalMilliseconds;
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                ISet<int> receivedMessages = receiver.GetReceivedMessageIndices();
                while (stopwatch.ElapsedMilliseconds < maxWait && messagesCount != receivedMessages.Count)
                {
                    receivedMessages = receiver.GetReceivedMessageIndices();
                }

                Assert.Equal(messagesCount, receivedMessages.Count);
            }
            finally
            {
                await this.Cleanup(rm, sender, receiver);
            }
        }

        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        async Task SendTelemetryMultipleInputsTest(ITransportSettings[] transportSettings)
        {
            int messagesCount = 30;
            TestModule sender = null;
            TestModule receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);

            try
            {
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender11", transportSettings);
                receiver = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver11", transportSettings);

                await receiver.SetupReceiveMessageHandler("input1");
                await receiver.SetupReceiveMessageHandler("input2");

                Task<int> task1 = sender.SendMessagesByCountAsync("output1", 0, messagesCount, TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(2));
                Task<int> task2 = sender.SendMessagesByCountAsync("output2", 0, messagesCount, TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(3));

                int[] sentMessagesCounts = await Task.WhenAll(task1, task2);
                Assert.Equal(messagesCount, sentMessagesCounts[0]);
                Assert.Equal(messagesCount, sentMessagesCounts[1]);

                ISet<int> receivedMessagesInput1 = receiver.GetReceivedMessageIndices("input1");
                ISet<int> receivedMessagesInput2 = receiver.GetReceivedMessageIndices("input2");

                double maxWait = TimeSpan.FromSeconds(60).TotalMilliseconds;
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                while(stopwatch.ElapsedMilliseconds < maxWait && (messagesCount != receivedMessagesInput1.Count || messagesCount != receivedMessagesInput2.Count))
                {
                    receivedMessagesInput1 = receiver.GetReceivedMessageIndices("input1");
                    receivedMessagesInput2 = receiver.GetReceivedMessageIndices("input2");
                    await Task.Delay(TimeSpan.FromMilliseconds(300));
                }
                stopwatch.Stop();

                Assert.Equal(messagesCount, receivedMessagesInput1.Count);
                Assert.Equal(messagesCount, receivedMessagesInput2.Count);
            }
            finally
            {
                await this.Cleanup(rm, sender, receiver);
            }
        }

        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        async Task SendLargeMessageHandleExceptionTest(ITransportSettings[] transportSettings)
        {
            TestModule sender = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);

            try
            {
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender1", transportSettings);

                Exception ex = null;
                try
                {
                    // create a large message
                    var message = new Message(new byte[400 * 1000]);
                    await sender.SendMessageAsync("output1", message);
                }
                catch (Exception e)
                {
                    ex = e;
                }

                Assert.NotNull(ex);
            }
            finally
            {
                await this.Cleanup(rm, sender, null);
            }
        }

        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        async Task SendTelemetryWithDelayedReceiverTest(ITransportSettings[] transportSettings)
        {
            int messagesCount = 10;
            TestModule sender = null;
            TestModule receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);

            try
            {
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender1", transportSettings);
                int sentMessagesCount = await sender.SendMessagesByCountAsync("output1", 0, messagesCount, TimeSpan.FromMinutes(2));
                Assert.Equal(messagesCount, sentMessagesCount);

                receiver = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver1", transportSettings);
                await receiver.SetupReceiveMessageHandler();

                await Task.Delay(TimeSpan.FromSeconds(60));
                ISet<int> receivedMessages = receiver.GetReceivedMessageIndices();

                Assert.Equal(messagesCount, receivedMessages.Count);
            }
            finally
            {
                await this.Cleanup(rm, sender, receiver);
            }
        }

        async Task Cleanup(RegistryManager rm, TestModule sender, TestModule receiver)
        {
            try
            {
                if (rm != null)
                {
                    await rm.CloseAsync();
                }

                if (sender != null)
                {
                    await sender.Disconnect();
                }

                if (receiver != null)
                {
                    await receiver.Disconnect();
                }
            }
            catch
            {
                // ignore
            }

            // wait for the connection to be closed on the Edge side
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }
}
