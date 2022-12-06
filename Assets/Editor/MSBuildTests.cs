using System.IO;
using System.Linq;
using Microsoft.Unity.VisualStudio.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Microsoft.Unity.VisualStudio.Standalone.EditorTests
{
	[UnityPlatform(RuntimePlatform.WindowsEditor)]
	internal class MSBuildTests : CleanupTest
	{
		private string m_msbuild;

		public override void SetUp()
		{
			base.SetUp();

			m_msbuild = FindMSBuild();
		}

		internal string FindMSBuild()
		{
			var vswhere = FileUtility.GetPackageAssetFullPath("Editor", "VSWhere", "vswhere.exe");

			Assert.IsTrue(!string.IsNullOrWhiteSpace(vswhere));

			var result = ProcessRunner.StartAndWaitForExit(vswhere, @"-latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe");

			Assert.IsTrue(result.Success);
			return result.Output;
		}

		[Test]
		public void MSBuildExists()
		{
			Assert.IsTrue(File.Exists(m_msbuild));
		}

		[TestCase(ProjectGenerationFlag.Embedded)]
		[TestCase(ProjectGenerationFlag.Local)]
		[TestCase(ProjectGenerationFlag.Git)]
		[TestCase(ProjectGenerationFlag.BuiltIn)]
		[TestCase(ProjectGenerationFlag.LocalTarBall)]
		[TestCase(ProjectGenerationFlag.Unknown)]
		[TestCase(ProjectGenerationFlag.PlayerAssemblies)]
		[TestCase(ProjectGenerationFlag.PlayerAssemblies | ProjectGenerationFlag.Embedded)] // default
		[TestCase(ProjectGenerationFlag.Registry | ProjectGenerationFlag.Embedded | ProjectGenerationFlag.PlayerAssemblies)]
		[TestCase(ProjectGenerationFlag.Embedded | ProjectGenerationFlag.Local | ProjectGenerationFlag.Registry | ProjectGenerationFlag.Git | ProjectGenerationFlag.BuiltIn | ProjectGenerationFlag.Unknown | ProjectGenerationFlag.PlayerAssemblies | ProjectGenerationFlag.LocalTarBall)] // full
		public void SolutionCompiles(ProjectGenerationFlag testFlag)
		{
			var provider = m_ProjectGeneration.AssemblyNameProvider;
			var flags = provider.ProjectGenerationFlag;

			try
			{
				CleanProjectFolder();

				provider.ToggleProjectGeneration(provider.ProjectGenerationFlag);
				provider.ToggleProjectGeneration(testFlag);
				m_ProjectGeneration.Sync();

				// try to build the solution
				const string msbuildArguments = "/t:clean,build /p:Configuration=Debug /nologo /verbosity:quiet /clp:ErrorsOnly";

				var result = ProcessRunner.StartAndWaitForExit(m_msbuild, $"{msbuildArguments} {m_ProjectGeneration.SolutionFile()}");

				Assert.IsTrue(result.Success);
				var output = result.Output;
				
				if (output.Contains("MSB5004"))
				{
					// see https://github.com/dotnet/msbuild/issues/530
					// Compiling in VS is not exactly the same as compiling the solution with MSBuild
					// VS allows two projects to have the same name in the solution, and will compute itself the project dependency list, then will build each project in order
					// So workaround this case by trying to call msbuild on all projects
					var projects = Directory.GetFiles(m_ProjectGeneration.ProjectDirectory, "*.csproj");
					foreach (var project in projects.Select(FileUtility.NormalizePathSeparators))
					{
						// try to build a project
						result = ProcessRunner.StartAndWaitForExit(m_msbuild, $"{msbuildArguments} {project}");

						Assert.IsTrue(result.Success);
						Assert.IsEmpty(result.Output);
					}
				}
				else
				{
					Assert.IsEmpty(output);
				}
			}
			finally
			{
				provider.ToggleProjectGeneration(provider.ProjectGenerationFlag);
				provider.ToggleProjectGeneration(flags);
			}
		}

		private void CleanProjectFolder()
		{
			var projects = Directory.GetFiles(m_ProjectGeneration.ProjectDirectory, "*.csproj");
			var solutions = Directory.GetFiles(m_ProjectGeneration.ProjectDirectory, "*.sln");

			foreach (var file in projects.Concat(solutions).Select(FileUtility.NormalizePathSeparators))
			{
				try
				{
					File.Delete(file);
				}
				catch (IOException)
				{
				}
			}
		}
	}
}
