/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Unity Technologies.
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Microsoft.Unity.VisualStudio.Editor
{
	internal static class Discovery
	{
		public static IEnumerable<IVisualStudioInstallation> GetVisualStudioInstallations()
		{
			foreach (var installation in VisualStudioForWindowsInstallation.GetVisualStudioInstallations())
				yield return installation;

			foreach (var installation in VisualStudioForMacInstallation.GetVisualStudioInstallations())
				yield return installation;

			foreach (var installation in VisualStudioCodeInstallation.GetVisualStudioInstallations())
				yield return installation;

			if (IsLegacyVSCodePackageLoaded())
			{
				Debug.LogWarning("[Visual Studio Editor] package has now built-in support for the whole Visual Studio family of products, including Visual Studio code. Please remove the legacy [Visual Studio Code Editor] package to avoid incompatibilities.");
			}
		}

		private static bool IsLegacyVSCodePackageLoaded()
		{
			return AppDomain
				.CurrentDomain
				.GetAssemblies()
				.Any(a => a.FullName.StartsWith("Unity.VSCode.Editor"));
		}

		public static bool TryDiscoverInstallation(string editorPath, out IVisualStudioInstallation installation)
		{
			try
			{
				if (VisualStudioForWindowsInstallation.TryDiscoverInstallation(editorPath, out installation))
					return true;

				if (VisualStudioForMacInstallation.TryDiscoverInstallation(editorPath, out installation))
					return true;

				if (VisualStudioCodeInstallation.TryDiscoverInstallation(editorPath, out installation))
					return true;
			}
			catch (IOException)
			{
				installation = null;
			}

			return false;
		}

		public static void Initialize()
		{
			VisualStudioForWindowsInstallation.Initialize();
			VisualStudioForMacInstallation.Initialize();
			VisualStudioCodeInstallation.Initialize();
		}
	}
}
