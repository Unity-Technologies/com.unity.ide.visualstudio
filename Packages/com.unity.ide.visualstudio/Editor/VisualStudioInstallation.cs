using System;
using Unity.CodeEditor;

namespace VisualStudioEditor
{
	internal class VisualStudioInstallation
	{
		public string Name { get; set; }
		public string Path { get; set; }
		public Version Version { get; set; }
		public bool IsPrerelease { get; set; }

		public bool SupportsAnalyzers
		{
			get
			{
				if (VisualStudioEditor.IsWindows)
					return Version >= new Version(16, 3);

				if (VisualStudioEditor.IsOSX)
					return Version >= new Version(8, 3);

				return false;
			}
		}

		public CodeEditor.Installation ToCodeEditorInstallation()
		{
			return new CodeEditor.Installation() { Name = Name, Path = Path };
		}
	}
}
