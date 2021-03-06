using System;
using System.Threading.Tasks;
using CampfireNet.Utilities.Channels;

namespace CampfireNet.IO.Transport
{
	public interface IBluetoothNeighbor
	{
		Guid AdapterId { get; }
		bool IsConnected { get; }
		ReadableChannel<byte[]> InboundChannel { get; }
		Task<bool> TryHandshakeAsync(double minTimeoutSeconds);
		Task SendAsync(byte[] data);
	   void Disconnect();
	}
}
