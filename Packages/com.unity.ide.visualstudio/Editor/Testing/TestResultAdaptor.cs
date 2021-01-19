using System;
using System.Linq;
using UnityEditor.TestTools.TestRunner.Api;

namespace Microsoft.Unity.VisualStudio.Editor.Testing
{
	internal class TestResultAdaptor
	{
		private ITestResultAdaptor _testResultAdaptor;
		private TestResultAdaptor[] _children;

		public string Name => _testResultAdaptor.Name;
		public string FullName => _testResultAdaptor.FullName;

		public int PassCount => _testResultAdaptor.PassCount;
		public int FailCount => _testResultAdaptor.FailCount;
		public int InconclusiveCount => _testResultAdaptor.InconclusiveCount;
		public int SkipCount => _testResultAdaptor.SkipCount;

		public string ResultState => _testResultAdaptor.ResultState;
		public string StackTrace => _testResultAdaptor.StackTrace;

		public TestStatusAdaptor TestStatus
		{
			get
			{
				switch (_testResultAdaptor.TestStatus)
				{
					case UnityEditor.TestTools.TestRunner.Api.TestStatus.Passed:
						return TestStatusAdaptor.Passed;
					case UnityEditor.TestTools.TestRunner.Api.TestStatus.Skipped:
						return TestStatusAdaptor.Skipped;
					case UnityEditor.TestTools.TestRunner.Api.TestStatus.Inconclusive:
						return TestStatusAdaptor.Inconclusive;
					case UnityEditor.TestTools.TestRunner.Api.TestStatus.Failed:
						return TestStatusAdaptor.Failed;
				}

				throw new NotSupportedException();
			}
		}


		public TestResultAdaptor[] Children
		{
			get
			{
				if (_children == null)
				{
					_children = _testResultAdaptor.Children.Select(ta => new TestResultAdaptor(ta)).ToArray();
				}

				return _children;
			}
		}

		public TestResultAdaptor(ITestResultAdaptor testResultAdaptor)
		{
			_testResultAdaptor = testResultAdaptor;
		}
	}
}
