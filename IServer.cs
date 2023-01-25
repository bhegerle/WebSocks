namespace WebStunnel;

internal interface IServer : IAsyncDisposable {
    Task Start(CancellationToken token);
}
