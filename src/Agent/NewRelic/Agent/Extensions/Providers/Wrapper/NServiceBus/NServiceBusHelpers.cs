// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.NServiceBus
{
    public class NServiceBusHelpers
    {
        private static Func<object, Dictionary<string, string>> _getHeadersFunc;
        private static Func<object, object> _getIncomingLogicalMessageFunc;
        private static Func<object, object> _getMessageTypeFunc;
        private static Func<object, string> _getMessageFullNameFunc;

        public static object GetIncomingLogicalMessage(object incomingContext)
        {
            var getLogicalMessageFunc = _getIncomingLogicalMessageFunc ??
                    (_getIncomingLogicalMessageFunc = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(incomingContext.GetType(), "IncomingLogicalMessage"));
            return getLogicalMessageFunc(incomingContext);
        }

        public static Dictionary<string, string> GetHeaders(object logicalMessage)
        {
            var getHeadersFunc = _getHeadersFunc ?? (_getHeadersFunc = VisibilityBypasser.Instance.GeneratePropertyAccessor<Dictionary<string, string>>(logicalMessage.GetType(), "Headers"));
            return getHeadersFunc(logicalMessage);
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
        public static string TryGetQueueName(object logicalMessage)
        {
            var getMessageTypeFunc = _getMessageTypeFunc ??
                (_getMessageTypeFunc = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(logicalMessage.GetType(), "MessageType"));

            var messageType = getMessageTypeFunc(logicalMessage);

            if (messageType == null)
            {
                return null;
            }

            var getMessageFullNameFunc = _getMessageFullNameFunc ??
                (_getMessageFullNameFunc = VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(messageType.GetType(), "FullName"));
            return getMessageFullNameFunc(messageType);
        }

    }
}
