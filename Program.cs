using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace FileServer
{
    class Program
    {
        private static TcpListener m_listener;
        private static string m_monitoredFolder;

        static async Task Main(string[] args)
        {
            Console.WriteLine("FileServer starting");

            m_monitoredFolder = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
            Console.WriteLine($"Monitoring folder: {m_monitoredFolder}");

            m_listener = new TcpListener(IPAddress.Any, 13000);
            m_listener.Start();
            Console.WriteLine("Server started");
            while (true)
            {
                var client = await m_listener.AcceptTcpClientAsync();
                _ = handleClientAsync(client);
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

                    while (client.Connected)
                    {
                        var command = await reader.ReadLineAsync();
                        if (string.IsNullOrEmpty(command))
                        {
                            continue;
                        }

                        Console.WriteLine($"Received command: {command}");

                        if (command == "LIST_FILES")
                        {
                            var files = Directory.GetFiles(m_monitoredFolder);
                            await writer.WriteLineAsync(string.Join("|", files));
                        }
                        else if (command.StartsWith("GET_FILES:"))
                        {
                            var filePath = command.Substring(9);
                            if (File.Exists(filePath))
                            {
                                var content = await File.ReadAllBytesAsync(filePath);
                                await writer.WriteLineAsync(Convert.ToBase64String(content));
                            }
                            else
                            {
                                await writer.WriteLineAsync("File_NOT_FOUND");
                            }
                        }
                        else if (command.StartsWith("SAVE_FILE:"))
                        {
                            var parts = command.Split(new[] { ':' }, 3);
                            if (parts.Length == 3)
                            {
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