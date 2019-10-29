namespace Microsoft.Unity.VisualStudio.Editor.Messaging
{
	internal enum MessageType
	{
		None = 0,

		Ping,
		Pong,

		Play,
		Stop,
		Pause,
		Unpause,

		Build,
		Refresh,

		Info,
		Error,
		Warning,

		Open,
		Opened,

		Version,
		UpdatePackage,

		ProjectPath,
	}
}
