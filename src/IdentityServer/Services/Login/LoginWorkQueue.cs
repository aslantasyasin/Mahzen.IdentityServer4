using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace IdentityServer.Services.Login;

public sealed class LoginWorkQueue : ILoginWork
{
    public const int Capacity = 5000;

    private readonly Channel<(string Name, Func<LoginProtectionService, Task> Job)> _channel =
        Channel.CreateBounded<(string, Func<LoginProtectionService, Task>)>(new BoundedChannelOptions(Capacity) { SingleReader = true });

    private readonly ILogger<LoginWorkQueue> _logger;

    public LoginWorkQueue(ILogger<LoginWorkQueue> logger) => _logger = logger;

    public ChannelReader<(string Name, Func<LoginProtectionService, Task> Job)> Reader => _channel.Reader;

    public void Enqueue(string name, Func<LoginProtectionService, Task> job)
    {
        if (!_channel.Writer.TryWrite((name, job)))
            _logger.LogError("login_work_dropped Job={Job} Reason=queue_full", name);
    }
}
