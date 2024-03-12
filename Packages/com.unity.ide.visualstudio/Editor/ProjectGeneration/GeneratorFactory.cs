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
}
