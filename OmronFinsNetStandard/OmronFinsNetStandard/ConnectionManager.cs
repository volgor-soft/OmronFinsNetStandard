using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace OmronFinsNetStandard
{
    internal static class ConnectionManager
    {
        // Key: "IP:Port"
        private static readonly ConcurrentDictionary<string, FinsConnection> _connections = new ConcurrentDictionary<string, FinsConnection>();
        private static readonly object _managerLock = new object();

        /// <summary>
        /// Gets an existing connection or creates a new one. Increments usage count.
        /// </summary>
        public static FinsConnection GetConnection(string ip, int port)
        {
            string key = $"{ip}:{port}";

            lock (_managerLock)
            {
                if (!_connections.TryGetValue(key, out var connection))
                {
                    connection = new FinsConnection(ip, port);
                    _connections[key] = connection;
                }

                connection.IncrementUsage();
                return connection;
            }
        }

        /// <summary>
        /// Decrements usage count. If 0, physically closes and removes from pool.
        /// </summary>
        public static void ReleaseConnection(string ip, int port)
        {
            string key = $"{ip}:{port}";

            lock (_managerLock)
            {
                if (_connections.TryGetValue(key, out var connection))
                {
                    int remaining = connection.DecrementUsage();
                    if (remaining <= 0)
                    {
                        connection.Dispose();
                        _connections.TryRemove(key, out _);
                    }
                }
            }
        }
    }
}