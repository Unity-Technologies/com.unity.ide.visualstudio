using System;
using Newtonsoft.Json;
using UnityEditor.TestTools.TestRunner.Api;

namespace Microsoft.Unity.VisualStudio.Editor.Testing
{
	internal class TestRunnerCallbacks : ICallbacks
	{
		private JsonSerializerSettings _settings;

		public TestRunnerCallbacks()
		{
			_settings = new JsonSerializerSettings
			{
			};
		}

		private string Serialize(object obj)
		{
			return JsonConvert.SerializeObject(obj, _settings);
		}

		public void RunFinished(ITestResultAdaptor testResultAdaptor)
		{
			VisualStudioIntegration.BroadcastMessage(Messaging.MessageType.RunFinished, Serialize(new TestResultAdaptor(testResultAdaptor)));
		}

		public void RunStarted(ITestAdaptor testAdaptor)
		{
			VisualStudioIntegration.BroadcastMessage(Messaging.MessageType.RunStarted, Serialize(new TestAdaptor(testAdaptor)));
		}

		public void TestFinished(ITestResultAdaptor testResultAdaptor)
		{
			VisualStudioIntegration.BroadcastMessage(Messaging.MessageType.TestFinished, Serialize(new TestResultAdaptor(testResultAdaptor)));
		}

		public void TestStarted(ITestAdaptor testAdaptor)
		{
			VisualStudioIntegration.BroadcastMessage(Messaging.MessageType.TestStarted, Serialize(new TestAdaptor(testAdaptor)));
		}

		private static string TestModeName(TestMode testMode)
		{
			switch (testMode)
			{
				case TestMode.EditMode: return "EditMode";
				case TestMode.PlayMode: return "PlayMode";
			}

			throw new ArgumentOutOfRangeException();
		}


		internal void TestListRetrieved(TestMode testMode, ITestAdaptor testAdaptor)
		{
			// TestListRetrieved format:
			// TestMode:Json

			var value = TestModeName(testMode) + ":" + Serialize(new TestAdaptor(testAdaptor));
			VisualStudioIntegration.BroadcastMessage(Messaging.MessageType.TestListRetrieved, value);
		}
	}
}
