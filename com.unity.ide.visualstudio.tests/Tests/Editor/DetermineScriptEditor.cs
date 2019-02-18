using System.Collections.Generic;
using NUnit.Framework;
using Moq;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using Unity.CodeEditor;

namespace VisualStudioEditor.Editor_spec
{
    [TestFixture]
    public class DetermineScriptEditor
    {
        [TestCase("/Applications/Unity/VisualStudio.app")]
        [TestCase("/Applications/Unity/Visual Studio.app")]
        [TestCase("/Applications/Unity/Visual Studio (Preview).app")]
        public void OSXPathDiscovery(string path)
        {
            Discover(path);
        }

        [TestCase(@"C:\Program Files (x86)\Microsoft Visual Studio 10.0\Common7\IDE\devenv.exe")]
        [TestCase(@"C:\Program Files (x86)\Microsoft Visual Studio 12.0\Common7\IDE\devenv.exe")]
        [TestCase(@"C:\Program Files (x86)\Microsoft Visual Studio Express\VCSExpress.exe")]
        [UnityPlatform(RuntimePlatform.WindowsEditor)]
        public void WindowsPathDiscovery(string path)
        {
            Discover(path);
        }

        static void Discover(string path)
        {
            var discovery = new Mock<IDiscovery>();
            var generator = new Mock<IGenerator>();

            discovery.Setup(x => x.PathCallback()).Returns(new[]
            {
                new CodeEditor.Installation
                {
                    Path = path,
                    Name = path
                }
            });

            var editor = new VSEditor(discovery.Object, generator.Object);

            editor.TryGetInstallationForPath(path, out var installation);

            Assert.AreEqual(path, installation.Path);
        }
    }

    [TestFixture]
    public class ParseRawDevEnvPaths
    {
        [TestCase("path/to/2017/devenv.exe", VisualStudioVersion.VisualStudio2017)]
        [TestCase("path/to/2019/devenv.exe", VisualStudioVersion.VisualStudio2019)]
        public void OnlyOnePathExisting(string filePath, VisualStudioVersion version)
        {
            var versions = new Dictionary<VisualStudioVersion, string[]>();
            VSEditor.ParseRawDevEnvPaths(new [] { filePath }, versions);

            Assert.AreEqual(1, versions[version].Length);
            Assert.AreEqual(1, versions.Count);
        }
    }
}
