/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using IOPath = System.IO.Path;

namespace Microsoft.Unity.VisualStudio.Editor
{
	internal class VisualStudioCodeInstallation : VisualStudioInstallation
	{
		private static readonly IGenerator _generator = new SdkStyleProjectGeneration();

		public override bool SupportsAnalyzers
		{
			get
			{
				return true;
			}
		}

		public override Version LatestLanguageVersionSupported
		{
			get
			{
				return new Version(11, 0);
			}
		}

		private string GetExtensionPath()
		{
			var vscode = IsPrerelease ? ".vscode-insiders" : ".vscode";
			var extensionsPath = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), vscode, "extensions");
			if (!Directory.Exists(extensionsPath))
				return null;

			return Directory
				.EnumerateDirectories(extensionsPath, "visualstudiotoolsforunity.vstuc*") // publisherid.extensionid
				.OrderByDescending(n => n)
				.FirstOrDefault();
		}

		public override string[] GetAnalyzers()
		{
			var vstuPath = GetExtensionPath();
			if (string.IsNullOrEmpty(vstuPath))
				return Array.Empty<string>();

			return GetAnalyzers(vstuPath);		}

		public override IGenerator ProjectGenerator
		{
			get
			{
				return _generator;
			}
		}

		private static bool IsCandidateForDiscovery(string path)
		{
			if (VisualStudioEditor.IsOSX)
				return Directory.Exists(path) && Regex.IsMatch(path, ".*Code.*.app$", RegexOptions.IgnoreCase);

			if (VisualStudioEditor.IsWindows)
				return File.Exists(path) && Regex.IsMatch(path, ".*Code.*.exe$", RegexOptions.IgnoreCase);

			return File.Exists(path) && path.EndsWith("code", StringComparison.OrdinalIgnoreCase);
		}

		public static bool TryDiscoverInstallation(string editorPath, out IVisualStudioInstallation installation)
		{
			installation = null;

			if (string.IsNullOrEmpty(editorPath))
				return false;

			if (!IsCandidateForDiscovery(editorPath))
				return false;

			Version version = null;
			bool isPrerelease = false;

			if (VisualStudioEditor.IsWindows) {
				// On windows we use the executable directly, so we can query extra information
				if (!File.Exists(editorPath))
					return false;

				// VSCode preview are not using the isPrerelease flag so far
				var vi = FileVersionInfo.GetVersionInfo(editorPath);
				version = new Version(vi.ProductMajorPart, vi.ProductMinorPart, vi.ProductBuildPart);
				isPrerelease = vi.IsPreRelease || vi.FileDescription.ToLower().Contains("insider");
			} else if (VisualStudioEditor.IsOSX) {
				var plist = IOPath.Combine(editorPath, "Contents", "Info.plist");
				if (!File.Exists(plist))
					return false;

				const string pattern = @"<key>CFBundleShortVersionString</key>\s*<string>(?<version>\d+\.\d+\.\d+).*</string>";
				var match = Regex.Match(File.ReadAllText(plist), pattern);
				if (!match.Success)
					return false;

				version = new Version(match.Groups[nameof(version)].ToString());
			}

			isPrerelease = isPrerelease || editorPath.ToLower().Contains("insider");
			installation = new VisualStudioCodeInstallation()
			{
				IsPrerelease = isPrerelease,
				Name = "Visual Studio Code" + (isPrerelease ? " - Insider" : string.Empty) + (version != null ? $" [{version.ToString(3)}]" : string.Empty),
				Path = editorPath,
				Version = version ?? new Version()
			};

			return true;
		}

		public static IEnumerable<IVisualStudioInstallation> GetVisualStudioInstallations()
		{
			var candidates = new List<string>();

			if (VisualStudioEditor.IsWindows)
			{
				var localAppPath = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
				candidates.Add(IOPath.Combine(localAppPath, "Microsoft VS Code", "Code.exe"));
				candidates.Add(IOPath.Combine(localAppPath, "Microsoft VS Code Insiders", "Code - Insiders.exe"));
			} else if (VisualStudioEditor.IsOSX)
			{
				var appPath = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
				candidates.AddRange(Directory.EnumerateDirectories(appPath, "Visual Studio Code*.app"));
			}
			else
			{
				candidates.Add("/usr/bin/code");
				candidates.Add("/bin/code");
				candidates.Add("/usr/local/bin/code");
			}

			foreach (var candidate in candidates)
			{
				if (TryDiscoverInstallation(candidate, out var installation))
					yield return installation;
			}
		}

		public override void CreateExtraFiles(string projectDirectory)
		{
			try
			{
				// see https://tattoocoder.com/recommending-vscode-extensions-within-your-open-source-projects/
				var vscodeDirectory = IOPath.Combine(projectDirectory.NormalizePathSeparators(), ".vscode");
				Directory.CreateDirectory(vscodeDirectory);

				CreateRecommendedExtensionsFile(vscodeDirectory);
				CreateSettingsFile(vscodeDirectory);
				CreateLaunchFile(vscodeDirectory);
			}
			catch (IOException)
			{
			}			
		}

		private static void CreateLaunchFile(string vscodeDirectory)
		{
			var launchFile = IOPath.Combine(vscodeDirectory, "launch.json");
			if (File.Exists(launchFile))
				return;
		}

		private static void CreateSettingsFile(string vscodeDirectory)
		{
			var settingsFile = IOPath.Combine(vscodeDirectory, "settings.json");
			if (File.Exists(settingsFile))
				return;

			const string content = @"{
    ""files.exclude"":
    {
        ""**/.DS_Store"":true,
        ""**/.git"":true,
        ""**/.gitmodules"":true,
        ""**/*.booproj"":true,
        ""**/*.pidb"":true,
        ""**/*.suo"":true,
        ""**/*.user"":true,
        ""**/*.userprefs"":true,
        ""**/*.unityproj"":true,
        ""**/*.dll"":true,
        ""**/*.exe"":true,
        ""**/*.pdf"":true,
        ""**/*.mid"":true,
        ""**/*.midi"":true,
        ""**/*.wav"":true,
        ""**/*.gif"":true,
        ""**/*.ico"":true,
        ""**/*.jpg"":true,
        ""**/*.jpeg"":true,
        ""**/*.png"":true,
        ""**/*.psd"":true,
        ""**/*.tga"":true,
        ""**/*.tif"":true,
        ""**/*.tiff"":true,
        ""**/*.3ds"":true,
        ""**/*.3DS"":true,
        ""**/*.fbx"":true,
        ""**/*.FBX"":true,
        ""**/*.lxo"":true,
        ""**/*.LXO"":true,
        ""**/*.ma"":true,
        ""**/*.MA"":true,
        ""**/*.obj"":true,
        ""**/*.OBJ"":true,
        ""**/*.asset"":true,
        ""**/*.cubemap"":true,
        ""**/*.flare"":true,
        ""**/*.mat"":true,
        ""**/*.meta"":true,
        ""**/*.prefab"":true,
        ""**/*.unity"":true,
        ""build/"":true,
        ""Build/"":true,
        ""Library/"":true,
        ""library/"":true,
        ""obj/"":true,
        ""Obj/"":true,
        ""ProjectSettings/"":true,
        ""temp/"":true,
        ""Temp/"":true
    }
}";

			File.WriteAllText(settingsFile, content);
		}

		private static void CreateRecommendedExtensionsFile(string vscodeDirectory)
		{
			var extensionFile = IOPath.Combine(vscodeDirectory, "extensions.json");
			if (File.Exists(extensionFile))
				return;

			const string content = @"{
  ""recommendations"": [
    ""ms-dotnettools.csharp""
  ]
}
";
			File.WriteAllText(extensionFile, content);
		}

		public override bool Open(string path, int line, int column, string solution)
		{
			line = Math.Max(1, line);
			column = Math.Max(0, column);

			var directory = IOPath.GetDirectoryName(solution);
			var application = Path;

			ProcessRunner.Start(string.IsNullOrEmpty(path) ? 
				ProcessStartInfoFor(application, $"\"{directory}\"") :
				ProcessStartInfoFor(application, $"\"{directory}\" -g \"{path}\":{line}:{column}"));

			return true;
		}

		private static ProcessStartInfo ProcessStartInfoFor(string application, string arguments)
		{
			if (!VisualStudioEditor.IsOSX)
				return ProcessRunner.ProcessStartInfoFor(application, arguments, redirect: false);

			// wrap with built-in OSX open feature
			arguments = $"-n \"{application}\" --args {arguments}";
			application = "open";
			return ProcessRunner.ProcessStartInfoFor(application, arguments, redirect:false, shell: true);
		}

		public static void Initialize()
		{
		}
	}
}
