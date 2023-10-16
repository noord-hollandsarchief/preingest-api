using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Noord.Hollands.Archief.Preingest.WebApi.Handlers.TreeView
{
    /// <summary>
    /// 
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public class DataTreeHandler : IDisposable
    {
        public TreeRoot DataRoot { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataTreeHandler"/> class.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="di">The di.</param>
        public DataTreeHandler(Guid sessionId, DirectoryInfo di)
        {
            CurrentDirectoryInfo = di;
            DataRoot = new TreeRoot();
            SessionId = sessionId;
        }

        /// <summary>
        /// Gets or sets the start folder.
        /// </summary>
        /// <value>
        /// The start folder.
        /// </value>
        public String StartFolder { get; set; }

        /// <summary>
        /// Gets or sets the session identifier.
        /// </summary>
        /// <value>
        /// The session identifier.
        /// </value>
        public Guid SessionId { get; set; }
        /// <summary>
        /// Gets or sets the current directory information.
        /// </summary>
        /// <value>
        /// The current directory information.
        /// </value>
        public DirectoryInfo CurrentDirectoryInfo
        {
            get; set;
        }

        /// <summary>
        /// Loads the folder structure.
        /// </summary>
        public void Load()
        {
            if (CurrentDirectoryInfo.Exists)
            {
                if (File.Exists(JsonDataFile))
                {
                    string extensionJson = File.ReadAllText(JsonDataFile);
                    TreeRoot treeRoot = JsonConvert.DeserializeObject<TreeRoot>(extensionJson);
                    DataRoot = treeRoot;
                }
                else
                {
                    var sessionFolder = GetDirectoryRecursivly(CurrentDirectoryInfo);
                    sessionFolder.Key = Guid.NewGuid().ToString();
                    sessionFolder.Label = String.Format("Collectie ID: {0}", CurrentDirectoryInfo.Name);
                    sessionFolder.Icon = "pi pi-fw pi-book";
                    sessionFolder.Data = CurrentDirectoryInfo.FullName;
                    DataRoot.Root.Add(sessionFolder);
                    this.Save();
                }
            }
        }
        /// <summary>
        /// Gets the json data to file.
        /// </summary>
        /// <value>
        /// The json data file.
        /// </value>
        public String JsonDataFile
        {
            get => Path.Combine(CurrentDirectoryInfo.FullName, String.Concat(this.GetType().Name, ".json"));
        }
        /// <summary>
        /// Saves folder structure to file as JSON.
        /// </summary>
        public void Save()
        {
            string outputFile = Path.Combine(CurrentDirectoryInfo.FullName, String.Concat(this.GetType().Name, ".json"));
            try
            {
                using (StreamWriter file = File.CreateText(outputFile))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.ContractResolver = new CamelCasePropertyNamesContractResolver();
                    serializer.NullValueHandling = NullValueHandling.Ignore;
                    serializer.Serialize(file, DataRoot);
                }
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Delete the cached JSON file.
        /// </summary>
        public void ClearCachedJson()
        {
            string outputFile = Path.Combine(CurrentDirectoryInfo.FullName, String.Concat(this.GetType().Name, ".json"));
            try
            {
                if (File.Exists(outputFile))
                    File.Delete(outputFile);
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Encode strin value with base64
        /// </summary>
        /// <param name="plainText">The plain text.</param>
        /// <returns></returns>
        public static string Base64Encode(string plainText)
        {
            if (String.IsNullOrEmpty(plainText))
                return plainText;

            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
        /// <summary>
        /// Decode the string with base64.
        /// </summary>
        /// <param name="base64EncodedData">The base64 encoded data.</param>
        /// <returns></returns>
        public static string Base64Decode(string base64EncodedData)
        {
            if (String.IsNullOrEmpty(base64EncodedData))
                return base64EncodedData;

            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }

        /// <summary>
        /// Gets the content of the item.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public static ItemContent GetItemContent(string path)
        {
            string result = String.Empty;
            try
            {
                XDocument document = XDocument.Load(path);
                result = DataTreeHandler.Base64Encode(document.ToString());
            }
            catch (Exception) { }
            return new ItemContent { Data = result };
        }

        /// <summary>
        /// Gets the item properties.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="startFolderToRemove">The start folder to remove.</param>
        /// <returns></returns>
        public static ItemContent GetItemProperties(string path, string startFolderToRemove)
        {
            FileInfo metadata = new FileInfo(path);
            if (!metadata.Exists)
                return null;

            FileInfo binary = null;
            ItemFileProps fileProps = null;
            if (path.EndsWith(".metadata", StringComparison.InvariantCultureIgnoreCase))
            {
                binary = new FileInfo(path.Replace(".metadata", string.Empty));
            }
            if (path.EndsWith(".mdto.xml", StringComparison.InvariantCultureIgnoreCase))
            {
                binary = new FileInfo(path.Replace(".mdto.xml", string.Empty));
            }
            if (path.EndsWith(".bestand.mdto.xml", StringComparison.InvariantCultureIgnoreCase))
            {
                binary = new FileInfo(path.Replace(".bestand.mdto.xml", string.Empty));
            }
            if (path.EndsWith(".opex", StringComparison.InvariantCultureIgnoreCase))
            {
                binary = new FileInfo(path.Replace(".opex", string.Empty));
            }
            if (binary.Exists)
            {
                fileProps = new ItemFileProps
                {
                    Name = binary.Name,
                    Location = binary.DirectoryName.Replace(startFolderToRemove, String.Empty).Replace("/", " / "),
                    CreationDateTime = binary.CreationTime.ToString("dd-MM-yyyy hh:mm:ss"),
                    LastModified = binary.LastWriteTime.ToString("dd-MM-yyyy hh:mm:ss"),
                    Size = FormatSize(binary.Length)
                };
            }

            return new ItemContent
            {
                Data = new ItemProps
                {
                    Name = metadata.Name,
                    Location = metadata.DirectoryName.Replace(startFolderToRemove, String.Empty).Replace("/", " / "),
                    CreationDateTime = metadata.CreationTime.ToString("dd-MM-yyyy hh:mm:ss"),
                    LastModified = metadata.LastWriteTime.ToString("dd-MM-yyyy hh:mm:ss"),
                    Size = FormatSize(metadata.Length),
                    FileProperties = fileProps
                }
            };
        }
        static readonly string[] suffixes = { "Bytes", "KB", "MB", "GB", "TB", "PB" };
        /// <summary>
        /// Formats the size.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <returns></returns>
        public static string FormatSize(Int64 bytes)
        {
            int counter = 0;
            decimal number = (decimal)bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }
            return string.Format("{0:n1}{1}", number, suffixes[counter]);
        }


        /// <summary>
        /// Gets the directory recursivly.
        /// </summary>
        /// <param name="directory">The directory.</param>
        /// <returns></returns>
        private Child GetDirectoryRecursivly(DirectoryInfo directory)
        {
            Child obj = new Child();
            foreach (DirectoryInfo d in directory.EnumerateDirectories())
            {
                string sidecarMetadata = File.Exists(Path.Combine(d.FullName, String.Concat(d.Name, ".metadata"))) ? Path.Combine(d.FullName, String.Concat(d.Name, ".metadata")) :
                    File.Exists(Path.Combine(d.FullName, String.Concat(d.Name, ".mdto.xml"))) ? Path.Combine(d.FullName, String.Concat(d.Name, ".mdto.xml")) :
                        File.Exists(Path.Combine(d.FullName, String.Concat(d.Name, ".opex"))) ? Path.Combine(d.FullName, String.Concat(d.Name, ".opex")) : "Metadata bestand niet gevonden.";

                if(sidecarMetadata.Equals("Metadata bestand niet gevonden.") && d.Name == "opex")
                {
                    var opexContainerMetadata = d.GetFiles("opex-*.opex").OrderByDescending(item => item.LastWriteTime).First();
                    if (opexContainerMetadata != null)
                        sidecarMetadata = opexContainerMetadata.FullName;
                }

                string sidecarObject = d.FullName;
                string levelName = GetLevelName(sidecarMetadata);
                string displayName = GetPresentationName(sidecarMetadata, d.Name);

                Child newObj = GetDirectoryRecursivly(d);
                newObj.Key = Guid.NewGuid().ToString();
                newObj.Label = String.Format("{0} - {1}", levelName, displayName);
                newObj.Icon = levelName.Equals("Geen") ? "pi pi-fw pi-times" : levelName.Equals("Archief", StringComparison.InvariantCultureIgnoreCase) ? "pi pi-fw pi-inbox" : "pi pi-fw pi-folder";
                newObj.Data = Base64Encode(sidecarMetadata);
                newObj.Type = File.Exists(sidecarMetadata) ? "url" : null;
                obj.Children.Add(newObj);
            }

            if (directory.Name == SessionId.ToString())
                return obj;

            foreach (FileInfo f in directory.GetFiles())
            {
                if (f.Name.EndsWith(".metadata") || f.Name.EndsWith(".mdto.xml") || f.Name.EndsWith(".opex")) { continue; }

                string sidecarMetadata = File.Exists(String.Concat(f.FullName, ".metadata")) ? String.Concat(f.FullName, ".metadata") :
                    File.Exists(String.Concat(f.FullName, ".bestand.mdto.xml")) ? String.Concat(f.FullName, ".bestand.mdto.xml") :
                        File.Exists(String.Concat(f.FullName, ".opex")) ? String.Concat(f.FullName, ".opex") : "Metadata bestand niet gevonden.";

                string sidecarObject = f.FullName;
                string levelName = GetLevelName(sidecarMetadata);
                string displayName = GetPresentationName(sidecarMetadata, f.Name);

                Child newObj = new Child
                {
                    Key = Guid.NewGuid().ToString(),
                    Label = String.Format("{0} - {1}", levelName, displayName),
                    Icon = levelName.Equals("Geen") ? "pi pi-fw pi-times" : "pi pi-fw pi-file",
                    Data = Base64Encode(sidecarMetadata),
                    Children = null,
                    Type = File.Exists(sidecarMetadata) ? "url" : null
                };
                obj.Children.Add(newObj);
            }
            return obj;
        }

        /// <summary>
        /// Gets the name of the level.
        /// </summary>
        /// <param name="metadata">The metadata.</param>
        /// <returns></returns>
        private String GetLevelName(string metadata)
        {
            string levelName = "Geen";
            bool exists = File.Exists(metadata);
            if (exists)
            {
                try
                {
                    XDocument document = XDocument.Load(metadata);
                    XNamespace ns = document.Root.GetDefaultNamespace();
                    bool isToPX = document.Root.Name.LocalName.Equals("ToPX", StringComparison.InvariantCultureIgnoreCase);
                    bool isMDTO = document.Root.Name.LocalName.Equals("MDTO", StringComparison.InvariantCultureIgnoreCase);
                    bool isOPEX = document.Root.Name.LocalName.Equals("OPEXMetadata", StringComparison.InvariantCultureIgnoreCase);
                    bool isBestand = (document.Root.Elements().First().Name.LocalName == "bestand");
                    if (isOPEX)
                        return levelName = "OPEX";
                    if (isBestand)
                        return levelName = "Bestand";
                    if (isToPX)
                        return levelName = document.Root.Element(ns + "aggregatie").Element(ns + "aggregatieniveau").Value;
                    if (isMDTO)
                        return levelName = document.Root.Element(ns + "informatieobject").Element(ns + "aggregatieniveau").Element(ns + "begripLabel").Value;
                }
                catch { }
            }
            return levelName;
        }

        /// <summary>
        /// Gets the name of the presentation.
        /// </summary>
        /// <param name="metadata">The metadata.</param>
        /// <param name="currentName">Name of the current.</param>
        /// <returns></returns>
        private String GetPresentationName(string metadata, string currentName)
        {
            bool exists = File.Exists(metadata);
            if (exists)
            {
                try
                {
                    XDocument document = XDocument.Load(metadata);
                    XNamespace ns = document.Root.GetDefaultNamespace();
                    bool isToPX = document.Root.Name.LocalName.Equals("ToPX", StringComparison.InvariantCultureIgnoreCase);
                    bool isMDTO = document.Root.Name.LocalName.Equals("MDTO", StringComparison.InvariantCultureIgnoreCase);
                    bool isOPEX = document.Root.Name.LocalName.Equals("OPEXMetadata", StringComparison.InvariantCultureIgnoreCase);
                    bool isBestand = (document.Root.Elements().First().Name.LocalName == "bestand");

                    if (isOPEX)
                        return currentName;
                    if (isBestand)
                        return document.Root.Element(ns + "bestand").Element(ns + "naam").Value;
                    if (isToPX)
                        return currentName = document.Root.Element(ns + "aggregatie").Element(ns + "naam").Value;
                    if (isMDTO)
                        return currentName = document.Root.Element(ns + "informatieobject").Element(ns + "naam").Value;
                }
                catch { }
            }
            return currentName;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.DataRoot = null;
        }

        public class Child
        {
            public Child()
            {
                Children = new List<Child>();
            }
            public string Key { get; set; }
            public string Label { get; set; }
            public string Icon { get; set; }
            public string Data { get; set; }
            public List<Child> Children { get; set; }
            public string Type { get; set; }
        }

        public class TreeRoot
        {
            public TreeRoot()
            {
                Root = new List<Child>();
            }
            public List<Child> Root { get; set; }
        }

        public class ItemContent
        {
            public dynamic Data { get; set; }
        }

        public class ItemProps
        {
            public String Name { get; set; }
            public String Location { get; set; }
            public String Size { get; set; }
            public String CreationDateTime { get; set; }
            public String LastModified { get; set; }
            public ItemFileProps FileProperties { get; set; }
        }

        public class ItemFileProps
        {
            public String Name { get; set; }
            public String Location { get; set; }
            public String Size { get; set; }
            public String CreationDateTime { get; set; }
            public String LastModified { get; set; }
        }
    }
}
