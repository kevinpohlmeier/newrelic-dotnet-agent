// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using NServiceBus;
using NServiceBusCommands;
using System;
using System.Linq;
using System.Messaging;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries
{
    [Library]
    class NServiceBusTester
    {
        public static IBus Bus { get; set; }

        private const string DestinationReceiverHost = "NServiceBusReceiverHost";


        //[LibraryMethod]
        //[Transaction]
        //[MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        //public string Send()
        //{
        //    // Build a message to send. Any object can be a message that implements NServiceBus.ICommand -- NSB will serialize it for you
        //    var message = new SampleNServiceBusMessage(new Random().Next(), "Foo bar");

        //    // Send the message. In this case we've hardcoded the recipient, but we could also use the web config to specify implicit recipients
        //    Bus.Send(DestinationReceiverHost, message);

        //    return string.Format("Message with ID={0} sent via NServiceBus", message.Id);
        //}

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public string SendValid()
        {
            // Build a message to send. Any object can be a message that implements NServiceBus.ICommand -- NSB will serialize it for you
            var message = new SampleNServiceBusCommand(new Random().Next(), "Valid");

            // Send the message. In this case we've hardcoded the recipient, but we could also use the web config to specify implicit recipients
            Bus.SendLocal(message);

            return string.Format("Message with ID={0} sent via NServiceBus", message.Id);
        }

        //[LibraryMethod]
        //[Transaction]
        //[MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        //public string SendInvalid()
        //{
        //    // Build a message to send. Any object can be a message that implements NServiceBus.ICommand -- NSB will serialize it for you
        //    var message = new SampleNServiceBusMessage2(new Random().Next(), "Invalid", false);

        //    // Send the message. In this case we've hardcoded the recipient, but we could also use the web config to specify implicit recipients
        //    Bus.Send(DestinationReceiverHost, message);

        //    return string.Format("Message with ID={0} sent via NServiceBus", message.Id);
        //}


        [LibraryMethod]
        public void Initialize()
        {
            Bus = CreateNServiceBus();
            CreateEmptyQueue("ConsoleMultiFunctionApplicationFW");

        }

        private static IBus CreateNServiceBus()
        {
            var configuration = new BusConfiguration();
            configuration.UsePersistence<InMemoryPersistence>();
            // limits the search for handlers to this specific assembly, otherwise it will traverse all referenced assemblies looking for handlers.
            //configuration.AssembliesToScan(Assembly.Load("NServiceBusCommands"));
            return NServiceBus.Bus.Create(configuration);
        }

        private static void CreateEmptyQueue(string queueName, bool isTransactional = false)
        {
            //We create the queue name here because this operation is only allowed on the current host and not remote ones.
            var privateQueueName = "private$\\" + queueName;

            MessageQueue queueToCreate =
                MessageQueue.GetPrivateQueuesByMachine(Environment.MachineName)
                    .SingleOrDefault(x => x.QueueName.EndsWith(queueName.ToLower()));

            if (queueToCreate == null)
            {
                var localQueue = MessageQueue.Create(".\\" + privateQueueName, isTransactional);
                localQueue.SetPermissions("Everyone", MessageQueueAccessRights.FullControl, AccessControlEntryType.Allow);
            }
            else
            {
                queueToCreate.Purge();
            }
        }

    }

    public class MessageHandler : IHandleMessages<SampleNServiceBusCommand>
    {
        public void Handle(SampleNServiceBusCommand message)
        {
            Console.WriteLine("Received {0} message with contents={1}", message.IsValid ? "Valid" : "Invalid", message.FooBar);

            if (!message.IsValid)
            {
                throw new Exception("An exception was thrown inside the NServiceBus Receive Handler!!!!");
            }
        }
    }
}
