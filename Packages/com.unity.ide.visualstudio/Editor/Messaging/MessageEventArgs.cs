namespace Microsoft.VisualStudio.Editor.Messaging
{
	internal class MessageEventArgs
	{
		private readonly Message _message;

		public Message Message
		{
			get { return _message; }
		}

		public MessageEventArgs(Message message)
		{
			_message = message;
		}
	}
}
