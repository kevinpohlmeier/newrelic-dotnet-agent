// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;
using NServiceBus.Unicast.Messages;

namespace NewRelic.Providers.Wrapper.NServiceBus
{
    /// <summary>
    /// Factory for NServiceBusSendMessage
    /// </summary>
    public class SendMessageWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        private const string WrapperName = "SendMessageWrapper";
        private const string BrokerVendorName = "NServiceBus";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var logicalMessage = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<LogicalMessage>(1);

            var queueName = TryGetQueueName(logicalMessage);
            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Queue, MessageBrokerAction.Produce, BrokerVendorName, queueName);

            CreateOutboundHeaders(agent, logicalMessage);
            return Delegates.GetDelegateFor(segment);
        }

        private static void CreateOutboundHeaders(IAgent agent, LogicalMessage logicalMessage)
        {
            if (logicalMessage.Headers == null)
                return;

            var setHeaders = new Action<LogicalMessage, string, string>((carrier, key, value) =>
            {
                if (carrier.Headers.ContainsKey(key))
                {
                    carrier.Headers.Remove(key);
                }

                carrier.Headers.Add(key, value);
            });

            agent.CurrentTransaction.InsertDistributedTraceHeaders(logicalMessage, setHeaders);
        }

        /// <summary>
        /// Returns a metric name based on the type of message. The source/destination queue isn't always known (depending on the circumstances) and in some cases isn't even relevant. The message type is always known and is always relevant.
        /// </summary>
        /// <param name="logicalMessage"></param>
        /// <returns></returns>
        private static string TryGetQueueName(LogicalMessage logicalMessage)
        {
            if (logicalMessage.MessageType == null)
                return null;

            return logicalMessage.MessageType.FullName;
        }
    }
}
