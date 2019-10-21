using System;

namespace Microsoft.VisualStudio.Editor.Messaging
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
