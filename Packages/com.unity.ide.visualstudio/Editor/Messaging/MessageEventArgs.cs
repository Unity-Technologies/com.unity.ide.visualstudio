namespace Microsoft.VisualStudio.Editor.Messaging
{
	internal class MessageEventArgs
	{
		public Message Message
		{
			get;
		}

		public MessageEventArgs(Message message)
		{
			Message = message;
		}
	}
}
