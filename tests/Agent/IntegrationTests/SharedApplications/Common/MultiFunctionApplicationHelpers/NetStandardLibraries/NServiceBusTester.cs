// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using NServiceBus;
using NServiceBus.Config;
using NServiceBus.Config.ConfigurationSource;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries
{
    [Library]
    class NServiceBusTester
    {
        public static IBus Bus { get; set; }

        private const string DestinationReceiverHost = "NServiceBusReceiverHost";


        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public string Send()
        {
            // Build a message to send. Any object can be a message that implements NServiceBus.ICommand -- NSB will serialize it for you
            var message = new SampleNServiceBusMessage(new Random().Next(), "Foo bar");

            // Send the message. In this case we've hardcoded the recipient, but we could also use the web config to specify implicit recipients
            Bus.Send(DestinationReceiverHost, message);

            return string.Format("Message with ID={0} sent via NServiceBus", message.Id);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public string SendValid()
        {
            // Build a message to send. Any object can be a message that implements NServiceBus.ICommand -- NSB will serialize it for you
            var message = new SampleNServiceBusMessage2(new Random().Next(), "Valid");

            // Send the message. In this case we've hardcoded the recipient, but we could also use the web config to specify implicit recipients
            //Bus.Send(DestinationReceiverHost, message);
            //Bus.Send(message);
            Bus.Send("MultiFunctionApplicationHelpers.NetStandardLibraries", message);

            return string.Format("Message with ID={0} sent via NServiceBus", message.Id);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public string SendInvalid()
        {
            // Build a message to send. Any object can be a message that implements NServiceBus.ICommand -- NSB will serialize it for you
            var message = new SampleNServiceBusMessage2(new Random().Next(), "Invalid", false);

            // Send the message. In this case we've hardcoded the recipient, but we could also use the web config to specify implicit recipients
            Bus.Send(DestinationReceiverHost, message);

            return string.Format("Message with ID={0} sent via NServiceBus", message.Id);
        }


        [LibraryMethod]
        public void Initialize()
        {
            Bus = CreateNServiceBus();

        }

        private static IBus CreateNServiceBus()
        {
            var configuration = new BusConfiguration();
            configuration.UsePersistence<InMemoryPersistence>();
            // limits the search for handlers to this specific assembly, otherwiese it will traverse all referenced assemblies looking for handlers.
            configuration.AssembliesToScan(Assembly.Load("MultiFunctionApplicationHelpers"));
            return NServiceBus.Bus.Create(configuration);
        }
    }

    public class MessageHandler : IHandleMessages<SampleNServiceBusMessage2>
    {
        public void Handle(SampleNServiceBusMessage2 message)
        {
            Console.WriteLine("Received {0} message with contents={1}", message.IsValid ? "Valid" : "Invalid", message.FooBar);

            if (!message.IsValid)
            {
                throw new Exception("An exception was thrown inside the NServiceBus Receive Handler!!!!");
            }
        }
    }

    public class SampleNServiceBusMessage : ICommand
    {
        public int Id { get; private set; }
        public string FooBar { get; private set; }

        public SampleNServiceBusMessage(int id, string fooBar)
        {
            Thread.Sleep(250);
            Id = id;
            FooBar = fooBar;
        }
    }

    public class SampleNServiceBusMessage2 : ICommand
    {
        public int Id { get; private set; }
        public string FooBar { get; private set; }
        public bool IsValid { get; private set; }

        public SampleNServiceBusMessage2(int id, string fooBar, bool isValid = true)
        {
            Thread.Sleep(250);
            Id = id;
            FooBar = fooBar;
            IsValid = isValid;
        }
    }
}
