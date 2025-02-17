using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;

namespace Microsoft.Unity.VisualStudio.Editor.Tests
{
	public class AssemblyNameProviderTests
	{
		AssemblyNameProvider m_AssemblyNameProvider;
		ProjectGenerationFlag m_Flag;

		[OneTimeSetUp]
		public void OneTimeSetUp()
		{
			m_AssemblyNameProvider = new AssemblyNameProvider();
			m_Flag = m_AssemblyNameProvider.ProjectGenerationFlag;
		}

		[OneTimeTearDown]
		public void OneTimeTearDown()
		{
			m_AssemblyNameProvider.ToggleProjectGeneration(ProjectGenerationFlag.None);
			m_AssemblyNameProvider.ToggleProjectGeneration(m_Flag);
		}

		[SetUp]
		public void SetUp()
		{
			m_AssemblyNameProvider.ResetProjectGenerationFlag();
		}

		[TestCase(@"Temp\bin\Debug\", "AssemblyName", "AssemblyName")]
		[TestCase(@"Temp\bin\Debug\", "My.Player.AssemblyName", "My.Player.AssemblyName")]
		[TestCase(@"Temp\bin\Debug\", "AssemblyName.Player", "AssemblyName.Player")]
		[TestCase(@"Temp\bin\Debug\Player\", "AssemblyName", "AssemblyName.Player")]
		[TestCase(@"Temp\bin\Debug\Player\", "AssemblyName.Player", "AssemblyName.Player.Player")]
		public void GetOutputPath_ReturnsPlayerAndeditorOutputPath(string assemblyOutputPath, string assemblyName, string expectedAssemblyName)
		{
			Assert.AreEqual(expectedAssemblyName, m_AssemblyNameProvider.GetAssemblyName(assemblyOutputPath.NormalizePathSeparators(), assemblyName));
		}

		[Test]
		public void AllEditorAssemblies_AreCollected()
		{
			var editorAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);

			var collectedAssemblies = m_AssemblyNameProvider.GetAssemblies(s => true).ToList();

			foreach (Assembly editorAssembly in editorAssemblies)
			{
				Assert.IsTrue(collectedAssemblies.Any(assembly => assembly.name == editorAssembly.name && assembly.outputPath == AssemblyNameProvider.AssemblyOutput), $"{editorAssembly.name}: was not found in collection.");
			}
		}

#if UNITY_2020_2_OR_NEWER
        [Test]
        public void EditorAssemblies_WillIncludeRootNamespace()
        {
            var editorAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            var collectedAssemblies = m_AssemblyNameProvider.GetAssemblies(s => true).ToList();

            var editorTestAssembly = editorAssemblies.Single(a => a.name == "Unity.VisualStudio.EditorTests");
            Assert.AreEqual("Microsoft.Unity.VisualStudio.Editor.Tests", editorTestAssembly.rootNamespace);

            var collectedTestAssembly = collectedAssemblies.Single(a => a.name == editorTestAssembly.name);
            Assert.AreEqual(editorTestAssembly.rootNamespace, collectedTestAssembly.rootNamespace);
        }
#endif

		/* This is legacy, and we have now MSBuild tests validating that the solution is compiling properly
		[Test]
		public void AllEditorAssemblies_HaveAReferenceToUnityEditorAndUnityEngine()
		{
			var editorAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);

			foreach (Assembly editorAssembly in editorAssemblies)
			{
				Assert.IsTrue(editorAssembly.allReferences.Any(reference => reference.EndsWith("UnityEngine.dll")));
				Assert.IsTrue(editorAssembly.allReferences.Any(reference => reference.EndsWith("UnityEditor.dll")));
			}
		}
		*/

		[Test]
		public void PlayerAssemblies_AreNotCollected_BeforeToggling()
		{
			var playerAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);

			var collectedAssemblies = m_AssemblyNameProvider.GetAssemblies(s => true).ToList();

			foreach (Assembly playerAssembly in playerAssemblies)
			{
				Assert.IsFalse(collectedAssemblies.Any(assembly => assembly.name == playerAssembly.name && assembly.outputPath == AssemblyNameProvider.PlayerAssemblyOutput), $"{playerAssembly.name}: was found in collection.");
			}
		}

		[Test]
		public void AllPlayerAssemblies_AreCollected_AfterToggling()
		{
			var playerAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);

			m_AssemblyNameProvider.ToggleProjectGeneration(ProjectGenerationFlag.PlayerAssemblies);

			var collectedAssemblies = m_AssemblyNameProvider.GetAssemblies(s => true).ToList();

			foreach (Assembly playerAssembly in playerAssemblies)
			{
				Assert.IsTrue(collectedAssemblies.Any(assembly => assembly.name == playerAssembly.name && assembly.outputPath == AssemblyNameProvider.PlayerAssemblyOutput), $"{playerAssembly.name}: was not found in collection.");
			}
		}

		[Test]
		public void AsDefaultArgument_ProjectGeneration_WillBeLocalAndEmbedded()
		{
			EditorPrefs.DeleteKey("unity_project_generation_flag");
			m_AssemblyNameProvider = new AssemblyNameProvider();

			Assert.That(
				m_AssemblyNameProvider.ProjectGenerationFlag,
				Is.EqualTo(ProjectGenerationFlag.Local | ProjectGenerationFlag.Embedded),
				"The default ProjectGenerationFlag should be (Local | Embedded)");
		}
	}
}
