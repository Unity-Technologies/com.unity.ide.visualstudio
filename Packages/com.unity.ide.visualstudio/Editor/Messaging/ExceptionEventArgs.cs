using System;

namespace Microsoft.VisualStudio.Editor.Messaging
{
	internal class ExceptionEventArgs
	{
		public Exception Exception { get; private set; }

		public ExceptionEventArgs(Exception exception)
		{
			Exception = exception;
		}
	}
}
