using System;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Microsoft.VisualStudio.Editor
{
	internal static class Symbols
	{
		public static bool IsPortableSymbolFile(string pdbFile)
		{
			try
			{
				using (var stream = File.OpenRead(pdbFile))
				{
					return stream.ReadByte() == 'B'
						   && stream.ReadByte() == 'S'
						   && stream.ReadByte() == 'J'
						   && stream.ReadByte() == 'B';
				}
			}
			catch (Exception)
			{
				return false;
			}
		}
	}
}
