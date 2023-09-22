using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Microsoft.Unity.VisualStudio.Editor.Tests
{
	namespace CSProjectGeneration
	{
		static class Util
		{
			internal static bool MatchesRegex(this string input, string pattern)
			{
				return Regex.Match(input, pattern).Success;
			}

			internal static string ReplaceDirectorySeparators(this string input)
			{
				return input.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
			}
		}

		class Formatting : SolutionGenerationTestBase
		{
			[TestCase(@"x & y.cs", @"x &amp; y.cs")]
			[TestCase(@"x ' y.cs", @"x &apos; y.cs")]
			[TestCase(@"Dimmer&\foo.cs", @"Dimmer&amp;\foo.cs")]
			[TestCase(@"C:\Dimmer/foo.cs", @"C:\Dimmer\foo.cs")]
			public void Escape_SpecialCharsInFileName(string illegalFormattedFileName, string expectedFileName)
			{
				var synchronizer = m_Builder.WithAssemblyData(files: new[] { illegalFormattedFileName }).Build();

				synchronizer.Sync();

				var csprojContent = m_Builder.ReadProjectFile(m_Builder.Assembly);
				StringAssert.DoesNotContain(illegalFormattedFileName, csprojContent);
				StringAssert.Contains(expectedFileName.ReplaceDirectorySeparators(), csprojContent);
			}

			[Test]
			public void NoExtension_IsNotValid()
			{
				var validFile = "dimmer.cs";
				var invalidFile = "foo";
				var file = new[] { validFile, invalidFile };
				var synchronizer = m_Builder.WithAssemblyData(files: file).Build();

				synchronizer.Sync();

				var csprojContent = m_Builder.ReadProjectFile(m_Builder.Assembly);
				XmlDocument scriptProject = XMLUtilities.FromText(csprojContent);
				XMLUtilities.AssertCompileItemsMatchExactly(scriptProject, new[] { validFile });
			}

			[Test]
			public void AbsoluteSourceFilePaths_WillBeMadeRelativeToProjectDirectory()
			{
				var absoluteFilePath = Path.Combine(SynchronizerBuilder.projectDirectory, "dimmer.cs");
				var synchronizer = m_Builder.WithAssemblyData(files: new[] { absoluteFilePath }).Build();

				synchronizer.Sync();

				var csprojContent = m_Builder.ReadProjectFile(m_Builder.Assembly)
					;
				XmlDocument scriptProject = XMLUtilities.FromText(csprojContent);
				XMLUtilities.AssertCompileItemsMatchExactly(scriptProject, new[] { "dimmer.cs" });
			}

			[Test]
			public void ProjectGeneration_UseAssemblyNameProvider_ForOutputPath()
			{
				var expectedAssemblyName = "my.AssemblyName";
				var synchronizer = m_Builder.WithOutputPathForAssemblyPath(m_Builder.Assembly.outputPath, m_Builder.Assembly.name, expectedAssemblyName).Build();

				synchronizer.Sync();

				Assert.That(m_Builder.FileExists(Path.Combine(SynchronizerBuilder.projectDirectory, $"{expectedAssemblyName}.csproj")));
			}

			private enum ProjectType
			{
				GamePlugins = 3,
				Game = 1,
				EditorPlugins = 7,
				Editor = 5,
			}

			private static ProjectType ProjectTypeOf(string fileName)
			{
				var plugins = fileName.Contains("firstpass");
				var editor = fileName.Contains("Editor");

				if (plugins && editor)
					return ProjectType.EditorPlugins;
				if (plugins)
					return ProjectType.GamePlugins;
				if (editor)
					return ProjectType.Editor;

				return ProjectType.Game;
			}

			[Test]
			public void DefaultSyncSettings_WhenSynced_CreatesProjectFileFromDefaultTemplate()
			{
				var projectGuid = "ProjectGuid";
				var synchronizer = m_Builder.WithProjectGuid(projectGuid, m_Builder.Assembly).Build();

				synchronizer.Sync();

				var csprojContent = m_Builder.ReadProjectFile(m_Builder.Assembly);

				var projectType = ProjectTypeOf(m_Builder.Assembly.name);
				var buildTarget = projectType + ":" + (int)projectType;
				var unityVersion = Application.unityVersion;
				var packageVersion = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AssemblyNameProvider).Assembly).version;

				var content = new[]
				{
					"<?xml version=\"1.0\" encoding=\"utf-8\"?>",
					"<Project ToolsVersion=\"4.0\" DefaultTargets=\"Build\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">",
					"  <!-- Generated file, do not modify, your changes will be overwritten (use AssetPostprocessor.OnGeneratedCSProject) -->",
					"  <PropertyGroup>",
					"    <LangVersion>latest</LangVersion>",
					"  </PropertyGroup>",
					"  <PropertyGroup>",
					"    <Configuration Condition=\" '$(Configuration)' == '' \">Debug</Configuration>",
					"    <Platform Condition=\" '$(Platform)' == '' \">AnyCPU</Platform>",
					"    <ProductVersion>10.0.20506</ProductVersion>",
					"    <SchemaVersion>2.0</SchemaVersion>",
					"    <RootNamespace></RootNamespace>",
					$"    <ProjectGuid>{{{projectGuid}}}</ProjectGuid>",
					"    <OutputType>Library</OutputType>",
					"    <AppDesignerFolder>Properties</AppDesignerFolder>",
					$"    <AssemblyName>{m_Builder.Assembly.name}</AssemblyName>",
					"    <TargetFrameworkVersion>v4.7.1</TargetFrameworkVersion>",
					"    <FileAlignment>512</FileAlignment>",
					"    <BaseDirectory>.</BaseDirectory>",
					"  </PropertyGroup>",
					"  <PropertyGroup Condition=\" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' \">",
					"    <DebugSymbols>true</DebugSymbols>",
					"    <DebugType>full</DebugType>",
					"    <Optimize>false</Optimize>",
					$"    <OutputPath>{m_Builder.Assembly.outputPath}</OutputPath>",
					$"    <DefineConstants></DefineConstants>",
					"    <ErrorReport>prompt</ErrorReport>",
					"    <WarningLevel>4</WarningLevel>",
					"    <NoWarn>0169;USG0001</NoWarn>",
					"    <AllowUnsafeBlocks>False</AllowUnsafeBlocks>",
					"  </PropertyGroup>",
					"  <PropertyGroup Condition=\" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' \">",
					"    <DebugType>pdbonly</DebugType>",
					"    <Optimize>true</Optimize>",
					"    <OutputPath>Temp\\bin\\Release\\</OutputPath>",
					"    <ErrorReport>prompt</ErrorReport>",
					"    <WarningLevel>4</WarningLevel>",
					"    <NoWarn>0169;USG0001</NoWarn>",
					"    <AllowUnsafeBlocks>False</AllowUnsafeBlocks>",
					"  </PropertyGroup>",
					"  <PropertyGroup>",
					"    <NoConfig>true</NoConfig>",
					"    <NoStdLib>true</NoStdLib>",
					"    <AddAdditionalExplicitAssemblyReferences>false</AddAdditionalExplicitAssemblyReferences>",
					"    <ImplicitlyExpandNETStandardFacades>false</ImplicitlyExpandNETStandardFacades>",
					"    <ImplicitlyExpandDesignTimeFacades>false</ImplicitlyExpandDesignTimeFacades>",
					"  </PropertyGroup>",
					"  <PropertyGroup>",
					"    <ProjectTypeGuids>{E097FAD1-6243-4DAD-9C02-E9B9EFC3FFC1};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids>",
					"    <UnityProjectGenerator>Package</UnityProjectGenerator>",
					$"    <UnityProjectGeneratorVersion>{packageVersion}</UnityProjectGeneratorVersion>",
					$"    <UnityProjectType>{buildTarget}</UnityProjectType>",
					$"    <UnityBuildTarget>{EditorUserBuildSettings.activeBuildTarget + ":" + (int)EditorUserBuildSettings.activeBuildTarget}</UnityBuildTarget>",
					$"    <UnityVersion>{unityVersion}</UnityVersion>",
					"  </PropertyGroup>",
					"  <ItemGroup>",
					"    <Compile Include=\"test.cs\" />",
					"  </ItemGroup>",
					"  <ItemGroup>",
					"  </ItemGroup>",
					"  <Import Project=\"$(MSBuildToolsPath)\\Microsoft.CSharp.targets\" />",
					"  <Target Name=\"GenerateTargetFrameworkMonikerAttribute\" />",
					"  <!-- To modify your build process, add your task inside one of the targets below and uncomment it.",
					"       Other similar extension points exist, see Microsoft.Common.targets.",
					"  <Target Name=\"BeforeBuild\">",
					"  </Target>",
					"  <Target Name=\"AfterBuild\">",
					"  </Target>",
					"  -->",
					"</Project>",
					""
				};

				StringAssert.AreEqualIgnoringCase(string.Join("\r\n", content), csprojContent);
			}
		}

		class GUID : SolutionGenerationTestBase
		{
			[Test]
			public void ProjectReference_MatchAssemblyGUID()
			{
				string[] files = { "test.cs" };
				var assemblyB = new Assembly("Test", "Temp/Test.dll", files, new string[0], new Assembly[0], new string[0], AssemblyFlags.None);
				var assemblyA = new Assembly("Test2", "some/path/file.dll", files, new string[0], new[] { assemblyB }, new string[0], AssemblyFlags.None);
				var synchronizer = m_Builder.WithAssemblies(new[] { assemblyA, assemblyB }).Build();

				synchronizer.Sync();

				var assemblyACSproject = Path.Combine(SynchronizerBuilder.projectDirectory, $"{assemblyA.name}.csproj");
				var assemblyBCSproject = Path.Combine(SynchronizerBuilder.projectDirectory, $"{assemblyB.name}.csproj");

				Assert.True(m_Builder.FileExists(assemblyACSproject));
				Assert.True(m_Builder.FileExists(assemblyBCSproject));

				XmlDocument scriptProject = XMLUtilities.FromText(m_Builder.ReadFile(assemblyACSproject));
				XmlDocument scriptPluginProject = XMLUtilities.FromText(m_Builder.ReadFile(assemblyBCSproject));

				var a = XMLUtilities.GetInnerText(scriptPluginProject, "/msb:Project/msb:PropertyGroup/msb:ProjectGuid");
				var b = XMLUtilities.GetInnerText(scriptProject, "/msb:Project/msb:ItemGroup/msb:ProjectReference/msb:Project");
				Assert.AreEqual(a, b);
			}
		}

		class Synchronization : SolutionGenerationTestBase
		{
			[Test]
			public void WontSynchronize_WhenNoFilesChanged()
			{
				var synchronizer = m_Builder.Build();

				synchronizer.Sync();
				Assert.AreEqual(2, m_Builder.WriteTimes, "2 writes for solution + csproj");

				synchronizer.Sync();
				Assert.AreEqual(2, m_Builder.WriteTimes, "No more files should be written");
			}

			[Test]
			public void WhenSynchronized_WillCreateCSProjectForAssembly()
			{
				var synchronizer = m_Builder.Build();

				Assert.IsFalse(m_Builder.FileExists(m_Builder.ProjectFilePath(m_Builder.Assembly)));

				synchronizer.Sync();

				Assert.IsTrue(m_Builder.FileExists(m_Builder.ProjectFilePath(m_Builder.Assembly)));
			}

			[Test]
			public void WhenSynchronized_WithTwoAssemblies_TwoProjectFilesAreGenerated()
			{
				var assemblyA = new Assembly("assemblyA", "path/to/a.dll", new[] { "file.cs" }, new string[0], new Assembly[0], new string[0], AssemblyFlags.None);
				var assemblyB = new Assembly("assemblyB", "path/to/b.dll", new[] { "file.cs" }, new string[0], new Assembly[0], new string[0], AssemblyFlags.None);
				var synchronizer = m_Builder.WithAssemblies(new[] { assemblyA, assemblyB }).Build();

				synchronizer.Sync();

				Assert.IsTrue(m_Builder.FileExists(m_Builder.ProjectFilePath(assemblyA)));
				Assert.IsTrue(m_Builder.FileExists(m_Builder.ProjectFilePath(assemblyB)));
			}

			[Test]
			public void NotInInternalizedPackage_WillResync()
			{
				var synchronizer = m_Builder.Build();

				synchronizer.Sync();

				var packageAsset = "packageAsset.cs";
				m_Builder.WithPackageAsset(packageAsset, false);

				Assert.IsTrue(synchronizer.SyncIfNeeded(new[] { packageAsset }, new string[0]));
			}
		}

		class SourceFiles : SolutionGenerationTestBase
		{
			[Test]
			public void NoCSFile_CreatesNoProjectFile()
			{
				var synchronizer = m_Builder.WithAssemblyData(files: new string[0]).Build();

				synchronizer.Sync();

				Assert.False(
					m_Builder.FileExists(Path.Combine(SynchronizerBuilder.projectDirectory, $"{m_Builder.Assembly.name}.csproj")),
					"Should not create csproj file for assembly with no cs file");
			}

			[Test]
			public void NotContributedAnAssembly_WillNotGetAdded()
			{
				var synchronizer = m_Builder.WithAssetFiles(new[] { "Assembly.hlsl" }).Build();

				synchronizer.Sync();

				var csprojContent = m_Builder.ReadProjectFile(m_Builder.Assembly);
				StringAssert.DoesNotContain("Assembly.hlsl", csprojContent);
			}

			[Test]
			public void MultipleSourceFiles_WillAllBeAdded()
			{
				var files = new[] { "fileA.cs", "fileB.cs", "fileC.cs" };
				var synchronizer = m_Builder
					.WithAssemblyData(files: files)
					.Build();

				synchronizer.Sync();

				var csprojectContent = m_Builder.ReadProjectFile(m_Builder.Assembly);
				var xmlDocument = XMLUtilities.FromText(csprojectContent);
				XMLUtilities.AssertCompileItemsMatchExactly(xmlDocument, files);
			}

			[Test]
			public void FullPathAsset_WillBeConvertedToRelativeFromProjectDirectory()
			{
				var assetPath = "Assets/Asset.cs";
				var synchronizer = m_Builder
					.WithAssemblyData(files: new[] { Path.Combine(SynchronizerBuilder.projectDirectory, assetPath) })
					.Build();

				synchronizer.Sync();

				var csprojectContent = m_Builder.ReadProjectFile(m_Builder.Assembly);
				var xmlDocument = XMLUtilities.FromText(csprojectContent);
				XMLUtilities.AssertCompileItemsMatchExactly(xmlDocument, new[] { assetPath.ReplaceDirectorySeparators() });
			}

			[Test]
			public void InRelativePackages_GetsPathResolvedCorrectly()
			{
				var assetPath = "/FullPath/ExamplePackage/Packages/Asset.cs";
				var assembly = new Assembly("ExamplePackage", "/FullPath/Example/ExamplePackage/ExamplePackage.dll", new[] { assetPath }, new string[0], new Assembly[0], new string[0], AssemblyFlags.None);
				var synchronizer = m_Builder
					.WithAssemblies(new[] { assembly })
					.WithPackageInfo(assetPath)
					.Build();

				synchronizer.Sync();

				StringAssert.Contains(assetPath.ReplaceDirectorySeparators(), m_Builder.ReadProjectFile(assembly));
			}

			[Test]
			public void InternalizedPackage_WillBeAddedToCompileInclude()
			{
				var synchronizer = m_Builder.WithPackageAsset(m_Builder.Assembly.sourceFiles[0], true).Build();

				synchronizer.Sync();

				StringAssert.Contains(m_Builder.Assembly.sourceFiles[0], m_Builder.ReadProjectFile(m_Builder.Assembly));
			}

			[Test]
			public void NoneInternalizedPackage_WillBeAddedToCompileInclude()
			{
				var synchronizer = m_Builder
					.WithPackageAsset(m_Builder.Assembly.sourceFiles[0], false)
					.Build();

				synchronizer.Sync();

				StringAssert.Contains(m_Builder.Assembly.sourceFiles[0], m_Builder.ReadProjectFile(m_Builder.Assembly));
			}

			[Test]
			public void CSharpFiles_WillBeIncluded()
			{
				var synchronizer = m_Builder.Build();

				synchronizer.Sync();

				var assembly = m_Builder.Assembly;
				StringAssert.Contains(assembly.sourceFiles[0].ReplaceDirectorySeparators(), m_Builder.ReadProjectFile(assembly));
			}

			[Test]
			public void NonCSharpFiles_AddedToNonCompileItems()
			{
				var nonCompileItems = new[]
				{
					"UnityShader.uss",
					"ComputerGraphic.cginc",
					"Test.shader",
				};
				var synchronizer = m_Builder
					.WithAssetFiles(nonCompileItems)
					.AssignFilesToAssembly(nonCompileItems, m_Builder.Assembly)
					.Build();

				synchronizer.Sync();

				var csprojectContent = m_Builder.ReadProjectFile(m_Builder.Assembly);
				var xmlDocument = XMLUtilities.FromText(csprojectContent);
				XMLUtilities.AssertCompileItemsMatchExactly(xmlDocument, m_Builder.Assembly.sourceFiles);
				XMLUtilities.AssertNonCompileItemsMatchExactly(xmlDocument, nonCompileItems);
			}

			[Test]
			public void UnsupportedExtensions_WillNotBeAdded()
			{
				var unsupported = new[] { "file.unsupported" };
				var synchronizer = m_Builder
					.WithAssetFiles(unsupported)
					.AssignFilesToAssembly(unsupported, m_Builder.Assembly)
					.Build();

				synchronizer.Sync();

				var csprojectContent = m_Builder.ReadProjectFile(m_Builder.Assembly);
				var xmlDocument = XMLUtilities.FromText(csprojectContent);
				XMLUtilities.AssertCompileItemsMatchExactly(xmlDocument, m_Builder.Assembly.sourceFiles);
				XMLUtilities.AssertNonCompileItemsMatchExactly(xmlDocument, new string[0]);
			}

			[Test]
			public void UnsupportedExtension_IsOverWrittenBy_UserSupportedExtensions()
			{
				var unsupported = new[] { "file.unsupported" };
				var synchronizer = m_Builder
					.WithAssetFiles(unsupported)
					.AssignFilesToAssembly(unsupported, m_Builder.Assembly)
					.WithUserSupportedExtensions(new[] { "unsupported" })
					.Build();

				synchronizer.Sync();

				var xmlDocument = XMLUtilities.FromText(m_Builder.ReadProjectFile(m_Builder.Assembly));
				XMLUtilities.AssertNonCompileItemsMatchExactly(xmlDocument, unsupported);
			}

#if UNITY_2020_2_OR_NEWER
			[Test]
			public void FullSync_And_SelectiveSync_Provide_Same_Output()
			{
				var files = new[] { "fileA.cs", "fileB.cs", "fileC.cs" };
				const string roslynAnalyzerDllPath = "Assets/RoslynAnalyzer.dll";

				var synchronizer = m_Builder
					.WithAssemblyData(files: files)
					.WithRoslynAnalyzers(new[] { roslynAnalyzerDllPath })
					.Build();

				synchronizer.SyncIfNeeded(files, files);
				var selectiveSyncContent = m_Builder.ReadProjectFile(m_Builder.Assembly);

				synchronizer.Sync();
				var fullSyncContent = m_Builder.ReadProjectFile(m_Builder.Assembly);

				Assert.AreEqual(selectiveSyncContent, fullSyncContent);
			}
#endif

			[TestCase(@"path\com.unity.cs")]
			[TestCase(@"..\path\file.cs")]
			public void IsValidFileName(string filePath)
			{
				var synchronizer = m_Builder
					.WithAssemblyData(files: new[] { filePath })
					.Build();

				synchronizer.Sync();

				var csprojContent = m_Builder.ReadProjectFile(m_Builder.Assembly);
				StringAssert.Contains(filePath.ReplaceDirectorySeparators(), csprojContent);
			}

			[Test]
			public void AddedAfterSync_WillBeSynced()
			{
				var synchronizer = m_Builder.Build();
				synchronizer.Sync();
				const string newFile = "Newfile.cs";
				var newFileArray = new[] { newFile };
				m_Builder.WithAssemblyData(files: m_Builder.Assembly.sourceFiles.Concat(newFileArray).ToArray());

				Assert.True(synchronizer.SyncIfNeeded(newFileArray, new string[0]), "Should sync when file in assembly changes");

				var csprojContentAfter = m_Builder.ReadProjectFile(m_Builder.Assembly);
				StringAssert.Contains(newFile, csprojContentAfter);
			}

			[Test]
			public void Moved_WillBeResynced()
			{
				var synchronizer = m_Builder.Build();
				synchronizer.Sync();
				var filesBefore = m_Builder.Assembly.sourceFiles;
				const string newFile = "Newfile.cs";
				var newFileArray = new[] { newFile };
				m_Builder.WithAssemblyData(files: newFileArray);

				Assert.True(synchronizer.SyncIfNeeded(newFileArray, new string[0]), "Should sync when file in assembly changes");

				var csprojContentAfter = m_Builder.ReadProjectFile(m_Builder.Assembly);
				StringAssert.Contains(newFile, csprojContentAfter);
				foreach (var file in filesBefore)
				{
					StringAssert.DoesNotContain(file, csprojContentAfter);
				}
			}

			[Test]
			public void Deleted_WillBeRemoved()
			{
				var filesBefore = new[]
				{
					"WillBeDeletedScript.cs",
					"Script.cs",
				};
				var synchronizer = m_Builder.WithAssemblyData(files: filesBefore).Build();

				synchronizer.Sync();

				var csprojContentBefore = m_Builder.ReadProjectFile(m_Builder.Assembly);
				StringAssert.Contains(filesBefore[0], csprojContentBefore);
				StringAssert.Contains(filesBefore[1], csprojContentBefore);

				var filesAfter = filesBefore.Skip(1).ToArray();
				m_Builder.WithAssemblyData(files: filesAfter);

				Assert.True(synchronizer.SyncIfNeeded(filesAfter, new string[0]), "Should sync when file in assembly changes");

				var csprojContentAfter = m_Builder.ReadProjectFile(m_Builder.Assembly);
				StringAssert.Contains(filesAfter[0], csprojContentAfter);
				StringAssert.DoesNotContain(filesBefore[0], csprojContentAfter);
			}

			[Test, TestCaseSource(nameof(s_BuiltinSupportedExtensionsForSourceFiles))]
			public void BuiltinSupportedExtensions_InsideAssemblySourceFiles_WillBeAddedToCompileItems(string fileExtension)
			{
				var compileItem = new[] { "file.cs", $"anotherFile.{fileExtension}" };
				var synchronizer = m_Builder.WithAssemblyData(files: compileItem).Build();

				synchronizer.Sync();

				var csprojContent = m_Builder.ReadProjectFile(m_Builder.Assembly);
				XmlDocument scriptProject = XMLUtilities.FromText(csprojContent);
				XMLUtilities.AssertCompileItemsMatchExactly(scriptProject, compileItem);
			}

			static string[] s_BuiltinSupportedExtensionsForSourceFiles =
			{
				"asmdef", "cs", "uxml", "uss", "shader", "compute", "cginc", "hlsl", "glslinc", "template", "raytrace"
			};

			[Test, TestCaseSource(nameof(s_BuiltinSupportedExtensionsForAssets))]
			public void BuiltinSupportedExtensions_InsideAssetFolder_WillBeAddedToNonCompileItems(string fileExtension)
			{
				var nonCompileItem = new[] { $"anotherFile.{fileExtension}" };
				var synchronizer = m_Builder
					.WithAssetFiles(files: nonCompileItem)
					.AssignFilesToAssembly(nonCompileItem, m_Builder.Assembly)
					.Build();

				synchronizer.Sync();

				var csprojContent = m_Builder.ReadProjectFile(m_Builder.Assembly);
				XmlDocument scriptProject = XMLUtilities.FromText(csprojContent);
				XMLUtilities.AssertCompileItemsMatchExactly(scriptProject, m_Builder.Assembly.sourceFiles);
				XMLUtilities.AssertNonCompileItemsMatchExactly(scriptProject, nonCompileItem);
			}

			static string[] s_BuiltinSupportedExtensionsForAssets =
			{
				"uxml", "uss", "shader", "compute", "cginc", "hlsl", "glslinc", "template", "raytrace"
			};
		}

