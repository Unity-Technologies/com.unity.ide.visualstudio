/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Unity Technologies.
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System.Text;
using UnityEditor.Compilation;

namespace Microsoft.Unity.VisualStudio.Editor
{
	internal class SdkStyleProjectGeneration : ProjectGeneration
	{
		internal override void GetProjectHeader(ProjectProperties properties, out StringBuilder headerBuilder)
		{
			headerBuilder = new StringBuilder();

			headerBuilder.Append(@"<Project ToolsVersion=""Current"" Sdk=""Microsoft.NET.Sdk"">").Append(k_WindowsNewline);
			headerBuilder.Append(@"  <PropertyGroup>").Append(k_WindowsNewline);
			headerBuilder.Append(@"    <EnableDefaultItems>false</EnableDefaultItems>").Append(k_WindowsNewline);
			headerBuilder.Append(@"    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>").Append(k_WindowsNewline);
			headerBuilder.Append(@"    <LangVersion>").Append(properties.LangVersion).Append(@"</LangVersion>").Append(k_WindowsNewline);
			headerBuilder.Append(@"    <Configurations>Debug;Release</Configurations>").Append(k_WindowsNewline);
			headerBuilder.Append(@"    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>").Append(k_WindowsNewline);
			headerBuilder.Append(@"    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>").Append(k_WindowsNewline);
			headerBuilder.Append(@"    <RootNamespace>").Append(properties.RootNamespace).Append(@"</RootNamespace>").Append(k_WindowsNewline);
			headerBuilder.Append(@"    <OutputType>Library</OutputType>").Append(k_WindowsNewline);
			headerBuilder.Append(@"    <AppDesignerFolder>Properties</AppDesignerFolder>").Append(k_WindowsNewline);
			headerBuilder.Append(@"    <AssemblyName>").Append(properties.AssemblyName).Append(@"</AssemblyName>").Append(k_WindowsNewline);
			headerBuilder.Append(@"    <TargetFramework>net471</TargetFramework>").Append(k_WindowsNewline);
			headerBuilder.Append(@"    <BaseDirectory>.</BaseDirectory>").Append(k_WindowsNewline);
			headerBuilder.Append(@"  </PropertyGroup>").Append(k_WindowsNewline);

			GetProjectHeaderConfigurations(properties, headerBuilder);
			GetProjectHeaderVstuFlavoring(properties, headerBuilder, false);
			GetProjectHeaderAnalyzers(properties, headerBuilder);
		}

		protected override void AppendProjectReference(Assembly assembly, Assembly reference, StringBuilder projectBuilder)
		{
			// If the current assembly is a Player project, we want to project-reference the corresponding Player project
			var referenceName = m_AssemblyNameProvider.GetAssemblyName(assembly.outputPath, reference.name);
			projectBuilder.Append(@"    <ProjectReference Include=""").Append(referenceName).Append(GetProjectExtension()).Append(@""" />").Append(k_WindowsNewline);
		}

		protected override void GetProjectFooter(StringBuilder footerBuilder)
		{
			footerBuilder.Append("</Project>").Append(k_WindowsNewline);
		}
	}
}
