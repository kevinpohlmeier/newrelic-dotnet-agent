// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;
using NServiceBus.Pipeline;

namespace NewRelic.Providers.Wrapper.NServiceBus
{
    /// <summary>
    /// Factory for NServiceBusReceiveMessage
    /// </summary>
    public class ReceiveMessageWrapper6Plus : IWrapper
    {
        private const string BrokerVendorName = "NServiceBus";
        private const string WrapperName = "ReceiveMessageWrapper6Plus";

        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall,
            IAgent agent, ITransaction transaction)
        {
            var incomingLogicalMessageContext = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<IIncomingLogicalMessageContext>(0);

            var logicalMessage = incomingLogicalMessageContext.Message;

            if (logicalMessage == null)
            {
                throw new NullReferenceException("logicalMessage");
            }

            var headers = incomingLogicalMessageContext.Headers;

            if (headers == null)
            {
                throw new NullReferenceException("headers"); // again, not sure about this...
            }

            var queueName = logicalMessage.MessageType.FullName;
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

    }
}
