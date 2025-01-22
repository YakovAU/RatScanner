using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;

namespace RatScanner
{
    public class NetworkManager
    {
        private TcpListener? _server;
        private readonly List<TcpClient> _clients = new();
        private bool _isServer;
        private TcpClient? _clientConnection;

        public bool IsServer => _isServer;
        public bool IsConnected => _isServer ? _clients.Count > 0 : _clientConnection?.Connected ?? false;

        public async Task StartServer()
        {
            if (_server != null) return;
            
            try
            {
                _server = new TcpListener(IPAddress.Any, RatConfig.Network.ServerPort);
                _server.Start();
                _isServer = true;

                // Accept clients in background
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            var client = await _server.AcceptTcpClientAsync();
                            _clients.Add(client);
                        }
                        catch (Exception)
                        {
                            break;
                        }
                    }
                });
            }
            catch (Exception)
            {
                _server = null;
                throw;
            }
        }

        public async Task ConnectToServer()
        {
            if (_clientConnection?.Connected ?? false) return;

            try
            {
                _clientConnection = new TcpClient();
                await _clientConnection.ConnectAsync(IPAddress.Parse(RatConfig.Network.ServerIP), RatConfig.Network.ServerPort);
                _isServer = false;

                // Listen for URLs in background
                _ = Task.Run(async () =>
                {
                    var buffer = new byte[1024];
                    while (_clientConnection.Connected)
                    {
                        try
                        {
                            var stream = _clientConnection.GetStream();
                            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                            if (bytesRead == 0) break; // Connection closed

                            var url = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            OpenReceivedURL(url);
                        }
                        catch (Exception)
                        {
                            break;
                        }
                    }
                });
            }
            catch (Exception)
            {
                _clientConnection?.Dispose();
                _clientConnection = null;
                throw;
            }
        }

        public async Task BroadcastURL(string url)
        {
            if (!_isServer || _clients.Count == 0) return;

            var data = System.Text.Encoding.UTF8.GetBytes(url);
            var deadClients = new List<TcpClient>();

            foreach (var client in _clients)
            {
                try
                {
                    if (!client.Connected)
                    {
                        deadClients.Add(client);
                        continue;
                    }

                    var stream = client.GetStream();
                    await stream.WriteAsync(data, 0, data.Length);
                }
                catch (Exception)
                {
                    deadClients.Add(client);
                }
            }

            // Cleanup disconnected clients
            foreach (var client in deadClients)
            {
                _clients.Remove(client);
                client.Dispose();
            }
        }

        private void OpenReceivedURL(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            };
            Process.Start(psi);
        }

        public void Cleanup()
        {
            foreach (var client in _clients)
            {
                client.Dispose();
            }
            _clients.Clear();
            
            _clientConnection?.Dispose();
            _clientConnection = null;
            
            _server?.Stop();
            _server = null;

            _isServer = false; // Reset server state
        }
    }
}
