using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FileServer
{
    class Program
    {
        private static TcpListener m_listener;
        private static string m_monitoredFolder;
        private static readonly object _fileLock = new object();

        static async Task Main(string[] args)
        {
            Console.WriteLine("FileServer starting");

            m_monitoredFolder = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
            if (!Directory.Exists(m_monitoredFolder))
            {
                Console.WriteLine($"Error: Folder '{m_monitoredFolder}' does not exist. ");
                return;
            }

            int port = args.Length > 1 && int.TryParse(args[1], out int parsedPort) ? parsedPort : 13000;
            Console.WriteLine($"Monitoring folder: {m_monitoredFolder} on port {port}");


            m_listener = new TcpListener(IPAddress.Any, 13000);
            m_listener.Start();
            Console.WriteLine("Server started");

            try
            {
                while (true)
                {
                    var client = await m_listener.AcceptTcpClientAsync();
                    _ = handleClientAsync(client);
                }
            }
            finally
            {
                Console.CancelKeyPress += (_, e) => { m_listener?.Stop(); };
            }
        }

        private static async Task handleClientAsync(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })

                {
                    stream.ReadTimeout = 5000;

                    while (client.Connected)
                    {
                        var command = await reader.ReadLineAsync();
                        if (string.IsNullOrEmpty(command))
                        {
                            continue;
                        }

                        Console.WriteLine($"Received command: {command}");

                        if (command == "LIST_FILE")
                        {
                            var files = Directory.GetFiles(m_monitoredFolder);
                            await writer.WriteLineAsync(string.Join("|", files));
                        }
                        else if (command.StartsWith("GET_FILE:"))
                        {
                            var filePath = command.Substring(9);
                            var fullPath = Path.GetFullPath(Path.Combine(m_monitoredFolder, filePath));

                            if (!fullPath.StartsWith(m_monitoredFolder))
                            {
                                await writer.WriteLineAsync("Error:Invalid Path");
                                continue;
                            }

                            if (File.Exists(fullPath))
                            {
                                try
                                {
                                    var content = await File.ReadAllBytesAsync(filePath);
                                    await writer.WriteLineAsync(Convert.ToBase64String(content));
                                }
                                catch (Exception ex)
                                {

                                    await writer.WriteLineAsync("File_NOT_FOUND");

                                }

                            }
                        }
                        else if (command.StartsWith("SAVE_FILE:"))
                        {
                            var parts = command.Split(new[] { ':' }, 3);
                            if (parts.Length != 3)
                            {
                                await writer.WriteLineAsync("ERROR:INVALID_SAVE_FORMAT");
                                continue;
                            }
                            var filePath = Path.Combine(m_monitoredFolder, parts[1]);
                            var content = Convert.FromBase64String(parts[2]);
                            await File.WriteAllBytesAsync(filePath, content);
                            await writer.WriteLineAsync("SAVE_SUCCESS");

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
            }
        }
    }
}