using Microsoft.AspNetCore.SignalR;

using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Model;

using System;
using System.Net.Sockets;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers
{
    /// <summary>
    /// Health check for each microservices
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class HealthCheckHandler : AbstractPreingestHandler, IDisposable
    {
        AppSettings _settings = null;
        /// <summary>
        /// Initializes a new instance of the <see cref="HealthCheckHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public HealthCheckHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) : base(settings, eventHub, preingestCollection)
        {
            _settings = settings;
        }

        /// <summary>
        /// Executes this instance.
        /// </summary>
        public override void Execute()
        {        
            try
            {
                using (TcpClient clamav = new TcpClient(_settings.ClamServerNameOrIp, Int32.Parse(_settings.ClamServerPort)))
                    IsAliveClamAv = clamav.Connected;
            }
            catch
            {
                IsAliveClamAv = false;
            }

            try
            {

                using (TcpClient xslweb = new TcpClient(_settings.XslWebServerName, Int32.Parse(_settings.XslWebServerPort)))
                    IsAliveXslWeb = xslweb.Connected;
            }
            catch
            {
                IsAliveXslWeb = false;
            }

            try
            {
                using (TcpClient droid = new TcpClient(_settings.DroidServerName, Int32.Parse(_settings.DroidServerPort)))
                    IsAliveDroid = droid.Connected;
            }
            catch
            {
                IsAliveDroid = false;
            }
            
            try
            {
                using (var context = new PreIngestStatusContext())
                {
                    IsAliveDatabase = context.Database.CanConnect();   
                }
            }
            catch
            {
                IsAliveDatabase = false;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
           
        }
        /// <summary>
        /// Gets or sets a value indicating whether this instance is alive droid.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is alive droid; otherwise, <c>false</c>.
        /// </value>
        public bool IsAliveDroid { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether this instance is alive XSL web.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is alive XSL web; otherwise, <c>false</c>.
        /// </value>
        public bool IsAliveXslWeb { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether this instance is alive clam av.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is alive clam av; otherwise, <c>false</c>.
        /// </value>
        public bool IsAliveClamAv { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether this instance is alive database.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is alive database; otherwise, <c>false</c>.
        /// </value>
        public bool IsAliveDatabase { get; set; }
    }
}
