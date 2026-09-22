using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PlantProcess.Infrastructure.IntegrationTests.Acquisition;

/// <summary>One disposable-test connection only. Forwards the actual PostgreSQL COMMIT
/// packet, then removes both response paths. The caller cannot know whether it committed.</summary>
internal sealed class AcceptedCommitDisconnectProxy : IAsyncDisposable
{
    private readonly TcpListener _listener=new(IPAddress.Loopback,0);
    private readonly CancellationTokenSource _stop=new(TimeSpan.FromSeconds(30));
    private readonly Task _run;
    internal int Port {get;}
    internal bool CommitForwarded {get;private set;}
    internal AcceptedCommitDisconnectProxy(string host,int port)
    {
        _listener.Start();Port=((IPEndPoint)_listener.LocalEndpoint).Port;_run=RunAsync(host,port);
    }
    private async Task RunAsync(string host,int port)
    {
        using var client=await _listener.AcceptTcpClientAsync(_stop.Token);
        using var server=new TcpClient();await server.ConnectAsync(host,port,_stop.Token);
        using var incoming=client.GetStream();using var outgoing=server.GetStream();
        var replies=outgoing.CopyToAsync(incoming,_stop.Token);
        try
        {
            // Startup has no type byte. No TLS is negotiated on this local test proxy.
            var length=new byte[4];int size;
            do
            {
                await incoming.ReadExactlyAsync(length,_stop.Token);
                size=BinaryPrimitives.ReadInt32BigEndian(length);if(size<8||size>65536)throw new InvalidDataException("Invalid startup packet.");
                var startup=new byte[size-4];await incoming.ReadExactlyAsync(startup,_stop.Token);
                await outgoing.WriteAsync(length,_stop.Token);await outgoing.WriteAsync(startup,_stop.Token);
            }while(size==8); // Negotiation probes precede the real startup message.
            while(!_stop.IsCancellationRequested)
            {
                var tag=new byte[1];await incoming.ReadExactlyAsync(tag,_stop.Token);
                await incoming.ReadExactlyAsync(length,_stop.Token);size=BinaryPrimitives.ReadInt32BigEndian(length);
                if(size<4||size>16777216)throw new InvalidDataException("Invalid frontend packet.");
                var body=new byte[size-4];await incoming.ReadExactlyAsync(body,_stop.Token);
                bool commit=tag[0]==(byte)'Q' && Encoding.ASCII.GetString(body).TrimEnd('\0').Trim().TrimEnd(';').Equals("COMMIT",StringComparison.OrdinalIgnoreCase);
                if(commit)
                {
                    // Stop the server-to-client pump before COMMIT can produce a response.
                    _stop.Cancel();try{await replies;}catch(OperationCanceledException){}
                    await outgoing.WriteAsync(tag);await outgoing.WriteAsync(length);await outgoing.WriteAsync(body);await outgoing.FlushAsync();
                    CommitForwarded=true;return;
                }
                await outgoing.WriteAsync(tag,_stop.Token);await outgoing.WriteAsync(length,_stop.Token);await outgoing.WriteAsync(body,_stop.Token);
            }
        }
        finally
        {
            _stop.Cancel();try{await replies;}catch(OperationCanceledException){}catch(IOException){}
        }
    }
    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();_listener.Stop();
        try{await _run;}catch(OperationCanceledException){}catch(IOException){}catch(SocketException){}
        _stop.Dispose();
    }
}
