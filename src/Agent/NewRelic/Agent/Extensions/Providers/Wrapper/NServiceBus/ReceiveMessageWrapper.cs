// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;
using NServiceBus.Pipeline.Contexts;
using NServiceBus.Unicast.Messages;

namespace NewRelic.Providers.Wrapper.NServiceBus
{
    /// <summary>
    /// Factory for NServiceBusReceiveMessage
    /// </summary>
    public class ReceiveMessageWrapper : IWrapper
    {
        private const string BrokerVendorName = "NServiceBus";
        private const string WrapperName = "ReceiveMessageWrapper";

        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall,
            IAgent agent, ITransaction transaction)
        {
            var incomingContext = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<IncomingContext>(0);
            var logicalMessage = incomingContext.IncomingLogicalMessage;
            if (logicalMessage == null)
                throw new NullReferenceException("logicalMessage");

            var headers = logicalMessage.Headers;

            if (headers == null)
                throw new NullReferenceException("headers");

            var queueName = TryGetQueueName(logicalMessage);
            transaction = agent.CreateTransaction(
                destinationType: MessageBrokerDestinationType.Queue,
                brokerVendorName: BrokerVendorName,
                destination: queueName);

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Queue, MessageBrokerAction.Consume, BrokerVendorName, queueName);

            ProcessHeaders(headers, agent);

            return Delegates.GetDelegateFor(
                onFailure: transaction.NoticeError,
                onComplete: () =>
                {
                    segment.End();
                    transaction.End();
                });
        }

        private void ProcessHeaders(Dictionary<string, string> headers, IAgent agent)
        {
            agent.CurrentTransaction.AcceptDistributedTraceHeaders(headers, GetHeaderValue, TransportType.HTTP);
        }

        IEnumerable<string> GetHeaderValue(Dictionary<string, string> carrier, string key)
        {
            if (carrier != null)
            {
                foreach (var item in carrier)
                {
                    if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        return new string[] { item.Value };
                    }
                }
            }
            return null;
        }

        private static string TryGetQueueName(LogicalMessage logicalMessage)
        {
            if (logicalMessage.MessageType == null)
                return null;

            return logicalMessage.MessageType.FullName;
        }
    }
}
