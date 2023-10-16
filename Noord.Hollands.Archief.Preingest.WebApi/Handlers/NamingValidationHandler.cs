using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Noord.Hollands.Archief.Preingest.WebApi.EventHub;
using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers
{
    //Check 2.2
    /// <summary>
    /// Handler to check for valid file and folder names.
    /// </summary>
    /// <seealso cref="Noord.Hollands.Archief.Preingest.WebApi.Handlers.AbstractPreingestHandler" />
    /// <seealso cref="System.IDisposable" />
    public class NamingValidationHandler : AbstractPreingestHandler, IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NamingValidationHandler"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="eventHub">The event hub.</param>
        /// <param name="preingestCollection">The preingest collection.</param>
        public NamingValidationHandler(AppSettings settings, IHubContext<PreingestEventHub> eventHub, CollectionHandler preingestCollection) : base(settings, eventHub, preingestCollection)
        {
            this.PreingestEvents += Trigger;
        }
        /// <summary>
        /// Executes this instance.
        /// </summary>
        /// <exception cref="System.IO.DirectoryNotFoundException"></exception>
        public override void Execute()
        {
            bool isSucces = false;
            var anyMessages = new List<String>();
            var eventModel = CurrentActionProperties(TargetCollection, this.GetType().Name);
            try
            {
                base.Execute();

                OnTrigger(new PreingestEventArgs { Description=String.Format("Start name check on folders, sub-folders and files in '{0}'", TargetFolder),  Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Started, PreingestAction = eventModel });

                var collection = new DirectoryInfo(TargetFolder).GetDirectories().OrderBy(item => item.CreationTime).First();
                if (collection == null)
                    throw new DirectoryNotFoundException(String.Format("Directory '{0}' not found!", TargetFolder));

                var result = new List<NamingItem>();

                DirectoryRecursion(collection, result, new PreingestEventArgs { Description = "Walk through the folder structure.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Executing, PreingestAction = eventModel });

                eventModel.Summary.Processed = result.Count();
                eventModel.Summary.Accepted = result.Where(item => item.IsSuccess).Count();
                eventModel.Summary.Rejected = result.Where(item => !item.IsSuccess).Count();

                eventModel.ActionData = result.ToArray();

                if (eventModel.Summary.Rejected > 0)
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Error;
                else
                    eventModel.ActionResult.ResultValue = PreingestActionResults.Success;

                isSucces = true;
            }
            catch(Exception e)
            {
                isSucces = false;
                Logger.LogError(e, "An exception occured in file and folder name check!");
                anyMessages.Clear();
                anyMessages.Add("An exception occured in file and folder name check!");
                anyMessages.Add(e.Message);
                anyMessages.Add(e.StackTrace);

                //eventModel.Summary.Processed = -1;
                eventModel.Summary.Accepted = 0;
                eventModel.Summary.Rejected = eventModel.Summary.Processed;

                eventModel.ActionResult.ResultValue = PreingestActionResults.Failed;
                eventModel.Properties.Messages = anyMessages.ToArray();

                OnTrigger(new PreingestEventArgs { Description = "An exception occured in name check!", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Failed, PreingestAction = eventModel });
            }
            finally
            {
                if (isSucces)
                    OnTrigger(new PreingestEventArgs { Description = "Checking names in files and folders is done.", Initiate = DateTimeOffset.Now, ActionType = PreingestActionStates.Completed, PreingestAction = eventModel });
            }
        }
        /// <summary>
        /// Directories the recursion.
        /// </summary>
        /// <param name="currentFolder">The current folder.</param>
        /// <param name="procesResult">The proces result.</param>
        /// <param name="model">The <see cref="PreingestEventArgs"/> instance containing the event data.</param>
        private void DirectoryRecursion(DirectoryInfo currentFolder, List<NamingItem> procesResult, PreingestEventArgs model)
        {
            model.Description = String.Format ("Checking folder '{0}'.", currentFolder.FullName);            
            OnTrigger(model);

            this.Logger.LogDebug("Checking folder '{0}'.", currentFolder.FullName);

            bool checkResult = ContainsInvalidCharacters(currentFolder.Name);
            bool checkResultNames = ContainsAnyDOSNames(currentFolder.Name);
            var errorMessages = new List<String>();
            if (checkResult)            
                errorMessages.Add(String.Format("Een of meerdere niet-toegestane bijzondere tekens komen voor in de map - of bestandsnaam '{0}'.", currentFolder.Name, currentFolder.FullName));  
            if (checkResultNames)           
                errorMessages.Add(String.Format("De map of het bestand '{0}' heeft een niet-toegestane naam.", currentFolder.Name, currentFolder.FullName));
            if (currentFolder.FullName.Length > 255)
                errorMessages.Add(String.Format("De map of het bestand '{0}' heeft een totale lengte groter dan 255 karakters (volledige pad).", currentFolder.Name, currentFolder.FullName));

            procesResult.Add(new NamingItem
            {
                ContainsInvalidCharacters = checkResult,
                ContainsDosNames = checkResultNames,
                Name = currentFolder.FullName,
                Length = currentFolder.FullName.Length,
                ErrorMessages = errorMessages.ToArray()               
            });

            currentFolder.GetFiles().ToList().ForEach(item =>
            {
                model.Description = String.Format("Checking file '{0}'.", item.FullName);                
                OnTrigger(model);

                this.Logger.LogDebug("Checking file '{0}'", currentFolder.FullName);
                var errorMessages = new List<String>();
                bool checkResult = ContainsInvalidCharacters(item.Name);
                if (checkResult)                
                    errorMessages.Add(String.Format("Een of meerdere niet-toegestane bijzondere tekens komen voor in de map - of bestandsnaam '{0}'", item.Name, item.FullName));
                bool checkResultNames = ContainsAnyDOSNames(item.Name);
                if (checkResultNames)                
                    errorMessages.Add(String.Format("De map of het bestand '{0}' heeft een niet-toegestane naam.", item.Name, item.FullName));
                if(item.FullName.Length > 255)
                    errorMessages.Add(String.Format("De map of het bestand '{0}' heeft een totale lengte groter dan 255 karakters (volledige pad).", item.Name, item.FullName));

                procesResult.Add(new NamingItem
                {
                    ContainsInvalidCharacters = checkResult,
                    ContainsDosNames = checkResultNames,
                    Length = item.FullName.Length,                   
                    Name = item.FullName,
                    ErrorMessages = errorMessages.ToArray()
                });
            });

            foreach (var directory in currentFolder.GetDirectories())
                DirectoryRecursion(directory, procesResult, model);
        }
        /// <summary>
        /// Determines whether [contains invalid characters] [the specified test name].
        /// </summary>
        /// <param name="testName">Name of the test.</param>
        /// <returns>
        ///   <c>true</c> if [contains invalid characters] [the specified test name]; otherwise, <c>false</c>.
        /// </returns>
        private bool ContainsInvalidCharacters(string testName)
        {
            if (this.IsToPX)
            {
                Regex containsABadCharacter = new Regex("[\\?*:\"​|/<>#&‌​]");
                bool result = (containsABadCharacter.IsMatch(testName));

                return result;
            }
            else
            {
                Regex containsABadCharacter = new Regex("[\\?*:\"|/<>#&‌\\s+]");
                bool result = containsABadCharacter.IsMatch(testName);
                return result;
            }

        }
        /// <summary>
        /// Determines whether [contains any dos names] [the specified test name].
        /// </summary>
        /// <param name="testName">Name of the test.</param>
        /// <returns>
        ///   <c>true</c> if [contains any dos names] [the specified test name]; otherwise, <c>false</c>.
        /// </returns>
        private bool ContainsAnyDOSNames(string testName)
        {
            Regex containsAnyDOSNames = new Regex("^(PRN|AUX|NUL|CON|COM[0-9]|LPT[0-9]|(\\.+))$", RegexOptions.IgnoreCase);
            return (containsAnyDOSNames.IsMatch(testName));
        }
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.PreingestEvents -= Trigger;
        }

        static bool IsValidPath(string p)
        {
            if ((File.GetAttributes(p) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                return false;
            }
            if (!IsReadable(p))
            {
                return false;
            }
            return true;
        }
        static bool IsReadable(string p)
        {
            try
            {
                string[] s = Directory.GetDirectories(p);
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            return true;
        }
    }
}