#if UNITY_2020_2_OR_NEWER
        class RootNamespace : SolutionGenerationTestBase
        {
            [Test]
            public void RootNamespaceFromAssembly_AddBlockToCsproj()
            {
                var @namespace = "TestNamespace";

                var synchronizer = m_Builder
                    .WithAssemblyData(rootNamespace: @namespace)
                    .Build();

                synchronizer.Sync();

                var csprojFileContents = m_Builder.ReadProjectFile(m_Builder.Assembly);
                StringAssert.Contains($"<RootNamespace>{@namespace}</RootNamespace>", csprojFileContents);
            }
        }
#endif

		class LanguageVersion : SolutionGenerationTestBase
		{
			[Test]
			public void OldVS2017_Supports70()
			{
				try
				{
					var synchronizer = m_Builder
						.WithLatestLanguageVersionSupported(new Version(7, 0))
						.Build();

					synchronizer.Sync();

					var csprojFileContents = m_Builder.ReadProjectFile(m_Builder.Assembly);
					StringAssert.Contains($"<LangVersion>7.0</LangVersion>", csprojFileContents);
				}
				finally
				{
					m_Builder.CleanUp();
				}
			}

#if UNITY_2020_2
			[Test]
			public void Unity2020_2_Supports80()
			{
				try
				{
					var synchronizer = m_Builder
						.WithLatestLanguageVersionSupported(new Version(99, 0))
						.Build();

					synchronizer.Sync();

					var csprojFileContents = m_Builder.ReadProjectFile(m_Builder.Assembly);
					StringAssert.Contains($"<LangVersion>8.0</LangVersion>", csprojFileContents);
				}
				finally
				{
					m_Builder.CleanUp();
				}
			}
#elif UNITY_2020_1
			[Test]
			public void Unity2020_1_Supports73()
			{
				try
				{
					var synchronizer = m_Builder
						.WithLatestLanguageVersionSupported(new Version(99, 0))
						.Build();

					synchronizer.Sync();

					var csprojFileContents = m_Builder.ReadProjectFile(m_Builder.Assembly);
					StringAssert.Contains($"<LangVersion>7.3</LangVersion>", csprojFileContents);
				}
				finally
				{
					m_Builder.CleanUp();
				}
			}
#endif
		}

		class CompilerOptions : SolutionGenerationTestBase
		{
			[Test]
			public void AllowUnsafeFromResponseFile_AddBlockToCsproj()
			{
				const string responseFile = "csc.rsp";
				var synchronizer = m_Builder
					.WithResponseFileData(m_Builder.Assembly, responseFile, _unsafe: true)
					.Build();

				synchronizer.Sync();

				var csprojFileContents = m_Builder.ReadProjectFile(m_Builder.Assembly);
				StringAssert.Contains("<AllowUnsafeBlocks>True</AllowUnsafeBlocks>", csprojFileContents);
			}

			[Test]
			public void AnalyzerInResponseFile_AddBlockToCsproj()
			{
				try
				{
					const string responseFile = "csc.rsp";
					var synchronizer = m_Builder
						.WithResponseFileData(m_Builder.Assembly, responseFile, otherArguments: new[] { "  /analyzer:foo.dll", "/a:bar.dll  ", "  -analyzer:/foobar.dll  ", "-a  :/barfoo.dll", "/a:  foo.dll" })
						.WithAnalyzerSupport()
						.Build();

					synchronizer.Sync();

					XMLUtilities.AssertAnalyzerDllsAreIncluded(
						XMLUtilities.FromText(m_Builder.ReadProjectFile(m_Builder.Assembly)),
						new[]
						{
							"foo.dll".MakeAbsolutePath().NormalizePathSeparators(),
							"bar.dll".MakeAbsolutePath().NormalizePathSeparators(),
							"/foobar.dll".NormalizePathSeparators(),
							"/barfoo.dll".NormalizePathSeparators()
						});
				}
				finally
				{
					m_Builder.CleanUp();
				}
			}

			[Test]
			public void AllowUnsafeFromAssemblySettings_AddBlockToCsproj()
			{
				var synchronizer = m_Builder
					.WithAssemblyData(unsafeSettings: true)
					.Build();

				synchronizer.Sync();

				var csprojFileContents = m_Builder.ReadProjectFile(m_Builder.Assembly);
				StringAssert.Contains("<AllowUnsafeBlocks>True</AllowUnsafeBlocks>", csprojFileContents);
			}
		}

		class References : SolutionGenerationTestBase
		{
#if UNITY_2020_2_OR_NEWER
	        [Test]
	        public void RoslynAnalyzerDlls_WillBeIncluded()
	        {
		        try
		        {
			        const string roslynAnalyzerDllPath = "Assets/RoslynAnalyzer.dll";

			        m_Builder.WithRoslynAnalyzers(new[] { roslynAnalyzerDllPath })
				        .Build()
				        .Sync();

			        XMLUtilities.AssertAnalyzerDllsAreIncluded(
				        XMLUtilities.FromText(m_Builder.ReadProjectFile(m_Builder.Assembly)),
				        new[] { roslynAnalyzerDllPath.MakeAbsolutePath().NormalizePathSeparators() });
		        }
		        finally
		        {
			        m_Builder.CleanUp();
		        }
	        }

			[Test]
			public void RoslynAndRSPAnalyzerDlls_WillBeIncluded()
			{
				try
				{
					const string responseFile = "csc.rsp";
					m_Builder
						.WithRoslynAnalyzers(new[] { "foounity.dll", "/barunity.dll" })
						.WithResponseFileData(m_Builder.Assembly, responseFile, otherArguments: new[] { "/analyzer:foorsp.dll", "/a:/barrsp.dll" })
						.Build()
						.Sync();

					XMLUtilities.AssertAnalyzerDllsAreIncluded(
						XMLUtilities.FromText(m_Builder.ReadProjectFile(m_Builder.Assembly)),
						new[]
						{
							"foounity.dll".MakeAbsolutePath().NormalizePathSeparators(),
							"/barunity.dll".NormalizePathSeparators(),
							"foorsp.dll".MakeAbsolutePath().NormalizePathSeparators(),
							"/barrsp.dll".NormalizePathSeparators()
						});
				}
				finally
				{
					m_Builder.CleanUp();
				}
			}

			[Test]
			public void RoslynAndRSPAnalyzerDlls_NoDuplicate()
			{
				try
				{
					const string responseFile = "csc.rsp";
					var analyzerAssembly = "foo.dll";
					
					m_Builder
						.WithRoslynAnalyzers(new[] { analyzerAssembly, analyzerAssembly })
						.WithResponseFileData(m_Builder.Assembly, responseFile, otherArguments: new[] { $"/a:{analyzerAssembly}", $"-a:{analyzerAssembly}" })
						.Build()
						.Sync();

					var content = m_Builder.ReadProjectFile(m_Builder.Assembly);
					XMLUtilities.AssertAnalyzerDllsAreIncluded(
						XMLUtilities.FromText(content),
						new[] { analyzerAssembly.MakeAbsolutePath().NormalizePathSeparators() });

					Assert.IsTrue(content.IndexOf(analyzerAssembly, StringComparison.Ordinal) == content.LastIndexOf(analyzerAssembly, StringComparison.Ordinal));
				}
				finally
				{
					m_Builder.CleanUp();
				}
			}

	        [Test]
	        public void RoslynAnalyzerRulesetPaths_WillBeIncluded()
	        {
		        try
		        {
			        const string roslynAnalyzerRuleSetPath = "Assets/SampleRuleSet.ruleset";
			        m_Builder.WithRulesetPath(roslynAnalyzerRuleSetPath)
				        .Build()
				        .Sync();

			        XMLUtilities.AssertAnalyzerRuleSetsAreIncluded(
				        XMLUtilities.FromText(m_Builder.ReadProjectFile(m_Builder.Assembly)),
				        roslynAnalyzerRuleSetPath.MakeAbsolutePath().NormalizePathSeparators());
		        }
		        finally
		        {
			        m_Builder.CleanUp();
		        }
	        }
#endif

#if UNITY_2021_3_OR_NEWER && !UNITY_2022_1 // we have support in 2021.3, 2022.2 but without a backport in 2022.1
			[Test]
			public void AnalyzerConfigPath_WillBeIncluded()
			{
				try
				{
					const string analyzerConfigPath = "Assets/Default.globalconfig";
					m_Builder.WithAnalyzerConfigPath(analyzerConfigPath)
						.Build()
						.Sync();

					XMLUtilities.AssertAnalyzerConfigPathIsIncluded(
						XMLUtilities.FromText(m_Builder.ReadProjectFile(m_Builder.Assembly)),
						analyzerConfigPath.MakeAbsolutePath().NormalizePathSeparators());
				}
				finally
				{
					m_Builder.CleanUp();
				}
			}

			[Test]
			public void AdditionalFilePaths_WillBeIncluded()
			{
				try
				{
					const string responseFile = "csc.rsp";
					m_Builder
						.WithAdditionalFilePaths(new[] { "FileA.Analyzer.additionalfile", "/FileB.Analyzer.additionalfile" })
						.WithResponseFileData(m_Builder.Assembly, responseFile, otherArguments: new[] { "/additionalfile:FileArsp.Analyzer.additionalfile", "/additionalfile:/FileBrsp.Analyzer.additionalfile" })
						.Build()
						.Sync();

					XMLUtilities.AssertAdditionalFilePathsAreIncluded(
						XMLUtilities.FromText(m_Builder.ReadProjectFile(m_Builder.Assembly)),
						new[]
						{
							"FileA.Analyzer.additionalfile".MakeAbsolutePath().NormalizePathSeparators(),
							"/FileB.Analyzer.additionalfile".NormalizePathSeparators(),
							"FileArsp.Analyzer.additionalfile".MakeAbsolutePath().NormalizePathSeparators(),
							"/FileBrsp.Analyzer.additionalfile".NormalizePathSeparators(),
						});
				}
				finally
				{
					m_Builder.CleanUp();
				}
			}

			[Test]
			public void AdditionalFilePaths_NoDuplicate()
			{
				try
				{
					m_Builder
						.WithAdditionalFilePaths(new[] { "FileA.Analyzer.additionalfile", "FileA.Analyzer.additionalfile" })
						.Build()
						.Sync();

					XMLUtilities.AssertAdditionalFilePathsAreIncluded(
						XMLUtilities.FromText(m_Builder.ReadProjectFile(m_Builder.Assembly)),
						new[]
						{
							"FileA.Analyzer.additionalfile".MakeAbsolutePath().NormalizePathSeparators(),
						});
				}
				finally
				{
					m_Builder.CleanUp();
				}
			}
#endif

			[Test]
			public void DllInSourceFiles_WillBeAddedAsReference()
			{
				var referenceDll = "reference.dll";
				var synchronizer = m_Builder
					.WithAssemblyData(files: new[] { "file.cs", referenceDll })
					.Build();

				synchronizer.Sync();

				var csprojFileContents = m_Builder.ReadProjectFile(m_Builder.Assembly);
				XmlDocument scriptProject = XMLUtilities.FromText(csprojFileContents);
				XMLUtilities.AssertCompileItemsMatchExactly(scriptProject, new[] { "file.cs" });
				XMLUtilities.AssertNonCompileItemsMatchExactly(scriptProject, new string[0]);
				Assert.IsTrue(csprojFileContents.MatchesRegex($"<Reference Include=\"reference\">\\W*<HintPath>{referenceDll}\\W*</HintPath>\\W*</Reference>"));
			}

			[Test]
			public void Containing_PathWithSpaces_IsParsedCorrectly()
			{
				const string responseFile = "csc.rsp";
				var fullPathReferences = new[] { "Folder/Path With Space/Goodbye.dll" };
				var synchronizer = m_Builder
					.WithResponseFileData(m_Builder.Assembly, responseFile, fullPathReferences: fullPathReferences)
					.Build();

				synchronizer.Sync();

				var csprojFileContents = m_Builder.ReadProjectFile(m_Builder.Assembly);
				Assert.IsTrue(csprojFileContents.MatchesRegex($"<Reference Include=\"Goodbye\">\\W*<HintPath>{Regex.Escape(fullPathReferences[0].ReplaceDirectorySeparators())}\\W*</HintPath>\\W*</Reference>"));
			}

			[Test]
			public void Containing_PathWithDotCS_IsParsedCorrectly()
			{
				var assembly = new Assembly("name", "/path/with.cs/assembly.dll", new[] { "file.cs" }, new string[0], new Assembly[0], new string[0], AssemblyFlags.None);
				var synchronizer = m_Builder
					.WithAssemblyData(assemblyReferences: new[] { assembly })
					.Build();

				synchronizer.Sync();

				var csprojFileContents = m_Builder.ReadProjectFile(m_Builder.Assembly);
				Assert.IsTrue(csprojFileContents.MatchesRegex($@"<ProjectReference Include=""{assembly.name}\.csproj\"">"));
			}

			[Test]
			public void Multiple_AreAdded()
			{
				const string responseFile = "csc.rsp";
				var synchronizer = m_Builder
					.WithResponseFileData(m_Builder.Assembly, responseFile, fullPathReferences: new[] { "MyPlugin.dll", "Hello.dll" })
					.Build();

				synchronizer.Sync();

				var csprojFileContents = m_Builder.ReadProjectFile(m_Builder.Assembly);

				Assert.IsTrue(csprojFileContents.MatchesRegex("<Reference Include=\"Hello\">\\W*<HintPath>Hello.dll</HintPath>\\W*</Reference>"));
				Assert.IsTrue(csprojFileContents.MatchesRegex("<Reference Include=\"MyPlugin\">\\W*<HintPath>MyPlugin.dll</HintPath>\\W*</Reference>"));
			}

			[Test]
			public void AssemblyReference_IsAdded()
			{
				string[] files = { "test.cs" };
				var assemblyReferences = new[]
				{
					new Assembly("MyPlugin", "/some/path/MyPlugin.dll", files, new string[0], new Assembly[0], new string[0], AssemblyFlags.None),
					new Assembly("Hello", "/some/path/Hello.dll", files, new string[0], new Assembly[0], new string[0], AssemblyFlags.None),
				};
				var synchronizer = m_Builder.WithAssemblyData(assemblyReferences: assemblyReferences).Build();

				synchronizer.Sync();

				var csprojFileContents = m_Builder.ReadProjectFile(m_Builder.Assembly);
				Assert.That(csprojFileContents, Does.Match($"<ProjectReference Include=\"{assemblyReferences[0].name}\\.csproj\">\\s+.+\\s+<Name>{assemblyReferences[0].name}</Name>\\W*</ProjectReference>"));
				Assert.That(csprojFileContents, Does.Match($"<ProjectReference Include=\"{assemblyReferences[1].name}\\.csproj\">\\s+.+\\s+<Name>{assemblyReferences[1].name}</Name>\\W*</ProjectReference>"));
			}

			[Test]
			public void AssemblyReferenceFromInternalizedPackage_IsAddedAsReference()
			{
				string[] files = { "test.cs" };
				var assemblyReferences = new[]
				{
					new Assembly("MyPlugin", "/some/path/MyPlugin.dll", files, new string[0], new Assembly[0], new string[0], AssemblyFlags.None),
					new Assembly("Hello", "/some/path/Hello.dll", files, new string[0], new Assembly[0], new string[0], AssemblyFlags.None),
				};
				var synchronizer = m_Builder.WithPackageAsset(files[0], true).WithAssemblyData(assemblyReferences: assemblyReferences).Build();

				synchronizer.Sync();

				var csprojFileContents = m_Builder.ReadProjectFile(m_Builder.Assembly);
				Assert.That(csprojFileContents, Does.Not.Match($@"<ProjectReference Include=""{assemblyReferences[0].name}\.csproj"">[\S\s]*?</ProjectReference>"));
				Assert.That(csprojFileContents, Does.Not.Match($@"<ProjectReference Include=""{assemblyReferences[1].name}\.csproj"">[\S\s]*?</ProjectReference>"));
				Assert.That(csprojFileContents, Does.Match($"<Reference Include=\"{assemblyReferences[0].name}\">\\W*<HintPath>{Regex.Escape(assemblyReferences[0].outputPath.ReplaceDirectorySeparators())}</HintPath>\\W*</Reference>"));
				Assert.That(csprojFileContents, Does.Match($"<Reference Include=\"{assemblyReferences[1].name}\">\\W*<HintPath>{Regex.Escape(assemblyReferences[1].outputPath.ReplaceDirectorySeparators())}</HintPath>\\W*</Reference>"));
			}

			[Test]
			public void CompiledAssemblyReference_IsAdded()
			{
				var compiledAssemblyReferences = new[]
				{
					"/some/other/path/Hello.dll",
					"/some/path/MyPlugin.dll",
				}.Select(path => path.ReplaceDirectorySeparators()).ToArray();
				var synchronizer = m_Builder.WithAssemblyData(compiledAssemblyReferences: compiledAssemblyReferences).Build();

				synchronizer.Sync();

				var csprojFileContents = m_Builder.ReadProjectFile(m_Builder.Assembly);
				Assert.IsTrue(csprojFileContents.MatchesRegex($"<Reference Include=\"Hello\">\\W*<HintPath>{Regex.Escape(compiledAssemblyReferences[0])}</HintPath>\\W*</Reference>"));
				Assert.IsTrue(csprojFileContents.MatchesRegex($"<Reference Include=\"MyPlugin\">\\W*<HintPath>{Regex.Escape(compiledAssemblyReferences[1])}</HintPath>\\W*</Reference>"));
			}

			[Test]
			public void ProjectReference_FromLibraryReferences_IsAdded()
			{
				var projectAssembly = new Assembly("ProjectAssembly", "/path/to/project.dll", new[] { "test.cs" }, new string[0], new Assembly[0], new string[0], AssemblyFlags.None);
				var synchronizer = m_Builder.WithAssemblyData(assemblyReferences: new[] { projectAssembly }).Build();

				synchronizer.Sync();

				var csprojFileContents = m_Builder.ReadProjectFile(m_Builder.Assembly);
				Assert.IsFalse(csprojFileContents.MatchesRegex($"<Reference Include=\"{projectAssembly.name}\">\\W*<HintPath>{Regex.Escape(projectAssembly.outputPath.ReplaceDirectorySeparators())}</HintPath>\\W*</Reference>"));
			}

			[Test]
			public void NotInAssembly_WontBeAdded()
			{
				var fileOutsideAssembly = "some.dll";
				var fileArray = new[] { fileOutsideAssembly };
				var synchronizer = m_Builder.WithAssetFiles(fileArray).Build();

				synchronizer.Sync();

				var csprojFileContents = m_Builder.ReadProjectFile(m_Builder.Assembly);
				StringAssert.DoesNotContain("some.dll", csprojFileContents);
			}
		}

		class Defines : SolutionGenerationTestBase
		{
			[Test]
			public void ResponseFiles_CanAddDefines()
			{
				const string responseFile = "csc.rsp";
				var synchronizer = m_Builder
					.WithResponseFileData(m_Builder.Assembly, responseFile, defines: new[] { "DEF1", "DEF2" })
					.Build();

				synchronizer.Sync();

				var csprojFileContents = m_Builder.ReadProjectFile(m_Builder.Assembly);
				StringAssert.Contains("<DefineConstants>DEF1;DEF2</DefineConstants>", csprojFileContents);
			}

			[Test]
			public void Assembly_CanAddDefines()
			{
				var synchronizer = m_Builder.WithAssemblyData(defines: new[] { "DEF1", "DEF2" }).Build();

				synchronizer.Sync();

				var csprojFileContents = m_Builder.ReadProjectFile(m_Builder.Assembly);
				StringAssert.Contains("<DefineConstants>DEF1;DEF2</DefineConstants>", csprojFileContents);
			}

			[Test]
			public void ResponseFileDefines_OverrideRootResponseFile()
			{
				string[] files = { "test.cs" };
				var assemblyA = new Assembly("A", "some/root/file.dll", files, new string[0], new Assembly[0], new string[0], AssemblyFlags.None);
				var assemblyB = new Assembly("B", "some/root/child/anotherfile.dll", files, new string[0], new Assembly[0], new string[0], AssemblyFlags.None);
				var synchronizer = m_Builder
					.WithAssemblies(new[] { assemblyA, assemblyB })
					.WithResponseFileData(assemblyA, "A.rsp", defines: new[] { "RootedDefine" })
					.WithResponseFileData(assemblyB, "B.rsp", defines: new[] { "CHILD_DEFINE" })
					.Build();

				synchronizer.Sync();

				var aCsprojContent = m_Builder.ReadProjectFile(assemblyA);
				var bCsprojContent = m_Builder.ReadProjectFile(assemblyB);

				StringAssert.Contains("<DefineConstants>CHILD_DEFINE</DefineConstants>", bCsprojContent);
				StringAssert.DoesNotContain("<DefineConstants>RootedDefine</DefineConstants>", bCsprojContent);
				StringAssert.DoesNotContain("<DefineConstants>CHILD_DEFINE</DefineConstants>", aCsprojContent);
				StringAssert.Contains("<DefineConstants>RootedDefine</DefineConstants>", aCsprojContent);
			}
		}
	}
}
