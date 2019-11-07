using System;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Compilation;
using UnityEngine;
using Microsoft.Unity.VisualStudio.Editor;

namespace VisualStudioEditor.Editor_spec
{
    [TestFixture]
    public class SolutionProject
    {
        static string ProjectName
        {
            get
            {
                string[] s = Application.dataPath.Split('/');
                string projectName = s[s.Length - 2];
                return projectName;
            }
        }
        static string s_SolutionFile = $"{ProjectName}.sln";

        [OneTimeSetUp]
        public void OneTimeSetUp() {
            File.Delete(s_SolutionFile);
        }

        [SetUp]
        public void SetUp() {
            var codeEditor = new Microsoft.Unity.VisualStudio.Editor.VisualStudioEditor();
            codeEditor.CreateIfDoesntExist();
        }

        [TearDown]
        public void Dispose() {
            File.Delete(s_SolutionFile);
        }

        [Test]
        public void CreatesSolutionFileIfFileDoesntExist()
        {
            Assert.IsTrue(File.Exists(s_SolutionFile));
        }

        [Test]
        public void HeaderFormatMatches()
        {
            string[] syncedSolutionText = File.ReadAllLines(s_SolutionFile);

            Assert.IsTrue(syncedSolutionText.Length >= 4);
            Assert.AreEqual(@"", syncedSolutionText[0]);
            // Do not include VS version specifics, as it will change depending on the Bridge edition
            Assert.IsTrue(syncedSolutionText[1].Contains(@"Microsoft Visual Studio Solution File, Format Version "));
            Assert.IsTrue(syncedSolutionText[2].Contains(@"# Visual Studio "));
            Assert.IsTrue(syncedSolutionText[3].StartsWith("Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\")"));
        }

        [Test]
        public void IsUTF8Encoded()
        {
            var bom = new byte[4];
            using (var file = new FileStream(s_SolutionFile, FileMode.Open, FileAccess.Read))
            {
                file.Read(bom, 0, 4);
            }

            // Check for UTF8 BOM - using StreamReader & Assert.AreEqual(Encoding.UTF8, CurrentEncoding); fails despite CurrentEncoding appearing to be UTF8 when viewed in the debugger
            Assert.AreEqual(0xEF, bom[0]);
            Assert.AreEqual(0xBB, bom[1]);
            Assert.AreEqual(0xBF, bom[2]);
        }

        [Test]
        public void SyncOnlyForSomeAssetTypesOnReimport()
        {
            IEnumerable<string> precompiledAssetImport = new[] { "reimport.dll" };
            IEnumerable<string> asmdefAssetImport = new[] { "reimport.asmdef" };
            IEnumerable<string> otherAssetImport = new[] { "reimport.someOther" };

            var projectGeneration = new ProjectGeneration(new FileInfo(s_SolutionFile).DirectoryName);
            Assert.IsTrue(File.Exists(s_SolutionFile));

            var precompiledAssemblySyncIfNeeded = projectGeneration.SyncIfNeeded(Enumerable.Empty<string>().ToArray(), precompiledAssetImport);
            var asmdefSyncIfNeeded = projectGeneration.SyncIfNeeded(Enumerable.Empty<string>().ToArray(), asmdefAssetImport);
            var someOtherSyncIfNeeded = projectGeneration.SyncIfNeeded(Enumerable.Empty<string>().ToArray(), otherAssetImport);

            Assert.IsTrue(precompiledAssemblySyncIfNeeded);
            Assert.IsTrue(asmdefSyncIfNeeded);
            Assert.IsFalse(someOtherSyncIfNeeded);
        }

        [Test]
        public void FormattedSolution()
        {
            var mock = new Mock<IAssemblyNameProvider>();
            var files = new[]
            {
                "File.cs",
            };
            var island = new Assembly("Assembly2", "/User/Test/Assembly2.dll", files, new string[0], new Assembly[0], new string[0], AssemblyFlags.None);
            mock.Setup(x => x.GetAllAssemblies(It.IsAny<Func<string, bool>>())).Returns(new[] { island });

            string projectDirectory = Directory.GetParent(Application.dataPath).FullName;
            var synchronizer = new ProjectGeneration(projectDirectory, mock.Object);
            var syncPaths = new Dictionary<string, string>();
            synchronizer.Settings = new TestSettings { ShouldSync = false, SyncPath = syncPaths };

            string GetProjectName()
            {
                string[] s = Application.dataPath.Split('/');
                return s[s.Length - 2];
            }

            string GetProjectGUID(string projectName)
            {
                return SolutionGuidGenerator.GuidForProject(GetProjectName() + projectName);
            }

            string GetSolutionGUID(string projectName)
            {
                return SolutionGuidGenerator.GuidForSolution(projectName, ScriptingLanguage.CSharp);
            }

            synchronizer.Sync();

            // solutionguid, solutionname, projectguid
            // Do not include VS version specifics, as it will change depending on the Bridge edition
            var solutionExpected = string.Join("\r\n", new[]
            {
                @"",
                @"Microsoft Visual Studio Solution File, Format Version ",
                @"# Visual Studio ",
                @"Project(""{{{0}}}"") = ""{2}"", ""{2}.csproj"", ""{{{1}}}""",
                @"EndProject",
                @"Global",
                @"    GlobalSection(SolutionConfigurationPlatforms) = preSolution",
                @"        Debug|Any CPU = Debug|Any CPU",
                @"        Release|Any CPU = Release|Any CPU",
                @"    EndGlobalSection",
                @"    GlobalSection(ProjectConfigurationPlatforms) = postSolution",
                @"        {{{1}}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU",
                @"        {{{1}}}.Debug|Any CPU.Build.0 = Debug|Any CPU",
                @"        {{{1}}}.Release|Any CPU.ActiveCfg = Release|Any CPU",
                @"        {{{1}}}.Release|Any CPU.Build.0 = Release|Any CPU",
                @"    EndGlobalSection",
                @"    GlobalSection(SolutionProperties) = preSolution",
                @"        HideSolutionNode = FALSE",
                @"    EndGlobalSection",
                @"EndGlobal",
                @""
            }).Replace("    ", "\t");

            var solutionTemplate = string.Format(
                solutionExpected,
                GetSolutionGUID(GetProjectName()),
                GetProjectGUID("Assembly2"),
                "Assembly2");

            string[] expected = solutionTemplate.Split( new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            string[] actual = syncPaths[synchronizer.SolutionFile()].Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.IsTrue(actual[i].Contains(expected[i]), "Index {0} [{1}]!=[{2}]", i, actual[i], expected[i]);
            }
        }
    }
}
