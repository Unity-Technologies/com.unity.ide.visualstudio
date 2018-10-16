using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using UnityEditor;
using UnityEngine;

namespace VisualStudioEditor
{
    internal enum VisualStudioVersion
    {
        Invalid = 0,
        VisualStudio2008 = 9,
        VisualStudio2010 = 10,
        VisualStudio2012 = 11,
        VisualStudio2013 = 12,
        VisualStudio2015 = 14,
        VisualStudio2017 = 15,
    }

    [InitializeOnLoad]
    internal class SyncVS : IExternalScriptEditor
    {

        internal class VisualStudioPath
        {
            public string Path { get; set; }
            public string Edition { get; set; }

            public VisualStudioPath(string path, string edition = "")
            {
                Path = path;
                Edition = edition;
            }
        }

        bool m_ExternalEditorSupportsUnityProj;
        static readonly string k_ExpressNotSupportedMessage = L10n.Tr(
            "Unfortunately Visual Studio Express does not allow itself to be controlled by external applications. " +
            "You can still use it by manually opening the Visual Studio project file, but Unity cannot automatically open files for you when you doubleclick them. " +
            "\n(This does work with Visual Studio Pro)"
        );

        static string VisualStudioVersionToNiceName(VisualStudioVersion version)
        {
            switch (version)
            {
                case VisualStudioVersion.Invalid: return "Invalid Version";
                case VisualStudioVersion.VisualStudio2008: return "Visual Studio 2008";
                case VisualStudioVersion.VisualStudio2010: return "Visual Studio 2010";
                case VisualStudioVersion.VisualStudio2012: return "Visual Studio 2012";
                case VisualStudioVersion.VisualStudio2013: return "Visual Studio 2013";
                case VisualStudioVersion.VisualStudio2015: return "Visual Studio 2015";
                case VisualStudioVersion.VisualStudio2017: return "Visual Studio 2017";
                default:
                    throw new ArgumentOutOfRangeException(nameof(version), version, null);
            }
        }

        static IEnumerable<string> FindVisualStudioDevEnvPaths()
        {
            var progpath = $"{Application.dataPath}/../Packages/com.unity.visualstudio_editor/VSIntegration/VSWhere/VSWhere.exe";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = progpath,
                    Arguments = "-products * -property productPath",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

            process.Start();
            process.WaitForExit();

            while (!process.StandardOutput.EndOfStream)
            {
                yield return process.StandardOutput.ReadLine();
            }
        }

        static SyncVS()
        {
            try
            {
                InstalledVisualStudios = GetInstalledVisualStudios() as Dictionary<VisualStudioVersion, VisualStudioPath[]>;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error detecting Visual Studio installations: {0}{1}{2}", ex.Message, Environment.NewLine, ex.StackTrace);
                InstalledVisualStudios = new Dictionary<VisualStudioVersion, VisualStudioPath[]>();
            }
            ScriptEditor.Register(new SyncVS());
        }

        ScriptEditor.Installation m_Installation;
        ProjectGenerationVS m_Generation = new ProjectGenerationVS();
        VSInitiliazer m_Initiliazer = new VSInitiliazer();

        internal static Dictionary<VisualStudioVersion, VisualStudioPath[]> InstalledVisualStudios { get; private set; }

        static bool IsOSX => Environment.OSVersion.Platform == PlatformID.Unix;
        static bool IsWindows => !IsOSX && Path.DirectorySeparatorChar == '\\' && Environment.NewLine == "\r\n";
        static readonly GUIContent k_AddUnityProjeToSln = EditorGUIUtility.TrTextContent("Add .unityproj's to .sln");

