using System;
using System.IO;
using System.Linq;
using Moq;
using Unity.CodeEditor;
using UnityEditor.Compilation;

namespace Microsoft.Unity.VisualStudio.Editor.Tests
{
	class SynchronizerBuilder
	{
		class BuilderError : Exception
		{
			public BuilderError(string message)
				: base(message) { }
		}

		ProjectGeneration m_ProjectGeneration;
		Mock<IAssemblyNameProvider> m_AssemblyProvider = new Mock<IAssemblyNameProvider>();
		Assembly[] m_Assemblies;
		MyMockIExternalCodeEditor m_MockExternalCodeEditor = null;
		MockFileIO m_FileIoMock = new MockFileIO();
		Mock<IGUIDGenerator> m_GUIDGenerator = new Mock<IGUIDGenerator>();

		public const string projectDirectory = "/FullPath/Example";

		public string ReadFile(string fileName) => m_FileIoMock.ReadAllText(fileName);
		public string ProjectFilePath(Assembly assembly) => Path.Combine(projectDirectory, $"{assembly.name}.csproj");
		public string ReadProjectFile(Assembly assembly) => ReadFile(ProjectFilePath(assembly));
		public bool FileExists(string fileName) => m_FileIoMock.Exists(fileName);
		public void DeleteFile(string fileName) => m_FileIoMock.DeleteFile(fileName);
		public int WriteTimes => m_FileIoMock.WriteTimes;
		public int ReadTimes => m_FileIoMock.ReadTimes;

		public Assembly Assembly
		{
			get
			{
				if (m_Assemblies.Length > 0)
				{
					return m_Assemblies[0];
				}

				throw new BuilderError("An empty list of assemblies has been populated, and then the first assembly was requested.");
			}
		}

		public SynchronizerBuilder()
		{
			WithAssemblyData();
		}

		public ProjectGeneration Build()
		{
			return m_ProjectGeneration = new ProjectGeneration(projectDirectory, m_AssemblyProvider.Object, m_FileIoMock, m_GUIDGenerator.Object);
		}

		public SynchronizerBuilder WithSolutionText(string solutionText)
		{
			if (m_ProjectGeneration == null)
			{
				throw new BuilderError("You need to call Build() before calling this method.");
			}

			m_FileIoMock.WriteAllText(m_ProjectGeneration.SolutionFile(), solutionText);
			return this;
		}

		public SynchronizerBuilder WithSolutionGuid(string solutionGuid)
		{
			m_GUIDGenerator.Setup(x => x.SolutionGuid(Path.GetFileName(projectDirectory), ScriptingLanguage.CSharp)).Returns(solutionGuid);
			return this;
		}

		public SynchronizerBuilder WithProjectGuid(string projectGuid, Assembly assembly)
		{
			m_GUIDGenerator.Setup(x => x.ProjectGuid(Path.GetFileName(projectDirectory), assembly.name)).Returns(projectGuid);
			return this;
		}

		public SynchronizerBuilder WithAssemblies(Assembly[] assemblies)
		{
			m_Assemblies = assemblies;
			m_AssemblyProvider.Setup(x => x.GetAssemblies(It.IsAny<Func<string, bool>>())).Returns(m_Assemblies);

			foreach (var assembly in assemblies)
			{
				m_AssemblyProvider.Setup(x => x.GetAssemblyName(assembly.outputPath, assembly.name)).Returns(assembly.name);

				foreach (var reference in assembly.assemblyReferences)
				{
					m_AssemblyProvider.Setup(x => x.GetAssemblyName(It.IsAny<string>(), reference.name)).Returns(reference.name);
				}
			}

			return this;
		}

		public SynchronizerBuilder WithAssemblyData(string[] files = null, string[] defines = null, Assembly[] assemblyReferences = null, string[] compiledAssemblyReferences = null, bool unsafeSettings = false, string rootNamespace = "")
		{
			var options = new ScriptCompilerOptions() { AllowUnsafeCode = unsafeSettings };

			var assembly = new Assembly(
				"Test",
				"some/path/file.dll",
				files ?? new[] { "test.cs" },
				defines ?? new string[0],
				assemblyReferences ?? new Assembly[0],
				compiledAssemblyReferences ?? new string[0],
				AssemblyFlags.None,
#if UNITY_2020_2_OR_NEWER
				options,
				rootNamespace);
#else
				options);
#endif
			return WithAssembly(assembly);
		}

		public SynchronizerBuilder WithLatestLanguageVersionSupported(Version version)
		{
			m_MockExternalCodeEditor = new MyMockIExternalCodeEditor(version);
			CodeEditor.Register(m_MockExternalCodeEditor);

			return this;
		}

