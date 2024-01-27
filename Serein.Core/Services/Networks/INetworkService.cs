using System;
using System.Threading.Tasks;

using WatsonWebsocket;

namespace Serein.Core.Services.Networks;

public interface INetworkService : IDisposable
{
    event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    bool Active { get; }

    Statistics? Stats { get; }

    Task SendAsync(string text);

    void Start();

    void Stop();
}
