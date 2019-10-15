using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Linq;

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

			if (string.IsNullOrEmpty(editorPath))
				return false;

			string fvi = null;
			if (File.Exists(editorPath) && VisualStudioEditor.IsWindows && Regex.IsMatch(editorPath, "devenv.exe$", RegexOptions.IgnoreCase))
			{
				// On windows we use the executable directly, so we can query extra information
				fvi = editorPath;
			}

			if (Directory.Exists(editorPath) && VisualStudioEditor.IsOSX && Regex.IsMatch(editorPath, "Visual\\s?Studio(?!.*Code.*).*.app$", RegexOptions.IgnoreCase))
			{
				// On Mac we use the .app folder, so we need to access to main assembly
				fvi = Path.Combine(editorPath, "Contents", "Resources", "lib", "monodevelop", "bin", "VisualStudio.exe");
			}

			if (fvi == null || !File.Exists(fvi))
				return false;

			var vi = FileVersionInfo.GetVersionInfo(fvi);
			var version = new Version(vi.ProductVersion);
			installation = new VisualStudioInstallation()
			{
				IsPrerelease = vi.IsPreRelease || editorPath.ToLower().Contains("preview"),
				Name = $"{vi.FileDescription} [{version.ToString(3)}]",
				Path = editorPath,
				Version = version
			};
			return true;
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
			var progpath = Utility
				.FindAssetFullPath("VSWhere a:packages", "vswhere.exe")
				.FirstOrDefault();

			if (string.IsNullOrWhiteSpace(progpath))
				return Enumerable.Empty<VisualStudioInstallation>();

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
				return result.ToVisualStudioInstallations();
			}
		}
	}
}
