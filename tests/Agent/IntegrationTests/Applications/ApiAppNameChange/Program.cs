﻿using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using CommandLine;
using JetBrains.Annotations;

namespace NewRelic.Agent.IntegrationTests.Applications.ApiAppNameChange
{
	public class Program
	{
		[Option("port", Required = true)]
		[NotNull]
		public String Port { get; set; }

		static void Main(string[] args)
		{
			if (Parser.Default == null)
				throw new NullReferenceException("CommandLine.Parser.Default");

			var program = new Program();
			if (!Parser.Default.ParseArgumentsStrict(args, program))
				return;

			// Create handle that RemoteApplication expects
			new EventWaitHandle(false, EventResetMode.ManualReset, "app_server_wait_for_all_request_done_" + program.Port);

			CreatePidFile();

			Api.Agent.NewRelic.SetApplicationName("AgentApi");
			Api.Agent.NewRelic.StartAgent();
			Api.Agent.NewRelic.SetApplicationName("AgentApi2");
		}

		private static void CreatePidFile()
		{
			var pid = Process.GetCurrentProcess().Id;
			var thisAssemblyPath = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
			var pidFilePath = thisAssemblyPath + ".pid";
			var file = File.CreateText(pidFilePath);
			file.WriteLine(pid);
		}
	}
}
