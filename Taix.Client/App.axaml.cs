using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Taix.Client.Logging;
using Taix.Client.Servicers;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.Views;

using System.Net;
using System.Net.Sockets;

namespace Taix.Client;

public class App : Application
{
#if !DEBUG
 private Mutex _mutex;
#endif
    private CancellationTokenSource _wakePipeCts = new();
    private FileLogger? _fileLogger;

    private const string UnixSocketPath = "/tmp/taix-client.sock";

    private bool IsRunned()
    {
#if DEBUG
        return false;
#else
        var mutexName = "Taix";
        bool createdNew;
        _mutex = new Mutex(true, mutexName, out createdNew);
        if (createdNew) return false;

        // Already running, try to wake existing instance
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: Named Pipe
                using var pipe = new NamedPipeClientStream(".", "TaixClient", PipeDirection.Out);
                pipe.Connect(1000);
                using var writer = new StreamWriter(pipe) { AutoFlush = true };
                writer.WriteLine("show");
            }
            else
            {
                // macOS/Linux: Unix Domain Socket
                using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                var endpoint = new UnixDomainSocketEndPoint(UnixSocketPath);
                socket.Connect(endpoint);
                using var stream = new NetworkStream(socket);
                using var writer = new StreamWriter(stream) { AutoFlush = true };
                writer.WriteLine("show");
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"[SingleInstance] Wake failed: {ex.Message}");
        }

        return true;
#endif
    }

    private void StartWakePipeServer()
    {
        _ = Task.Run(async () =>
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: Named Pipe
                while (!IsShuttingDown)
                {
                    try
                    {
                        var pipe = new NamedPipeServerStream("TaixClient", PipeDirection.In, 10);
                        await pipe.WaitForConnectionAsync(_wakePipeCts.Token);
                        _ = HandlePipeConnectionAsync(pipe, _wakePipeCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"[WakePipeServer] Error: {ex.Message}");
                        try { await Task.Delay(100, _wakePipeCts.Token); } catch (OperationCanceledException) { break; }
                    }
                }
            }
            else
            {
                // macOS/Linux: Unix Domain Socket
                try
                {
                    if (File.Exists(UnixSocketPath))
                        File.Delete(UnixSocketPath);

                    using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    var endpoint = new UnixDomainSocketEndPoint(UnixSocketPath);
                    listener.Bind(endpoint);
                    listener.Listen(10);

                    while (!IsShuttingDown)
                    {
                        var clientSocket = await listener.AcceptAsync(_wakePipeCts.Token);
                        _ = HandleSocketConnectionAsync(clientSocket, _wakePipeCts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // ignored
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[WakeSocketServer] Error: {ex.Message}");
                }
                finally
                {
                    try { File.Delete(UnixSocketPath); } catch { }
                }
            }
        }, _wakePipeCts.Token);
    }

    private static async Task HandlePipeConnectionAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        try
        {
            await using (pipe)
            {
                using var reader = new StreamReader(pipe);
                var cmd = await reader.ReadLineAsync(ct);
                if (cmd == "show")
                {
                    await ShowMainWindowAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        catch (Exception ex)
        {
            Logger.Warn($"[WakePipeServer] Error: {ex.Message}");
        }
    }

    private static async Task HandleSocketConnectionAsync(Socket socket, CancellationToken ct)
    {
        try
        {
            using var stream = new NetworkStream(socket);
            using var reader = new StreamReader(stream);
            var cmd = await reader.ReadLineAsync(ct);
            if (cmd == "show")
            {
                await ShowMainWindowAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        catch (Exception ex)
        {
            Logger.Warn($"[WakeSocketServer] Error: {ex.Message}");
        }
        finally
        {
            try { socket.Dispose(); } catch { }
        }
    }

    private static async Task ShowMainWindowAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var desk = Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (desk?.MainWindow is MainWindow mw)
            {
                if (!mw.IsVisible) mw.IsVisible = true;
                if (mw.WindowState == WindowState.Minimized) mw.WindowState = WindowState.Normal;
                mw.Activate();
            }
        });
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _fileLogger = new FileLogger(options =>
        {
            options.MaxLogFileAgeDays = 30;
            options.SaveThreshold = 100;
            options.AutoSaveInterval = 1000 * 60 * 10;
#if DEBUG
            options.WriteToConsole = true;
#endif
        });
        Logger.SetLogger(_fileLogger);

        Dispatcher.UIThread.UnhandledException += (sender, e) =>
        {
            Logger.Error("[Program crash] " + e.Exception.Message, e.Exception);
            Logger.Error("Stack trace", e.Exception);
            Logger.Flush();
            new Views.Dialogs.ErrorDialog().Show();
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var exception = e.ExceptionObject as Exception;
            Logger.Error("[Program crash]" + exception?.Message, exception);
            Logger.Flush();
        };
        ServiceLocator.Initialize(new AppServiceProvider());

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            OnStartup(this, Environment.GetCommandLineArgs());
            desktop.Exit += (e, r) =>
            {
                _wakePipeCts.Cancel();
                Logger.Flush();
                _fileLogger?.Dispose();
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try { File.Delete(UnixSocketPath); } catch { }
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void OnStartup(object sender, string[] args)
    {
        try
        {
            await OnStartupAsync(sender, args);
        }
        catch (Exception e)
        {
            Logger.Error(e.Message, e);
            Logger.Flush();
        }
    }

    private async Task OnStartupAsync(object sender, string[] args)
    {
        if (IsRunned()) Environment.Exit(0);
#if !DEBUG
        StartWakePipeServer();
#endif
        var main = ServiceLocator.GetService<IMainServicer>();
        await main.Start();
    }


    private static bool _isShuttingDown;
    public static bool IsShuttingDown => _isShuttingDown;

    public static async Task ExitAsync()
    {
        if (_isShuttingDown) return;
        _isShuttingDown = true;

        var desktop = Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktop == null) return;

        var shutdownService = ServiceLocator.GetService<IShutdownService>();
        if (shutdownService != null)
            await shutdownService.ShutdownAsync();

        desktop.Shutdown();
    }
}
