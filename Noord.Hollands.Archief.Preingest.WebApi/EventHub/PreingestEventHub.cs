using Microsoft.AspNetCore.SignalR;

using System;
using System.Threading.Tasks;

namespace Noord.Hollands.Archief.Preingest.WebApi.EventHub
{
    /// <summary>
    /// Interface for event information
    /// </summary>
    public interface IEventHub
    {
        // here place some method(s) for message from server to client
        Task SendNoticeEventToClient(string message);
        Task CollectionsStatus(string jsonData);
        Task CollectionStatus(Guid guid, string jsonData);
        Task SendNoticeToWorkerService(Guid guid, string jsonData);
    }

    /// <summary>
    /// Concrete object that extends the interface
    /// </summary>
    /// <seealso cref="Microsoft.AspNetCore.SignalR.Hub&lt;Noord.Hollands.Archief.Preingest.WebApi.EventHub.IEventHub&gt;" />
    public class PreingestEventHub : Hub<IEventHub>
    {    }
}
