using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Microsoft.Unity.VisualStudio.Editor.Tests
{
	public static class XMLUtilities
	{
		public static void AssertCompileItemsMatchExactly(XmlDocument projectXml, IEnumerable<string> expectedCompileItems)
		{
			var compileItems = projectXml.SelectAttributeValues("/msb:Project/msb:ItemGroup/msb:Compile/@Include").ToArray();
			CollectionAssert.AreEquivalent(expectedCompileItems, compileItems);
		}

		public static void AssertNonCompileItemsMatchExactly(XmlDocument projectXml, IEnumerable<string> expectedNoncompileItems)
		{
			var nonCompileItems = projectXml.SelectAttributeValues("/msb:Project/msb:ItemGroup/msb:None/@Include").ToArray();
			CollectionAssert.AreEquivalent(expectedNoncompileItems, nonCompileItems);
		}

		public static void AssertAnalyzerDllsAreIncluded(XmlDocument projectXml, IEnumerable<string> expectedAnalyzerDllPaths)
		{
			foreach (string path in expectedAnalyzerDllPaths.Select(FileUtility.NormalizePathSeparators))
			{
				CollectionAssert.Contains(
					projectXml.SelectAttributeValues("/msb:Project/msb:ItemGroup/msb:Analyzer/@Include"), path);
			}
		}

		internal static void AssertAnalyzerRuleSetsAreIncluded(XmlDocument projectXml, string expectedRuleSetPath)
		{
			CollectionAssert.Contains(projectXml.SelectElementValues("/msb:Project/msb:PropertyGroup/msb:CodeAnalysisRuleSet"), expectedRuleSetPath);
		}

		static XmlNamespaceManager GetModifiedXmlNamespaceManager(XmlDocument projectXml)
		{
			var xmlNamespaces = new XmlNamespaceManager(projectXml.NameTable);
			xmlNamespaces.AddNamespace("msb", "http://schemas.microsoft.com/developer/msbuild/2003");
			return xmlNamespaces;
		}

		static IEnumerable<string> SelectAttributeValues(this XmlDocument xmlDocument, string xpathQuery)
		{
			var result = xmlDocument.SelectNodes(xpathQuery, GetModifiedXmlNamespaceManager(xmlDocument));
			foreach (XmlAttribute attribute in result)
				yield return attribute.Value;
		}

		static IEnumerable<string> SelectElementValues(this XmlDocument xmlDocument, string xpathQuery)
		{
			var result = xmlDocument.SelectNodes(xpathQuery, GetModifiedXmlNamespaceManager(xmlDocument));
			foreach (XmlElement attribute in result)
			{
				yield return attribute.InnerXml;
			}
		}

		public static XmlDocument FromText(string textContent)
		{
			var xmlDocument = new XmlDocument();
			xmlDocument.LoadXml(textContent);
			return xmlDocument;
		}

		public static string GetInnerText(XmlDocument xmlDocument, string xpathQuery)
		{
			return xmlDocument.SelectSingleNode(xpathQuery, GetModifiedXmlNamespaceManager(xmlDocument)).InnerText;
		}
	}
}
