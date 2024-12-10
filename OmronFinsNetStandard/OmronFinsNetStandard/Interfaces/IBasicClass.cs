using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace OmronFinsNetStandard.Interfaces
{
    public interface IBasicClass : IDisposable
    {
        byte PCNode { get; set; }
        byte PLCNode { get; set; }
        Task ConnectAsync(string ipAddress, int port);
        Task SendDataAsync(byte[] data);
        Task<int> ReceiveDataAsync(byte[] buffer);
        Task<bool> PingCheckAsync(string ipAddress, int timeout);
        void Disconnect();
    }
}
