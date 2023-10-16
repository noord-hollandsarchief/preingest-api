using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers
{
    /// <summary>
    /// Interface for al handlers
    /// </summary>
    public interface IPreingest
    {
        /// <summary>
        /// Gets a value indicating whether this instance is to px.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is to px; otherwise, <c>false</c>.
        /// </value>
        Boolean IsToPX { get; }
        /// <summary>
        /// Gets a value indicating whether this instance is mdto.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is mdto; otherwise, <c>false</c>.
        /// </value>
        Boolean IsMDTO { get; }
        /// <summary>
        /// Gets the application settings.
        /// </summary>
        /// <value>
        /// The application settings.
        /// </value>
        AppSettings ApplicationSettings { get; }
        /// <summary>
        /// Executes this instance.
        /// </summary>
        void Execute();
        /// <summary>
        /// Gets the session unique identifier.
        /// </summary>
        /// <value>
        /// The session unique identifier.
        /// </value>
        Guid SessionGuid { get; }
        /// <summary>
        /// Gets or sets the action process identifier.
        /// </summary>
        /// <value>
        /// The action process identifier.
        /// </value>
        Guid ActionProcessId { get; set; }
        /// <summary>
        /// Sets the session unique identifier.
        /// </summary>
        /// <param name="guid">The unique identifier.</param>
        void SetSessionGuid(Guid guid);
        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        /// <value>
        /// The logger.
        /// </value>
        ILogger Logger { get; set; }
        /// <summary>
        /// Gets or sets the tar filename.
        /// </summary>
        /// <value>
        /// The tar filename.
        /// </value>
        public String TarFilename { get; set; }
        /// <summary>
        /// Gets the target collection.
        /// </summary>
        /// <value>
        /// The target collection.
        /// </value>
        public String TargetCollection { get; }
        /// <summary>
        /// Gets the target folder.
        /// </summary>
        /// <value>
        /// The target folder.
        /// </value>
        public String TargetFolder { get; }
        /// <summary>
        /// Adds the process action.
        /// </summary>
        /// <param name="processId">The process identifier.</param>
        /// <param name="name">The name.</param>
        /// <param name="description">The description.</param>
        /// <param name="result">The result.</param>
        /// <returns></returns>
        Guid AddProcessAction(Guid processId, String name, String description, String result);
        /// <summary>
        /// Updates the process action.
        /// </summary>
        /// <param name="actionId">The action identifier.</param>
        /// <param name="result">The result.</param>
        /// <param name="summary">The summary.</param>
        void UpdateProcessAction(Guid actionId, String result, String summary);
        /// <summary>
        /// Adds the start state.
        /// </summary>
        /// <param name="processId">The process identifier.</param>
        void AddStartState(Guid processId);
        /// <summary>
        /// Adds the state of the complete.
        /// </summary>
        /// <param name="processId">The process identifier.</param>
        void AddCompleteState(Guid processId);
        /// <summary>
        /// Adds the state of the failed.
        /// </summary>
        /// <param name="processId">The process identifier.</param>
        /// <param name="message">The message.</param>
        void AddFailedState(Guid processId, string message);
        /// <summary>
        /// Validates the action.
        /// </summary>
        void ValidateAction();
        /// <summary>
        /// Triggers the specified sender.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="PreingestEventArgs"/> instance containing the event data.</param>
        void Trigger(object sender, PreingestEventArgs e);
    }
}
