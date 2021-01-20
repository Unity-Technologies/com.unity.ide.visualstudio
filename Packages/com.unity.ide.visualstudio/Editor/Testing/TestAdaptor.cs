using System.Linq;
using UnityEditor.TestTools.TestRunner.Api;

namespace Microsoft.Unity.VisualStudio.Editor.Testing
{
	internal class TestAdaptor
	{
		private ITestAdaptor _testAdaptor;
		private TestAdaptor[] _children;

		public string Id => _testAdaptor.Id;
		public string Name => _testAdaptor.Name;
		public string FullName => _testAdaptor.FullName;

		public string Type => _testAdaptor.TypeInfo?.FullName;
		public string Method => _testAdaptor?.Method?.Name;
		public string Assembly => _testAdaptor.TypeInfo?.Assembly?.Location;

		public TestAdaptor[] Children
		{
			get
			{
				if (_children == null)
				{
					_children = _testAdaptor.Children.Select(ta => new TestAdaptor(ta)).ToArray();
				}

				return _children;
			}
		}

		public TestAdaptor(ITestAdaptor testAdaptor)
		{
			_testAdaptor = testAdaptor;
		}
	}
}
