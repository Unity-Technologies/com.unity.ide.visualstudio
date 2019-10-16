using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Microsoft.VisualStudio.Editor
{
	internal static class FileUtility
	{
		public const char WinSeparator = '\\';
		public const char UnixSeparator = '/';

		// Safe for packages as we use packageInfo.resolvedPath, so this should work in library package cache as well
		public static string[] FindPackageAssetFullPath(string assetfilter, string filefilter)
		{
			return AssetDatabase.FindAssets(assetfilter)
				.Select(AssetDatabase.GUIDToAssetPath)
				.Where(assetPath => assetPath.Contains(filefilter))
				.Select(asset =>
				 {
					 var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(asset);
					 return Normalize(packageInfo.resolvedPath + asset.Substring(packageInfo.assetPath.Length));
				 })
				.ToArray();
		}

		public static string GetAssetFullPath(string asset)
		{
			var basePath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
			return Path.GetFullPath(Path.Combine(basePath, Normalize(asset)));
		}

		public static string Normalize(string path)
		{
			if (VisualStudioEditor.IsWindows)
				return path.Replace(UnixSeparator, WinSeparator);

			return path;
		}

		public static string FileNameWithoutExtension(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return "";
			}

			var indexOfDot = -1;
			var indexOfSlash = 0;
			for (var i = path.Length - 1; i >= 0; i--)
			{
				if (indexOfDot == -1 && path[i] == '.')
				{
					indexOfDot = i;
				}

				if (indexOfSlash == 0 && path[i] == '/' || path[i] == '\\')
				{
					indexOfSlash = i + 1;
					break;
				}
			}

			if (indexOfDot == -1)
			{
				indexOfDot = path.Length - 1;
			}

			return path.Substring(indexOfSlash, indexOfDot - indexOfSlash);
		}
	}
}