        static string GetRegistryValue(string path, string key)
        {
            try
            {
                return Registry.GetValue(path, key, null) as string;
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>
        /// Derives the Visual Studio installation path from the debugger path
        /// </summary>
        /// <returns>
        /// The Visual Studio installation path (to devenv.exe)
        /// </returns>
        /// <param name='debuggerPath'>
        /// The debugger path from the windows registry
        /// </param>
        static string DeriveVisualStudioPath(string debuggerPath)
        {
            string startSentinel = DeriveProgramFilesSentinel();
            string endSentinel = "Common7";
            bool started = false;
            string[] tokens = debuggerPath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            string path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            // Walk directories in debugger path, chop out "Program Files\INSTALLATION\PATH\HERE\Common7"
            foreach (var token in tokens)
            {
                if (!started && string.Equals(startSentinel, token, StringComparison.OrdinalIgnoreCase))
                {
                    started = true;
                    continue;
                }
                if (started)
                {
                    path = Path.Combine(path, token);
                    if (string.Equals(endSentinel, token, StringComparison.OrdinalIgnoreCase))
                        break;
                }
            }

            return Path.Combine(path, "IDE", "devenv.exe");
        }

        /// <summary>
        /// Derives the program files sentinel for grabbing the VS installation path.
        /// </summary>
        /// <remarks>
        /// From a path like 'c:\Archivos de programa (x86)', returns 'Archivos de programa'
        /// </remarks>
        static string DeriveProgramFilesSentinel()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .LastOrDefault();

            if (!string.IsNullOrEmpty(path))
            {
                // This needs to be the "real" Program Files regardless of 64bitness
                int index = path.LastIndexOf("(x86)");
                if (0 <= index)
                    path = path.Remove(index);
                return path.TrimEnd();
            }

            return "Program Files";
        }

        public ScriptEditor.Installation[] Installations
        {
            get
            {
                try
                {
                    return GetInstalledVisualStudios().Select(pair => new ScriptEditor.Installation
                    {
                        Path = pair.Value[0].Path,
                        Name = VisualStudioVersionToNiceName(pair.Key)
                    }).ToArray();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error detecting Visual Studio installations: {0}{1}{2}", ex.Message, Environment.NewLine, ex.StackTrace);
                    //InstalledVisualStudios = new Dictionary<VisualStudioVersion, VisualStudioPath[]>();
                    return new ScriptEditor.Installation[0];
                }
            }
        }

        public class VisualStudio
        {
            public readonly string DevEnvPath;
            public readonly string Edition;

            internal VisualStudio(string devEnvPath, string edition)
            {
                DevEnvPath = devEnvPath;
                Edition = edition;
            }
        }

        public static IEnumerable<VisualStudio> ParseRawDevEnvPaths(string[] rawDevEnvPaths)
        {
            if (rawDevEnvPaths == null)
            {
                yield break;
            }

            foreach (var path in rawDevEnvPaths)
            {
                yield return new VisualStudio(
                    path,
                    path.Contains("2017") ? "2017" : "Unknown");
            }
        }

        /// <summary>
        /// Detects Visual Studio installations using the Windows registry
        /// </summary>
        /// <returns>
        /// The detected Visual Studio installations
        /// </returns>
        static IDictionary<VisualStudioVersion, VisualStudioPath[]> GetInstalledVisualStudios()
        {
            var versions = new Dictionary<VisualStudioVersion, VisualStudioPath[]>();

            if (IsWindows)
            {
                foreach (VisualStudioVersion version in Enum.GetValues(typeof(VisualStudioVersion)))
                {
                    if (version > VisualStudioVersion.VisualStudio2015)
                        continue;

                    try
                    {
                        // Try COMNTOOLS environment variable first
                        FindLegacyVisualStudio(version, versions);
                    }
                    catch
                    {
                        // This can happen with a registry lookup failure
                    }
                }

                //var requiredWorkloads = new[] {"Microsoft.VisualStudio.Workload.ManagedGame"}; // TODO:
                var raw = FindVisualStudioDevEnvPaths();

                var visualStudioPaths = ParseRawDevEnvPaths(raw.ToArray())
                    .Select(vs => new VisualStudioPath(vs.DevEnvPath, vs.Edition))
                    .ToArray();

                if (visualStudioPaths.Length != 0)
                {
                    versions[VisualStudioVersion.VisualStudio2017] = visualStudioPaths;
                }
            }

            return versions;
        }

        static void FindLegacyVisualStudio(VisualStudioVersion version, Dictionary<VisualStudioVersion, VisualStudioPath[]> versions)
        {
            string key = Environment.GetEnvironmentVariable($"VS{(int)version}0COMNTOOLS");
            if (!string.IsNullOrEmpty(key))
            {
                string path = Path.Combine(key, "..", "IDE", "devenv.exe");
                if (File.Exists(path))
                {
                    versions[version] = new[] { new VisualStudioPath(path, VisualStudioVersionToNiceName(version)) };
                    return;
                }
            }

            // Try the proper registry key
            key = GetRegistryValue(
                $@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\{(int)version}.0", "InstallDir");

            // Try to fallback to the 32bits hive
            if (string.IsNullOrEmpty(key))
                key = GetRegistryValue(
                    $@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\{(int)version}.0", "InstallDir");

            if (!string.IsNullOrEmpty(key))
            {
                string path = Path.Combine(key, "devenv.exe");
                if (File.Exists(path))
                {
                    versions[version] = new[] { new VisualStudioPath(path, VisualStudioVersionToNiceName(version)) };
                    return;
                }
            }

            // Fallback to debugger key
            key = GetRegistryValue(
                // VS uses this key for the local debugger path
                $@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\{(int)version}.0\Debugger", "FEQARuntimeImplDll");
            if (!string.IsNullOrEmpty(key))
            {
                string path = DeriveVisualStudioPath(key);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    versions[version] = new[] { new VisualStudioPath(DeriveVisualStudioPath(key), VisualStudioVersionToNiceName(version)) };
            }
        }

        public bool TryGetInstallationForPath(string editorPath, out ScriptEditor.Installation installation)
        {
            var lowerCasePath = editorPath.ToLower();
            if (lowerCasePath.EndsWith("vcsexpress.exe"))
            {
                installation = new ScriptEditor.Installation
                {
                    Name = "VSExpress",
                    Path = editorPath
                };
                m_Installation = installation;
                return true;
            }

            if (lowerCasePath.EndsWith("devenv.exe"))
            {
                installation = new ScriptEditor.Installation
                {
                    Name = "VisualStudio",
                    Path = editorPath
                };
                m_Installation = installation;
                return true;
            }
            var filename = Path.GetFileName(lowerCasePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)).Replace(" ", "");

            if (filename == "visualstudio.app" || lowerCasePath.Contains("monodevelop") || lowerCasePath.Contains("xamarinstudio") || lowerCasePath.Contains("xamarin studio"))
            {
                installation = new ScriptEditor.Installation
                {
                    Name = "MonoDevelop",
                    Path = editorPath
                };
                m_Installation = installation;
                return true;
            }

            installation = default;
            m_Installation = installation;
            return false;
        }

        // TODO: Figure out how to make sure which instance we are calling? Maybe passing the installation to this function
        public void OnGUI()
        {
            if (m_Installation.Name.Equals("VSExpress"))
            {
                GUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label("", "CN EntryWarn");
                GUILayout.Label(k_ExpressNotSupportedMessage, "WordWrappedLabel");
                GUILayout.EndHorizontal();
            }

            if (m_Installation.Name.Equals("MonoDevelop"))
            {
                m_ExternalEditorSupportsUnityProj = EditorGUILayout.Toggle(
                    k_AddUnityProjeToSln,
                    m_ExternalEditorSupportsUnityProj);
            }
        }

        public void SyncIfNeeded(IEnumerable<string> affectedFiles, IEnumerable<string> reimportedFiles)
        {
            m_Generation.SyncIfNeeded(affectedFiles, reimportedFiles);
        }

        public void Sync()
        {
            m_Generation.Sync();
        }

        public void Initialize(string editorInstallationPath)
        {
            m_Initiliazer.Initialize(editorInstallationPath, InstalledVisualStudios);
        }

        public bool OpenFileAtLine(string path, int line)
        {
            var progpath = $"{Application.dataPath}/../Packages/com.unity.visualstudio_editor/VSIntegration/COMIntegration/Debug/COMIntegration.exe";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = progpath,
                    Arguments = $"\"{EditorPrefs.GetString("kScriptsDefaultApp")}\" \"{path}\" \"{m_Generation.SolutionFile()}\" {line}",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };
            var result = process.Start();

            while (!process.StandardOutput.EndOfStream)
            {
                if (process.StandardOutput.ReadLine() == "displayProgressBar")
                {
                    EditorUtility.DisplayProgressBar("Opening Visual Studio", "Starting up Visual Studio, this might take some time.", .5f);
                }

                if (process.StandardOutput.ReadLine() == "clearprogressbar")
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            UnityEngine.Debug.Log("Error: \n" + process.StandardError.ReadToEnd());
            process.WaitForExit();
            return result;
        }
    }
}
