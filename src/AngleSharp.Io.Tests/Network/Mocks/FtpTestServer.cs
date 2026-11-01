namespace AngleSharp.Io.Tests.Network.Mocks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A minimal in-process FTP server bound to the loopback interface. It
    /// understands just enough of RFC 959 (login, PWD/CWD, TYPE, passive
    /// mode, SIZE, RETR) for FtpWebRequest to download a file.
    /// </summary>
    sealed class FtpTestServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts;
        private readonly Dictionary<String, Byte[]> _files;
        private readonly Task _acceptLoop;

        public FtpTestServer(IDictionary<String, String> files)
        {
            _files = new Dictionary<String, Byte[]>(StringComparer.Ordinal);

            foreach (var file in files)
            {
                _files[Normalize(file.Key)] = Encoding.UTF8.GetBytes(file.Value);
            }

            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _acceptLoop = AcceptLoopAsync();
        }

        public Int32 Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public String GetUrl(String path) => $"ftp://127.0.0.1:{Port}{Normalize(path)}";

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();

            try
            {
                _acceptLoop.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
            }

            _cts.Dispose();
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient client;

                try
                {
                    client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (Exception) when (_cts.IsCancellationRequested)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                _ = Task.Run(() => HandleSessionAsync(client));
            }
        }

        private async Task HandleSessionAsync(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.ASCII))
            using (var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true })
            {
                var cwd = "/";
                TcpListener passive = null;

                try
                {
                    await writer.WriteLineAsync("220 AngleSharp test FTP server ready").ConfigureAwait(false);

                    while (!_cts.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync().ConfigureAwait(false);

                        if (line == null)
                        {
                            break;
                        }

                        var space = line.IndexOf(' ');
                        var command = (space < 0 ? line : line.Substring(0, space)).ToUpperInvariant();
                        var argument = space < 0 ? String.Empty : line.Substring(space + 1);

                        switch (command)
                        {
                            case "USER":
                                await writer.WriteLineAsync("331 Password required").ConfigureAwait(false);
                                break;
                            case "PASS":
                                await writer.WriteLineAsync("230 Logged in").ConfigureAwait(false);
                                break;
                            case "PWD":
                                await writer.WriteLineAsync($"257 \"{cwd}\" is current directory").ConfigureAwait(false);
                                break;
                            case "CWD":
                                cwd = Combine(cwd, argument);
                                await writer.WriteLineAsync("250 Directory changed").ConfigureAwait(false);
                                break;
                            case "TYPE":
                                await writer.WriteLineAsync("200 Type set").ConfigureAwait(false);
                                break;
                            case "SIZE":
                                if (_files.TryGetValue(Combine(cwd, argument), out var sized))
                                {
                                    await writer.WriteLineAsync($"213 {sized.Length}").ConfigureAwait(false);
                                }
                                else
                                {
                                    await writer.WriteLineAsync("550 File not found").ConfigureAwait(false);
                                }
                                break;
                            case "PASV":
                                passive?.Stop();
                                passive = new TcpListener(IPAddress.Loopback, 0);
                                passive.Start();
                                var port = ((IPEndPoint)passive.LocalEndpoint).Port;
                                await writer.WriteLineAsync($"227 Entering Passive Mode (127,0,0,1,{port >> 8},{port & 0xFF})").ConfigureAwait(false);
                                break;
                            case "RETR":
                                if (passive == null)
                                {
                                    await writer.WriteLineAsync("425 Use PASV first").ConfigureAwait(false);
                                }
                                else if (_files.TryGetValue(Combine(cwd, argument), out var content))
                                {
                                    await writer.WriteLineAsync("150 Opening data connection").ConfigureAwait(false);

                                    using (var data = await passive.AcceptTcpClientAsync().ConfigureAwait(false))
                                    using (var dataStream = data.GetStream())
                                    {
                                        await dataStream.WriteAsync(content, 0, content.Length).ConfigureAwait(false);
                                    }

                                    passive.Stop();
                                    passive = null;
                                    await writer.WriteLineAsync("226 Transfer complete").ConfigureAwait(false);
                                }
                                else
                                {
                                    await writer.WriteLineAsync("550 File not found").ConfigureAwait(false);
                                }
                                break;
                            case "QUIT":
                                await writer.WriteLineAsync("221 Bye").ConfigureAwait(false);
                                return;
                            default:
                                // FtpWebRequest probes optional features (e.g. OPTS utf8);
                                // a positive reply keeps it from aborting the session.
                                await writer.WriteLineAsync("200 OK").ConfigureAwait(false);
                                break;
                        }
                    }
                }
                catch (IOException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                finally
                {
                    passive?.Stop();
                }
            }
        }

        private static String Combine(String cwd, String path)
        {
            if (path.StartsWith("/", StringComparison.Ordinal))
            {
                return Normalize(path);
            }

            return Normalize(cwd.TrimEnd('/') + "/" + path);
        }

        private static String Normalize(String path)
        {
            var trimmed = path.Trim();

            if (trimmed.Length > 1)
            {
                trimmed = trimmed.TrimEnd('/');
            }

            return trimmed.StartsWith("/", StringComparison.Ordinal) ? trimmed : "/" + trimmed;
        }
    }
}
