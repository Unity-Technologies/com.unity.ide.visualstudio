using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace VisualStudioEditor
{
	internal static class Discovery
	{
		public static IEnumerable<VisualStudioInstallation> GetVisualStudioInstallations()
		{
			if (VisualStudioEditor.IsWindows)
			{
				foreach (var installation in QueryVsWhere())
					yield return installation;
			}

			if (VisualStudioEditor.IsOSX)
			{
				if (TryDiscoverInstallation("/Applications/Visual Studio.app", out var installation))
					yield return installation;

				if (TryDiscoverInstallation("/Applications/Visual Studio (Preview).app", out installation))
					yield return installation;
			}
		}

		public static bool TryDiscoverInstallation(string editorPath, out VisualStudioInstallation installation)
		{
			installation = null;

			if (string.IsNullOrEmpty(editorPath) || !File.Exists(editorPath))
				return false;

			if (VisualStudioEditor.IsWindows && Regex.IsMatch(editorPath, "devenv.exe$", RegexOptions.IgnoreCase))
			{
				var vi = FileVersionInfo.GetVersionInfo(editorPath);
				installation = new VisualStudioInstallation()
				{
					IsPrerelease = vi.IsPreRelease,
					Name = vi.FileDescription,
					Path = editorPath,
					Version = new Version(vi.ProductVersion)
				};
				return true;
			}

			if (VisualStudioEditor.IsOSX && Regex.IsMatch(editorPath, "Visual\\s?Studio(?!.*Code.*).*.app$", RegexOptions.IgnoreCase))
			{
				var plist = Path.Combine(editorPath, "Contents/Info.plist");
				if (!File.Exists(plist))
					return false;

				var file = File.ReadAllText(plist);

				const string versionStringRegex = @"\<key\>CFBundleShortVersionString\</key\>\s+\<string\>(?<version>\d+\.\d+\.\d+\.\d+?)\</string\>";
				var versionMatch = Regex.Match(file, versionStringRegex);
				var versionGroup = versionMatch.Groups["version"];
				if (!versionGroup.Success)
					return false;

				installation = new VisualStudioInstallation()
				{
					IsPrerelease = editorPath.ToLower().Contains("preview"),
					Name = "Visual Studio",
					Path = editorPath,
					Version = new Version(versionGroup.Value)
				};
				return true;
			}

			return false;
		}

		#region VsWhere Json Schema
		#pragma warning disable CS0649
		[Serializable]
		internal class VsWhereResult
		{
			public VsWhereEntry[] entries;

			public static VsWhereResult FromJson(string json)
			{
				return JsonUtility.FromJson<VsWhereResult>("{ \"" + nameof(VsWhereResult.entries) + "\": " + json + " }");
			}

			public IEnumerable<VisualStudioInstallation> ToVisualStudioInstallations()
			{
				foreach(var entry in entries)
				{
					yield return new VisualStudioInstallation()
					{
						Name = $"{entry.displayName} [{entry.catalog.productDisplayVersion}]",
						Path = entry.productPath,
						IsPrerelease = entry.isPrerelease,
						Version = Version.Parse(entry.catalog.buildVersion)
					};
				}
			}
		}

		[Serializable]
		internal class VsWhereEntry
		{
			public string displayName;
			public bool isPrerelease;
			public string productPath;
			public VsWhereCatalog catalog;
		}

		[Serializable]
		internal class VsWhereCatalog
		{
			public string productDisplayVersion; // non parseable like "16.3.0 Preview 3.0"
			public string buildVersion;
		}
		#pragma warning restore CS3021
		#endregion

		private static IEnumerable<VisualStudioInstallation> QueryVsWhere()
		{
			var progpath = Utility.FindAssetFullPath("VSWhere a:packages", "vswhere.exe");

			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = progpath,
					Arguments = "-prerelease -format json",
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
				}
			};

			using (process)
			{
				var json = string.Empty;

				process.OutputDataReceived += (o, e) => json += e.Data;
				process.Start();
				process.BeginOutputReadLine();
				process.WaitForExit();

				var result = VsWhereResult.FromJson(json);
				return result
					.ToVisualStudioInstallations();
			}
		}
	}
}
