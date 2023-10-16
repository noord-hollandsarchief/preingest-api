using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Noord.Hollands.Archief.Preingest.WebApi.Utilities
{
    public class SidecarStructureRulesHandler : IDisposable
    {
        public class ExplanationItem
        {
            public String ExplantionText { get; set; }
            public String TargetMetadata { get; set; }
        }

        private List<ExplanationItem> _explantionList = null;

        public SidecarStructureRulesHandler(KeyValuePair<String, List<XDocument>> aggregationDataSet)
        {
            CurrentAggregationDataSet = aggregationDataSet;
            this._explantionList = new List<ExplanationItem>();
        }
        public ExplanationItem[] GetExplanations()
        {
            return this._explantionList.ToArray();
        }
        public bool IsMDTO
        {
            get
            {
                return this.CurrentAggregationDataSet.Key.EndsWith(".mdto.xml", StringComparison.InvariantCultureIgnoreCase);
            }
        }
        public KeyValuePair<String, List<XDocument>> CurrentAggregationDataSet
        {
            get; set;
        }
        public String[] AggregationLevel
        {
            get
            {
                XNamespace ns = CurrentAggregationDataSet.Value.Select(item => item.Root.GetDefaultNamespace()).Distinct().First();
                //TODO TOPX variant
                var aggregationLevel = IsMDTO 
                    ? CurrentAggregationDataSet.Value.Select(item => (item.Root.Elements().First().Name.LocalName == "bestand") ? "Bestand" : item.Root.Element(ns + "informatieobject").Element(ns + "aggregatieniveau").Element(ns + "begripLabel").Value).Reverse() 
                    : CurrentAggregationDataSet.Value.Select(item => (item.Root.Elements().First().Name.LocalName == "bestand") ? "Bestand" : item.Root.Elements().First().Element(ns + "aggregatieniveau").Value).Reverse();

                return aggregationLevel.ToArray();
            }
        }
        public String DisplayAggregationLevel
        {
            get
            {
                return String.Join('|', this.AggregationLevel);
            }
        }
        public Dictionary<String, String> GetLevelWithMetadataLocation()
        {
            Dictionary<String, String> results = new Dictionary<string, string>();

            var parts = this.CurrentAggregationDataSet.Key.Split("/", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            List<String> pathBuilder = new List<string>();
            pathBuilder.Add("/");//root
            parts.ForEach(part =>
            {
                var test = new List<String>(pathBuilder.ToArray());
                test.Add(part);
                test.Add(String.Concat(part, IsMDTO ? ".mdto.xml" : ".metadata"));

                var metadataFile = Path.Join(test.ToArray());
                pathBuilder.Add(part);
                if (!File.Exists(metadataFile))
                    return;

                int index = results.Count;
                if (index < AggregationLevel.Count())
                {
                    string aggregationName = AggregationLevel[index];
                    results.Add(metadataFile, aggregationName);
                }
                else
                {
                    throw new IndexOutOfRangeException(String.Format("Er wordt een aggregatie niveau opgevraagd buiten de lengte van de lijst."));
                }
            });

            return results;
        }
       
        public void Explane()
        {
            try
            {
                var levelKeyDictionary = GetLevelWithMetadataLocation();
                if (this.AggregationLevel.Count() == 0)
                {
                    //geen aggregatie niveau's samengesteld. Controleer of de structuur + metadata correct zijn opgeleverd.
                    this._explantionList.Add(new ExplanationItem
                    {
                        ExplantionText = "Samenstellen van aggregatie niveau's is niet gelukt. Controleer of de structuur + metadata correct zijn opgesteld.",
                        TargetMetadata = String.Join("; ", levelKeyDictionary.Keys.ToArray())
                    });
                }
                if (!this.AggregationLevel.First().Equals("Archief"))
                {
                    //Start niveau moet archief zijn
                    this._explantionList.Add(new ExplanationItem
                    {
                        ExplantionText = "Start niveau mag alleen 'Archief' zijn!",
                        TargetMetadata = levelKeyDictionary.Keys.First()
                    });
                }
                if (!this.AggregationLevel.Last().Equals("Bestand"))
                {
                    //Laagste niveau moet eindigen met een bestand.
                    this._explantionList.Add(new ExplanationItem
                    {
                        ExplantionText = "Laagste niveau mag alleen een bestand zijn!",
                        TargetMetadata = CurrentAggregationDataSet.Key
                    });
                }
                if(this.AggregationLevel.Count(item => item.Equals("Archief")) != 1)
                {
                    //Laagste niveau moet eindigen met een bestand.
                    this._explantionList.Add(new ExplanationItem
                    {
                        ExplantionText = "Archief mag alleen 1 keer voorkomen!",
                        TargetMetadata = String.Join("; ", levelKeyDictionary.Keys.ToArray())
                    });
                }
                if (this.AggregationLevel.Count(item => item.Equals("Bestand")) != 1)
                {
                    //Laagste niveau moet eindigen met een bestand.
                    this._explantionList.Add(new ExplanationItem
                    {
                        ExplantionText = "Bestand mag alleen 1 keer voorkomen!",
                        TargetMetadata = String.Join("; ", levelKeyDictionary.Keys.ToArray())
                    });
                }

                if (IsMDTO)
                {
                    for (int i = 0, l = this.AggregationLevel.Count(); i < l; i++)
                    {
                        string level = this.AggregationLevel[i];
                        string nextLevel = ((i + 1) < this.AggregationLevel.Length) ? this.AggregationLevel[i + 1] : String.Empty;

                        switch (level)
                        {
                            case "Archief":
                                if (!(nextLevel == "Serie" || nextLevel == "Dossier" || nextLevel == "Zaak" || nextLevel == "Archiefstuk"))
                                {
                                    this._explantionList.Add(new ExplanationItem
                                    {
                                        ExplantionText = "Onder 'Archief' niveau alleen: 'Serie' of 'Dossier' of 'Zaak' of 'Archiefstuk'!",
                                        TargetMetadata = ((i + 1) < levelKeyDictionary.Keys.Count) ? levelKeyDictionary.Keys.ToArray()[i + 1] : String.Join("; ", levelKeyDictionary.Keys.ToArray())
                                    });
                                }
                                break;
                            case "Serie":
                                if (!(nextLevel == "Serie" || nextLevel == "Dossier" || nextLevel == "Archiefstuk"))
                                {
                                    this._explantionList.Add(new ExplanationItem
                                    {
                                        ExplantionText = "Onder 'Serie' niveau alleen: 'Serie' of 'Dossier' of 'Archiefstuk'!",
                                        TargetMetadata = ((i + 1) < levelKeyDictionary.Keys.Count) ? levelKeyDictionary.Keys.ToArray()[i + 1] : String.Join("; ", levelKeyDictionary.Keys.ToArray())
                                    });
                                }
                                break;
                            case "Dossier":
                                if (!(nextLevel == "Zaak" || nextLevel == "Archiefstuk"))
                                {
                                    this._explantionList.Add(new ExplanationItem
                                    {
                                        ExplantionText = "Onder 'Dossier' niveau alleen: 'Dossier' of 'Archiefstuk'!",
                                        TargetMetadata = ((i + 1) < levelKeyDictionary.Keys.Count) ? levelKeyDictionary.Keys.ToArray()[i + 1] : String.Join("; ", levelKeyDictionary.Keys.ToArray())
                                    });
                                }
                                break;
                            case "Archiefstuk":
                                if (!(nextLevel == "Bestand"))
                                {
                                    this._explantionList.Add(new ExplanationItem
                                    {
                                        ExplantionText = "Onder 'Archiefstuk' niveau komt bestand'!",
                                        TargetMetadata = ((i + 1) < levelKeyDictionary.Keys.Count) ? levelKeyDictionary.Keys.ToArray()[i + 1] : String.Join("; ", levelKeyDictionary.Keys.ToArray())
                                    });
                                }
                                break;

                            case "Zaak":
                                if (!(nextLevel == "Archiefstuk"))
                                {
                                    this._explantionList.Add(new ExplanationItem
                                    {
                                        ExplantionText = "Onder 'Zaak' niveau aleen: 'Archiefstuk'!",
                                        TargetMetadata = ((i + 1) < levelKeyDictionary.Keys.Count) ? levelKeyDictionary.Keys.ToArray()[i + 1] : String.Join("; ", levelKeyDictionary.Keys.ToArray())
                                    });
                                }
                                break;

                            case "Bestand":
                                if (!String.IsNullOrEmpty(nextLevel))
                                {
                                    this._explantionList.Add(new ExplanationItem
                                    {
                                        ExplantionText = "Onder bestanden mogen geen aggregatie niveau's meer voorkomen. Samenstelling is niet correct. Controlleer de metadata bestanden op inhoud en structuur.",
                                        TargetMetadata = ((i + 1) < levelKeyDictionary.Keys.Count) ? levelKeyDictionary.Keys.ToArray()[i + 1] : String.Join("; ", levelKeyDictionary.Keys.ToArray())
                                    });
                                }
                                break;
                            default:
                                break;
                        }
                    }
                }
                else
                {
                    for (int i = 0, l = this.AggregationLevel.Count(); i < l; i++)
                    {
                        string level = this.AggregationLevel[i];
                        string nextLevel = ((i + 1) < this.AggregationLevel.Length) ? this.AggregationLevel[i + 1] : String.Empty;

                        switch (level)
                        {
                            case "Archief":
                                if (!(nextLevel == "Serie" || nextLevel == "Dossier"))
                                {
                                    this._explantionList.Add(new ExplanationItem
                                    {
                                        ExplantionText = "Onder 'Archief' niveau alleen: 'Serie' of 'Dossier'!",
                                        TargetMetadata = ((i + 1) < levelKeyDictionary.Keys.Count) ? levelKeyDictionary.Keys.ToArray()[i + 1] : String.Join("; ", levelKeyDictionary.Keys.ToArray())
                                    });
                                }
                                break;
                            case "Serie":
                                if (!(nextLevel == "Serie" || nextLevel == "Dossier"))
                                {
                                    this._explantionList.Add(new ExplanationItem
                                    {
                                        ExplantionText = "Onder 'Serie' niveau alleen: 'Serie' of 'Dossier'!",
                                        TargetMetadata = ((i + 1) < levelKeyDictionary.Keys.Count) ? levelKeyDictionary.Keys.ToArray()[i + 1] : String.Join("; ", levelKeyDictionary.Keys.ToArray())
                                    });
                                }
                                break;
                            case "Dossier":
                                if (!(nextLevel == "Dossier" || nextLevel == "Record" || nextLevel == "Bestand"))
                                {
                                    this._explantionList.Add(new ExplanationItem
                                    {
                                        ExplantionText = "Onder 'Dossier' niveau alleen: 'Dossier' of 'Record' of 'Bestand'!",
                                        TargetMetadata = ((i + 1) < levelKeyDictionary.Keys.Count) ? levelKeyDictionary.Keys.ToArray()[i + 1] : String.Join("; ", levelKeyDictionary.Keys.ToArray())
                                    });
                                }
                                break;
                            case "Record":
                                if (!(nextLevel == "Record" || nextLevel == "Bestand"))
                                {
                                    this._explantionList.Add(new ExplanationItem
                                    {
                                        ExplantionText = "Onder 'Dossier' niveau alleen: 'Record' of 'Bestand'!",
                                        TargetMetadata = ((i + 1) < levelKeyDictionary.Keys.Count) ? levelKeyDictionary.Keys.ToArray()[i + 1] : String.Join("; ", levelKeyDictionary.Keys.ToArray())
                                    });
                                }
                                break;
                            case "Bestand":
                                if (!String.IsNullOrEmpty(nextLevel))
                                {
                                    this._explantionList.Add(new ExplanationItem
                                    {
                                        ExplantionText = "Onder bestanden mogen geen aggregatie niveau's meer voorkomen. Samenstelling is niet correct. Controlleer de metadata bestanden op inhoud en structuur.",
                                        TargetMetadata = ((i + 1) < levelKeyDictionary.Keys.Count) ? levelKeyDictionary.Keys.ToArray()[i + 1] : String.Join("; ", levelKeyDictionary.Keys.ToArray())
                                    });
                                }
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            catch(Exception e)
            {
                this._explantionList.Add(new ExplanationItem
                {
                    ExplantionText = String.Format("Zoeken naar een verklaring is fout gelopen! {0} - {1}", e.Message, e.StackTrace),
                    TargetMetadata = CurrentAggregationDataSet.Key
                });
            }
        }

        public void Dispose()
        {
            this._explantionList.Clear();
            this._explantionList = null;
        }
    }
}
