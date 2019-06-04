using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using UnityEngine;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace VisualStudioEditor
{
    internal class VSInitializer
    {
        public string BridgeFile { get; private set; }

        public void Initialize(string editorPath, Dictionary<VisualStudioVersion, string[]> installedVisualStudios)
        {
            switch (Application.platform) {
                case RuntimePlatform.OSXEditor:
                    InitializeVSForMac(editorPath);
                    break;
                case RuntimePlatform.WindowsEditor:
                    InitializeVisualStudio(editorPath, installedVisualStudios);
                    break;
            }
        }

        void InitializeVSForMac(string externalEditor)
        {
            if (!IsVSForMac(externalEditor, out var vsfmVersion))
                return;

            BridgeFile = GetVSForMacBridgeAssembly(externalEditor, vsfmVersion);
            if (string.IsNullOrEmpty(BridgeFile) || !File.Exists(BridgeFile))
            {
                Debug.Log("Unable to find Tools for Unity bridge dll for Visual Studio for Mac " + externalEditor);
                return;
            }

            LoadAssembly(BridgeFile);
        }

        static bool IsVisualStudioForMac(string path)
        {
            var lowerCasePath = path.ToLower();
            var filename = Path.GetFileName(lowerCasePath).Replace(" ", "");
            return filename.StartsWith("visualstudio") && !filename.Contains("code") && filename.EndsWith(".app");
        }

        static bool IsVSForMac(string externalEditor, out Version vsfmVersion)
        {
            vsfmVersion = null;

            if (!IsVisualStudioForMac(externalEditor))
                return false;

            // We need to extract the version used by VS for Mac
            // to lookup its addin registry
            try
            {
                return GetVSForMacVersion(externalEditor, out vsfmVersion);
            }
            catch (Exception e)
            {
                Debug.Log("Failed to read Visual Studio for Mac information");
                Debug.LogException(e);
                return false;
            }
        }

        static bool GetVSForMacVersion(string externalEditor, out Version vsfmVersion)
        {
            vsfmVersion = null;

            // Read the full VS for Mac version from the plist, it will look like this:
            //
            // <key>CFBundleShortVersionString</key>
            // <string>X.X.X.X</string>

            var plist = Path.Combine(externalEditor, "Contents/Info.plist");
            if (!File.Exists(plist))
                return false;

            const string versionStringRegex = @"\<key\>CFBundleShortVersionString\</key\>\s+\<string\>(?<version>\d+\.\d+\.\d+\.\d+?)\</string\>";

            var file = File.ReadAllText(plist);
            var match = Regex.Match(file, versionStringRegex);
            var versionGroup = match.Groups["version"];
            if (!versionGroup.Success)
                return false;

            vsfmVersion = new Version(versionGroup.Value);
            return true;
        }

        void InitializeVisualStudio(string externalEditor, Dictionary<VisualStudioVersion, string[]> installedVisualStudios)
        {
            FindVisualStudio(externalEditor, out var vsVersion, installedVisualStudios);
            BridgeFile = GetVstuBridgeAssembly(vsVersion);
            if (BridgeFile == null)
            {
                Debug.Log("Unable to find bridge dll in registry for Microsoft Visual Studio Tools for Unity for " + externalEditor);
                return;
            }
            if (!File.Exists(BridgeFile))
            {
                Debug.Log("Unable to find bridge dll on disk for Microsoft Visual Studio Tools for Unity for " + BridgeFile);
                return;
            }

            LoadAssembly(BridgeFile);
        }
        
        static void LoadAssembly(string filename) 
        {
            var assembly = AppDomain.CurrentDomain.Load(AssemblyName.GetAssemblyName(filename));
            var autoInitTypes = assembly.GetTypes().Where(t => t.GetCustomAttributes(typeof(InitializeOnLoadAttribute)).Any());
            foreach(var type in autoInitTypes)
            {
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            }
        }

        static string GetVstuBridgeAssembly(VisualStudioVersion version)
        {
            try
            {
                var vsVersion = string.Empty;

                switch (version)
                {
                    // Starting with VS 15, the registry key is using the VS version
                    // to avoid taking a dependency on the product name
                    case VisualStudioVersion.VisualStudio2017:
                        vsVersion = "15.0";
                        break;
                    case VisualStudioVersion.VisualStudio2019:
                        vsVersion = "16.0";
                        break;
                    // VS 2015 and under are still installed in the registry
                    // using their project names
                    case VisualStudioVersion.VisualStudio2015:
                        vsVersion = "2015";
                        break;
                    case VisualStudioVersion.VisualStudio2013:
                        vsVersion = "2013";
                        break;
                    case VisualStudioVersion.VisualStudio2012:
                        vsVersion = "2012";
                        break;
                    case VisualStudioVersion.VisualStudio2010:
                        vsVersion = "2010";
                        break;
                }

                // search first for the current user with a fallback to machine wide setting
                return GetVstuBridgePathFromRegistry(vsVersion, true)
                    ?? GetVstuBridgePathFromRegistry(vsVersion, false);
            }
            catch (Exception)
            {
                return null;
            }
        }

        static string GetVstuBridgePathFromRegistry(string vsVersion, bool currentUser)
        {
            var registryKey = $@"{(currentUser ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE")}\Software\Microsoft\Microsoft Visual Studio {vsVersion} Tools for Unity";

            return (string)Registry.GetValue(registryKey, "UnityExtensionPath", null);
        }

        static void FindVisualStudio(string externalEditor, out VisualStudioVersion vsVersion, Dictionary<VisualStudioVersion, string[]> installedVisualStudios)
        {
            if (string.IsNullOrEmpty(externalEditor))
            {
                vsVersion = VisualStudioVersion.Invalid;
                return;
            }

            // If it's a VS found through envvars or the registry
            var matches = installedVisualStudios.Where(kvp => kvp.Value.Any(v => Path.GetFullPath(v).Equals(Path.GetFullPath(externalEditor), StringComparison.OrdinalIgnoreCase))).ToArray();
            if (matches.Length > 0)
            {
                vsVersion = matches[0].Key;
                return;
            }

            // If it's a side-by-side VS selected manually
            if (externalEditor.EndsWith("devenv.exe", StringComparison.OrdinalIgnoreCase))
            {
                if (TryGetVisualStudioVersion(externalEditor, out vsVersion)) return;
            }

            vsVersion = VisualStudioVersion.Invalid;
        }

        static bool TryGetVisualStudioVersion(string externalEditor, out VisualStudioVersion vsVersion)
        {
            switch (ProductVersion(externalEditor).Major)
            {
                case 9:
                    vsVersion = VisualStudioVersion.VisualStudio2008;
                    return true;
                case 10:
                    vsVersion = VisualStudioVersion.VisualStudio2010;
                    return true;
                case 11:
                    vsVersion = VisualStudioVersion.VisualStudio2012;
                    return true;
                case 12:
                    vsVersion = VisualStudioVersion.VisualStudio2013;
                    return true;
                case 14:
                    vsVersion = VisualStudioVersion.VisualStudio2015;
                    return true;
                case 15:
                    vsVersion = VisualStudioVersion.VisualStudio2017;
                    return true;
                case 16:
                    vsVersion = VisualStudioVersion.VisualStudio2019;
                    return true;
            }

            vsVersion = VisualStudioVersion.Invalid;
            return false;
        }

        static Version ProductVersion(string externalEditor)
        {
            try
            {
                return new Version(FileVersionInfo.GetVersionInfo(externalEditor).ProductVersion);
            }
            catch (Exception)
            {
                return new Version(0, 0);
            }
        }

        static string GetVSForMacBridgeAssembly(string externalEditor, Version vsfmVersion)
        {
            // Check first if we're overriden
            // Useful when developing UnityVS for Mac
            var bridge = Environment.GetEnvironmentVariable("VSTUM_BRIDGE");
            if (!string.IsNullOrEmpty(bridge) && File.Exists(bridge))
                return bridge;

            // Look for installed addin
            const string addinBridge = "Editor/SyntaxTree.VisualStudio.Unity.Bridge.dll";
            const string addinName = "MonoDevelop.Unity";

            // Check if we're installed in the user addins repository
            // ~/Library/Application Support/VisualStudio/X.0/LocalInstall/Addins
            var localAddins = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "Library/Application Support/VisualStudio/" + vsfmVersion.Major + ".0" + "/LocalInstall/Addins");

            // In the user addins repository, the addins are suffixed by their versions, like `MonoDevelop.Unity.1.0`
            // When installing another local user addin, MD will remove files inside the folder
            // So we browse all VSTUM addins, and return the one with a bridge, which is the one MD will load
            if (Directory.Exists(localAddins))
            {
                foreach (var folder in Directory.GetDirectories(localAddins, addinName + "*", SearchOption.TopDirectoryOnly))
                {
                    bridge = Path.Combine(folder, addinBridge);
                    if (File.Exists(bridge))
                        return bridge;
                }
            }

            // Check in Visual Studio.app/
            // In that case the name of the addin is used
            bridge = Path.Combine(externalEditor, "Contents/Resources/lib/monodevelop/AddIns/" + addinName + "/" + addinBridge);
            if (File.Exists(bridge))
                return bridge;

            return null;
        }
    }
}
