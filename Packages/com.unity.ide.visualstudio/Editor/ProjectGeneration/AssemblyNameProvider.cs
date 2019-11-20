using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;

namespace Microsoft.Unity.VisualStudio.Editor
{
    public interface IAssemblyNameProvider
    {
        string[] ProjectSupportedExtensions { get; }
        string ProjectGenerationRootNamespace { get; }
        string GetAssemblyNameFromScriptPath(string path);
        bool IsInternalizedPackagePath(string path);
        IEnumerable<Assembly> GetAssemblies(Func<string, bool> shouldFileBePartOfSolution);
        IEnumerable<string> GetAllAssetPaths();
        UnityEditor.PackageManager.PackageInfo FindForAssetPath(string assetPath);
        ResponseFileData ParseResponseFile(string responseFilePath, string projectDirectory, string[] systemReferenceDirectories);
        void GeneratePlayerProjects(bool generatePlayerProjects);
    }

    public class AssemblyNameProvider : IAssemblyNameProvider
    {
        bool m_generatePlayerProjects;

        public string[] ProjectSupportedExtensions => EditorSettings.projectGenerationUserExtensions;

        public string ProjectGenerationRootNamespace => EditorSettings.projectGenerationRootNamespace;

        public string GetAssemblyNameFromScriptPath(string path)
        {
            return CompilationPipeline.GetAssemblyNameFromScriptPath(path);
        }

        public IEnumerable<Assembly> GetAssemblies(Func<string, bool> shouldFileBePartOfSolution)
        {

            return CompilationPipeline.GetAssemblies()
              .Where(i => 0 < i.sourceFiles.Length && i.sourceFiles.Any(shouldFileBePartOfSolution));
        }

        public IEnumerable<string> GetAllAssetPaths()
        {
            return AssetDatabase.GetAllAssetPaths();
        }

        public UnityEditor.PackageManager.PackageInfo FindForAssetPath(string assetPath)
        {
            return UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
        }

        public bool IsInternalizedPackagePath(string path)
        {
            if (string.IsNullOrEmpty(path.Trim()))
            {
                return false;
            }
            var packageInfo = FindForAssetPath(path);
            if (packageInfo == null)
            {
                return false;
            }
            var packageSource = packageInfo.source;
            return packageSource != PackageSource.Embedded && packageSource != PackageSource.Local;
        }

        public ResponseFileData ParseResponseFile(string responseFilePath, string projectDirectory, string[] systemReferenceDirectories)
        {
            return CompilationPipeline.ParseResponseFile(
              responseFilePath,
              projectDirectory,
              systemReferenceDirectories
            );
        }

        public void GeneratePlayerProjects(bool generatePlayerProjects)
        {
            m_generatePlayerProjects = generatePlayerProjects;
        }
    }
}