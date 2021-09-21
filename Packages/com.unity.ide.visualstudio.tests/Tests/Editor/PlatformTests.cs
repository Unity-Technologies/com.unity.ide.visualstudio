using NUnit.Framework;

namespace Microsoft.Unity.VisualStudio.Editor.Tests
{
	public class PlatformTests
	{
		[Test]
		public void CallerMemberNameIsWorking()
		{
			Assert.IsTrue(SessionSettings.GetKey().EndsWith(nameof(CallerMemberNameIsWorking)));
		}
	}
}
