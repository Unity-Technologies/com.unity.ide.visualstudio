/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Unity Technologies.
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System.Collections.Generic;
using UnityEditor;

namespace Microsoft.Unity.VisualStudio.Editor
{
	internal enum GeneratorStyle
	{
		Automatic = 0,
		SDK = 1,
		Legacy = 2,
	}

	internal static class GeneratorFactory
	{
		private static readonly Dictionary<GeneratorStyle, IGenerator> _generators = new Dictionary<GeneratorStyle, IGenerator>();

		static GeneratorFactory()
		{
			_generators.Add(GeneratorStyle.SDK, new SdkStyleProjectGeneration());
			_generators.Add(GeneratorStyle.Legacy, new LegacyStyleProjectGeneration());
		}

		public static IGenerator GetInstance(GeneratorStyle style)
		{
			if (_generators.TryGetValue(style, out var result))
				return result;

			throw new System.ArgumentException("Unknown generator style");
		}
	}

	internal class DynamicGeneration : IGenerator
	{
		private const string GenerationStyleSettingKey = "unity_project_generation_style";

		public GeneratorStyle PreferredStyle { get; }

		private GeneratorStyle _currentStyle = GeneratorStyle.Automatic;
		public GeneratorStyle CurrentStyle
		{
			get
			{
				_currentStyle = (GeneratorStyle)EditorPrefs.GetInt(GenerationStyleSettingKey, (int)GeneratorStyle.Automatic);
				return _currentStyle;
			}
			set
			{
				_currentStyle = value;
				EditorPrefs.SetInt(GenerationStyleSettingKey, (int)_currentStyle);
				SetGenerator();
			}
		}

		private IGenerator _generator;

		private void SetGenerator()
		{
			_generator = CurrentStyle == GeneratorStyle.Automatic
				? GeneratorFactory.GetInstance(PreferredStyle)
				: GeneratorFactory.GetInstance(CurrentStyle);
		}

		public DynamicGeneration(GeneratorStyle preferredStyle)
		{
			PreferredStyle = preferredStyle;
			SetGenerator();
		}

		public bool SyncIfNeeded(IEnumerable<string> affectedFiles, IEnumerable<string> reimportedFiles)
		{
			return _generator.SyncIfNeeded(affectedFiles, reimportedFiles);
		}

		public void Sync()
		{
			_generator.Sync();
		}

		public bool HasSolutionBeenGenerated()
		{
			return _generator.HasSolutionBeenGenerated();
		}

		public bool IsSupportedFile(string path)
		{
			return _generator.IsSupportedFile(path);
		}

		public string SolutionFile()
		{
			return _generator.SolutionFile();
		}

		public string ProjectDirectory => _generator.ProjectDirectory;
		public IAssemblyNameProvider AssemblyNameProvider => _generator.AssemblyNameProvider;
	}
}
