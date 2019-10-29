using System;
using System.IO;

namespace Microsoft.Unity.VisualStudio.Editor
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
