// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.NServiceBus
{
    /// <summary>
    /// Factory for NServiceBusReceiveMessage
    /// </summary>
    public class ReceiveMessageWrapper : IWrapper
    {
        private const string BrokerVendorName = "NServiceBus";
        private const string WrapperName = "ReceiveMessageWrapper";

        private const int IncomingContextIndex = 0;

        public bool IsTransactionRequired => false;

        private static Func<object, Dictionary<string,string>> _getHeadersFunc;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall,
            IAgent agent, ITransaction transaction)
        {
            var incomingContext = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<object>(IncomingContextIndex);

            var logicalMessage = GetIncomingLogicalMessage(incomingContext);

            if (logicalMessage == null)
            {
                throw new NullReferenceException("logicalMessage");
            }

            var headers = GetHeaders(logicalMessage);

            if (headers == null)
            {
                throw new NullReferenceException("headers"); // again, not sure about this...
            }

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

        private static object GetIncomingLogicalMessage(object incomingContext)
        {
            var getLogicalMessageFunc = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(incomingContext.GetType(), "IncomingLogicalMessage");
            return getLogicalMessageFunc(incomingContext);
        }

        private static string TryGetQueueName(object logicalMessage)
        {
            var getMessageTypeFunc = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(logicalMessage.GetType(), "MessageType");

            var messageType = getMessageTypeFunc(logicalMessage);

            if (messageType == null)
            {
                return null;
            }

            var getFullNameFunc = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(messageType.GetType(), "FullName");
            return getFullNameFunc(messageType) as string;
        }

        public static Dictionary<string, string> GetHeaders(object logicalMessage)
        {
            var func = _getHeadersFunc ?? (_getHeadersFunc = VisibilityBypasser.Instance.GeneratePropertyAccessor<Dictionary<string,string>>(logicalMessage.GetType(), "Headers"));
            return func(logicalMessage);
        }

        public static void SetHeaders(object logicalMessage, IDictionary<string, object> headers)
        {
            // Unlike the GetHeaders function, we can't cache this action.  It is only valid for the specific logicalMessage object instance provided.
            var action = VisibilityBypasser.Instance.GeneratePropertySetter<IDictionary<string, object>>(logicalMessage, "Headers");

            action(headers);
        }

    }
}