		public SynchronizerBuilder WithAssembly(Assembly assembly)
		{
			AssignFilesToAssembly(assembly.sourceFiles, assembly);
			return WithAssemblies(new[] { assembly });
		}

		public SynchronizerBuilder WithAssetFiles(string[] files)
		{
			m_AssemblyProvider.Setup(x => x.GetAllAssetPaths()).Returns(files);
			return this;
		}

		public SynchronizerBuilder AssignFilesToAssembly(string[] files, Assembly assembly)
		{
			m_AssemblyProvider.Setup(x => x.GetAssemblyNameFromScriptPath(It.Is<string>(file => files.Contains(file)))).Returns(assembly.name);
			return this;
		}

		public SynchronizerBuilder WithResponseFileData(Assembly assembly, string responseFile, string[] defines = null, string[] errors = null, string[] fullPathReferences = null, string[] otherArguments = null, bool _unsafe = false)
		{
			assembly.compilerOptions.ResponseFiles = new[] { responseFile };
			m_AssemblyProvider.Setup(x => x.ParseResponseFile(responseFile, projectDirectory, It.IsAny<string[]>())).Returns(new ResponseFileData
			{
				Defines = defines ?? new string[0],
				Errors = errors ?? new string[0],
				FullPathReferences = fullPathReferences ?? new string[0],
				OtherArguments = otherArguments ?? new string[0],
				Unsafe = _unsafe,
			});
			return WithLatestLanguageVersionSupported(null); // we need a mocked VS instance to process analyzers in 'OtherArguments' from the response file 
		}

		public SynchronizerBuilder WithPackageInfo(string assetPath)
		{
			m_AssemblyProvider.Setup(x => x.FindForAssetPath(assetPath)).Returns(default(UnityEditor.PackageManager.PackageInfo));
			return this;
		}

		public SynchronizerBuilder WithPackageAsset(string assetPath, bool isInternalPackageAsset)
		{
			m_AssemblyProvider.Setup(x => x.IsInternalizedPackagePath(assetPath)).Returns(isInternalPackageAsset);
			return this;
		}

		public SynchronizerBuilder WithUserSupportedExtensions(string[] extensions)
		{
			m_AssemblyProvider.Setup(x => x.ProjectSupportedExtensions).Returns(extensions);
			return this;
		}

		public SynchronizerBuilder WithOutputPathForAssemblyPath(string outputPath, string assemblyName, string resAssemblyName)
		{
			m_AssemblyProvider.Setup(x => x.GetAssemblyName(outputPath, assemblyName)).Returns(resAssemblyName);
			return this;
		}

#if UNITY_2020_2_OR_NEWER
		public SynchronizerBuilder WithRoslynAnalyzers(string[] roslynAnalyzerDllPaths)
		{
			m_MockExternalCodeEditor = new MyMockIExternalCodeEditor();
			CodeEditor.Register(m_MockExternalCodeEditor);

			foreach (Assembly assembly in m_Assemblies)
			{
				assembly.compilerOptions.RoslynAnalyzerDllPaths = roslynAnalyzerDllPaths;
			}
			return this;
		}

		public SynchronizerBuilder WithRulesetPath(string rulesetFilePath)
		{
			m_MockExternalCodeEditor = new MyMockIExternalCodeEditor();
			CodeEditor.Register(m_MockExternalCodeEditor);

			foreach (Assembly assembly in m_Assemblies)
			{
				assembly.compilerOptions.RoslynAnalyzerRulesetPath = rulesetFilePath;
			}
			return this;
		}
#endif

		public class MyMockIExternalCodeEditor : VisualStudioEditor
		{
			private Version LatestLanguageVersionSupported = new Version(7, 3);
			public MyMockIExternalCodeEditor(Version version = null)
			{
				if (version != null)
					LatestLanguageVersionSupported = version;
			}

			public override bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
			{
				installation = new CodeEditor.Installation
				{
					Name = editorPath,
					Path = editorPath
				};
				return true;
			}

			internal override bool TryGetVisualStudioInstallationForPath(string editorPath, bool searchInstallations, out IVisualStudioInstallation installation)
			{
				var mock = new Mock<IVisualStudioInstallation>();
				mock.Setup(x => x.SupportsAnalyzers).Returns(true);
				mock.Setup(x => x.LatestLanguageVersionSupported).Returns(() => LatestLanguageVersionSupported);

				installation = mock.Object;

				return true;
			}
		}

		public void CleanUp()
		{
			if (m_MockExternalCodeEditor != null)
			{
				CodeEditor.Unregister(m_MockExternalCodeEditor);
			}
		}
	}
}
