using System.IO;
using System.IO.Pipes;

namespace InboxDock.App.SystemIntegration;

/// <summary>
/// 通过命名互斥体保证单实例。第二实例通过命名管道通知第一实例展开并聚焦，然后退出。
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = "InboxDock_SingleInstance_Mutex";
    private const string PipeName = "InboxDock_SingleInstance_Pipe";

    private Mutex? mutex;
    private CancellationTokenSource? pipeServerToken;
    private bool disposed;

    /// <summary>尝试获取单实例锁。返回 true 表示当前是第一个实例。</summary>
    public bool TryAcquire()
    {
        mutex = new Mutex(initiallyOwned: false, MutexName, out var createdNew);
        return createdNew;
    }

    /// <summary>启动命名管道服务器，监听第二实例的呼出请求。</summary>
    public void StartServer(Action onActivate)
    {
        pipeServerToken = new CancellationTokenSource();
        _ = ListenAsync(onActivate, pipeServerToken.Token);
    }

    /// <summary>作为第二实例通知第一实例展开。失败时静默忽略。</summary>
    public static void SignalFirstInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(
                serverName: ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.None,
                System.Security.Principal.TokenImpersonationLevel.None);
            client.Connect(timeout: 2000);
            using var writer = new StreamWriter(client);
            writer.Write("ACTIVATE");
            writer.Flush();
        }
        catch
        {
            // 第一实例已退出或管道不可用，忽略。
        }
    }

    private async Task ListenAsync(Action onActivate, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(token);
                using var reader = new StreamReader(server);
                var message = await reader.ReadToEndAsync(token);
                server.Dispose();
                server = null;
                if (message.Contains("ACTIVATE", StringComparison.OrdinalIgnoreCase))
                {
                    onActivate();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // 管道异常时不中断监听循环。
            }
            finally
            {
                server?.Dispose();
            }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        pipeServerToken?.Cancel();
        pipeServerToken?.Dispose();
        try
        {
            if (mutex is not null)
            {
                mutex.ReleaseMutex();
                mutex.Dispose();
            }
        }
        catch
        {
            // 释放失败时忽略，进程退出会自动清理。
        }
    }
}
