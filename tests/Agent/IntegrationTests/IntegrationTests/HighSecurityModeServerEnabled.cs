﻿using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
	public class HighSecurityModeServerEnabled : IClassFixture<RemoteServiceFixtures.HSMBasicWebApi>
	{
		[NotNull]
		private readonly RemoteServiceFixtures.HSMBasicWebApi _fixture;

		public HighSecurityModeServerEnabled([NotNull] RemoteServiceFixtures.HSMBasicWebApi fixture, [NotNull] ITestOutputHelper output)
		{
			_fixture = fixture;
			_fixture.BypassAgentConnectionErrorLineRegexCheck = true;
			_fixture.TestLogger = output;
			_fixture.Actions
			(
				setupConfiguration: () =>
				{
					var configPath = fixture.DestinationNewRelicConfigFilePath;

					var configModifier = new NewRelicConfigModifier(configPath);

					CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
					CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "requestParameters" }, "enabled", "true");
					CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "highSecurity" }, "enabled", "false");
				},
				exerciseApplication: () =>
				{
					_fixture.GetData();
					_fixture.Get();
					_fixture.Get404();
					_fixture.GetId();
					_fixture.Post();
				}
			);

			_fixture.Initialize();
		}

		[Fact]
		public void Test()
		{
			var notConnectedLogLine = _fixture.AgentLog.TryGetLogLine(@".*? NewRelic INFO: Shutting down: Account Security Violation:*?");
			Assert.NotNull(notConnectedLogLine);
		}
	}
}
