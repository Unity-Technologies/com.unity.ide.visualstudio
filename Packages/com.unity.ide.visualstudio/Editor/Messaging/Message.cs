using System.Globalization;
using System.Net;

namespace Microsoft.Unity.VisualStudio.Editor.Messaging
{
	internal class Message
	{
		public MessageType Type { get; set; }

		public string Value { get; set; }

		public IPEndPoint Origin { get; set; }

		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "<Message type:{0} value:{1}>", Type, Value);
		}
	}
}
