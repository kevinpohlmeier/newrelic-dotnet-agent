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
    /// Factory for NServiceBusSendMessage
    /// </summary>
    public class SendMessageWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        private const string WrapperName = "SendMessageWrapper";
        private const string BrokerVendorName = "NServiceBus";

        private const string LogicalMessageType = "NServiceBus.Unicast.Messages.LogicalMessage";
        private const int LogicalMessageIndex = 1;

        private static Func<object, Dictionary<string,string>> _getHeadersFunc;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var logicalMessage = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<object>(LogicalMessageIndex);

            //If the NServiceBus version doesn't provide the LogicalMessage parameter we just bail.
            if (logicalMessage.GetType().FullName != LogicalMessageType)
            {
                return Delegates.NoOp;
            }

            var queueName = TryGetQueueName(logicalMessage);
            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Queue, MessageBrokerAction.Produce, BrokerVendorName, queueName);

            CreateOutboundHeaders(agent, logicalMessage);
            return Delegates.GetDelegateFor(segment);
        }

        private static void CreateOutboundHeaders(IAgent agent, object logicalMessage)
        {
            // not sure why we just bail if the headers are null, we might not want to keep this behavior
            var headers = GetHeaders(logicalMessage);
            if (headers == null)
            {
                return;
            }

            var setHeaders = new Action<object, string, string>((carrier, key, value) =>
            {
                var headers = GetHeaders(carrier);

                if (headers == null)
                {
                    headers = new Dictionary<string, string>();
                    SetHeaders(carrier, headers);
                }
                else if (headers is IReadOnlyDictionary<string, object>)
                {
                    headers = new Dictionary<string, string>(headers);
                    SetHeaders(carrier, headers);
                }

                headers[key] = value;
            });

            agent.CurrentTransaction.InsertDistributedTraceHeaders(logicalMessage, setHeaders);
        }

        public static Dictionary<string, string> GetHeaders(object logicalMessage)
        {
            var func = _getHeadersFunc ?? (_getHeadersFunc = VisibilityBypasser.Instance.GeneratePropertyAccessor<Dictionary<string, string>>(logicalMessage.GetType(), "Headers"));
            return func(logicalMessage);
        }

        public static void SetHeaders(object logicalMessage, Dictionary<string, string> headers)
        {
            // Unlike the GetHeaders function, we can't cache this action.  It is only valid for the specific logicalMessage object instance provided.
            var action = VisibilityBypasser.Instance.GeneratePropertySetter<Dictionary<string, string>>(logicalMessage, "Headers");

            action(headers);
        }


        /// <summary>
        /// Returns a metric name based on the type of message. The source/destination queue isn't always known (depending on the circumstances) and in some cases isn't even relevant. The message type is always known and is always relevant.
        /// </summary>
        /// <param name="logicalMessage"></param>
        /// <returns></returns>
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
    }
}
