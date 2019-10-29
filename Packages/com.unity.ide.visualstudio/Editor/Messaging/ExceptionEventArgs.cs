using System;

namespace Microsoft.Unity.VisualStudio.Editor.Messaging
{
	internal class ExceptionEventArgs
	{
		public Exception Exception { get; }

		public ExceptionEventArgs(Exception exception)
		{
			Exception = exception;
		}
	}
}
