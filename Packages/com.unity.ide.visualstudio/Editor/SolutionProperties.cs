using System.Collections.Generic;

namespace Microsoft.Unity.VisualStudio.Editor
{
	internal class SolutionProperties
	{
		public string Name { get; set; }
		public IList<KeyValuePair<string, string>> Entries { get; set; }
		public string Type { get; set; }
	}
}
