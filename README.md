# Pre-ingest API

Een .NET (Core) gebaseerde REST API service met het doel om diverse controle & validatie acties uit te voeren om de kwaliteit van de aangeleverde collecties te beoordelen.

## Controllers
De preingest REST API bestaat uit 6 contollers. Elk controller heeft één of meerdere acties om uit te voeren. De controllers zijn:

- Preingest: Acties voor controlen, valideren en classificeren
- Output: Raadplegen informatie en eigenschappen van de collecties 
- Service: Automatisch werkschema opzetten, starten en/of annuleren
- Status: Raadplegen en muteren van taken bij het uitvoeren een werkschema
- Opex: Voorbewerking en omzetting t.b.v. ingest + raadplegen van de S3 bucket 
- ToPX naar MDTO: Acties t.b.v omzetting van ToPX naar MDTO

### Preingest
- CollectionChecksumCalculation: berekenen van een checksum waarde volgens een algoritme. Algoritme MD5, SHA1, SHA-256 en SHA512 wordt standaard via .NET uitgerekend. Voor SHA-224 en SHA-384 wordt de calculatie gedaan met behulp van [een onderliggende service](https://github.com/noord-hollandsarchief/preingest-mdto-utilities).

- Unpack: uitpakken van een collectie. Een collectie is in een TAR formaat opgeleverd. De collecties dienen opgeslagen te zijn in `data` map.

- VirusScan: een uitgepakte collectie scannen voor virus en/of malware. De actie maakt gebruikt van [een onderliggende service](https://hub.docker.com/r/clamav/clamav). 

- Naming: naamgeving van mappen en bestanden binnen een collectie controleren op bijzonder karakters, ongewenste combinaties en de maximale lengte van een naam.

- Sidecar: structuur van een collectie controlleren op de constructie (volgens de sidecar principe), de opbouw van aggregatie niveau's bij ToPX en MDTO, mappen zonder bestanden en de uniciteit van de aangeleverde objecten.

- Profiling: Voor het classificeren van mappen en bestanden binnen een collectie is een profiel nodig. Nadat een profiel is aangemaakt kunnen de acties Exporting en Reporting gestart worden. Het aanmaken van een profiel wordt gedaan m.b.v. [een onderliggende service](https://github.com/noord-hollandsarchief/preingest-droid).

- Exporting: De resultaten bij het classificeren van mappen en bestanden binnen een collectie opslaan als een CSV bestand. Het exporteren van de resultaten wordt gedaan m.b.v. [een onderliggende service](https://github.com/noord-hollandsarchief/preingest-droid).

- Reporting: De resultaten bij het classificeren van mappen en bestanden binnen een collectie opslaan als een PDF bestand. Het opslaan van de resultaten wordt gedaan m.b.v. [een onderliggende service](https://github.com/noord-hollandsarchief/preingest-droid)

- SignatureUpdate: Interne classificatie lijst van DROID bijwerken. Zie [DROID](https://www.nationalarchives.gov.uk/information-management/manage-information/preserving-digital-records/droid/) voor meer informatie.

- Greenlist: Bestanden binnen een collectie vergelijken met een voorkeurslijst. Voorkeurslijst is een overzicht met bestand extensies en formaten die NHA een primaire voorkeur om te ingesten. De actie wordt gedaan m.b.v. [een onderliggende service](https://github.com/noord-hollandsarchief/preingest-mdto-utilities).

- Encoding: ToPX of MDTO metadata bestanden controleren op encoding en byte order mark.

- ValidateMetadata: ToPX of MDTO metadata bestanden valideren volgens de XSD schema's en controleren op business regels volgens de NHA specificaties bijv. beperking gebruik, openbaarheid en auteurswet. De actie wordt (deels) gedaan m.b.v. [een onderliggende service](https://github.com/noord-hollandsarchief/preingest-xslweb).

- CreateExcel: De preingest resultaten van alle uitgevoerde acties ophalen, converteren en opslaan als een MS Excel bestand.

- PutSettings: Instellingen opslaan van de tool.

- PreWashMetadata: Mogelijkheid om ToPX of MDTO metadata bestanden bij te werken d.m.v. XSLT transformatie. Hiervoor moet wel XSLT bestanden toegevoegd worden met specifieke transformatie. De actie wordt gedaan m.b.v. [een onderliggende service](https://github.com/noord-hollandsarchief/preingest-xslweb).

- IndexMetadataFiles: Alle elementen en waarde van ToPX of MDTO metadata bestanden binnen een collectie extraheren en opslaan in een MS Excel bestand.

- DetectPasswordProtection: Het achterhalen van wachtwoorden binnen MS Office en PDF bestanden. De actie wordt gedaan m.b.v. [een onderliggende service](https://github.com/noord-hollandsarchief/preingest-mdto-utilities). 

- UpdateWithPronom: ToPX of MDTO metadata bestanden van het type 'bestand' bijwerken met informatie uit de classificatie resultaten. Deze actie vereist een resultaat van 'Exporting'. 

- ValidateBinaries: Binaire bestanden binnen een collectie controleren en vergelijken met de classificatie resultaten. Deze actie vereist een resultaat van 'Exporting'.

### Output
- GetCollections: De eigenschappen van alle collecties retourneren t.b.v front-end weergave.
- GetCollection: De eigenschappen van een collectie retourneren t.b.v. front-end weergave.
- GetJson: JSON resultaten retourneren van een uitgevoerd preingest actie.
- GetReport: Indien bestaat/aanwezig, rapportage bestand ophalen. Uiteraard nadat een 'Reporting' actie is uitgevoerd.
- GetStylesheetList: Ophalen van een lijst transformatie bestanden.
- GetSchemaList: Ophalen van een lijst XSD schema bestanden.
- GetCollectionStructure: Mappen en bestanden structuur van een collectie ophalen.
- GetCollectionItem: Ophalen van een binaire bestand.
- GetCollectionItemProps: Ophalen van een metadata bestand.

### Service
- StartPlan: Starten van een samengestelde werkschema. Een werkschema bevat de gekozen actie(s). Starten mag meerdere keren uitgevoerd worden. Voorgaande werkschema wordt dan overschreven.
- CancelPlan: Annuleren van een samengestelde werkschema.

### Status
- GetAction: De eigenschappen ophalen van een actie. 
- GetActions: De eigenschappen ophalen van alle acties.
- AddProcessAction: Een actie toevoegen.
- UpdateProcessAction: Een actie bijewerken.
- AddStartState: Bij een actie een 'start' status meegeven.
- AddCompletedState: Bij een actie een 'voltooid' status meegeven.
- AddFailedState: Bij een actie een 'mislukte' status meegeven.
- ResetSession: Bij een actie de status legen.
- RemoveSession: Bij een actie alle voorgaande sessie informatie legen.
- SendNotification: Een notificatie sturen via SignalR.
- AddState: Een sessie informatie van een actie registreren.
- DeleteSession: Een sessie informatie van een actie legen.

### Opex
- BuildOpex: Een collectie voorbereiden en omzetten naar OPEX constructie t.b.v ingest naar Preservica.
- ShowBucket: Inhoud van S3 bucket opvragen. De actie wordt gedaan m.b.v. [een onderliggende service](https://github.com/noord-hollandsarchief/preingest-mdto-utilities). 
- ClearBucket: Inhoud van de S3 bucket legen. De actie wordt gedaan m.b.v. [een onderliggende service](https://github.com/noord-hollandsarchief/preingest-mdto-utilities). 
- Upload2Bucket: Collectie uploaden naar de S3 bucket. De actie wordt gedaan m.b.v. [een onderliggende service](https://github.com/noord-hollandsarchief/preingest-mdto-utilities). 
- RunChecksum: Checksum waarden controleren in de metadata bestanden. De actie wordt gedaan m.b.v. [een onderliggende service](https://github.com/noord-hollandsarchief/preingest-mdto-utilities). 
- Polish: Na de conversie naar OPEX, alle OPEX bestanden bijwerken d.m.v. XSLT transformatie. 

### ToPX naar MDTO
- Convert: Converten van ToPX metadata bestanden naar MDTO metadata bestanden.
- UpdatePronum: Na conversie, de MDTO metadata bestanden van type 'bestand' voorzien van PRONUM gegevens uit de classificatie resultaten (vereist de actie 'Exporting')
- UpdateFixity: Na de conversie, de MDTO metadata bestanden van type 'bestand' voorzien van fixity/checksum waarde.
- UpdateRelationshipReferences: Na de conversie, de MDTO metadata bestanden voorzien van bovenliggende en onderliggende relaties.

## Status van collectie/actie

Bij het verwerken van een werkschema voor een collectie kunnen de volgende statussen voorkomen:

- `Running` als een actie aan het verwerken is, anders:
- `Failed` als een actie heeft gefaald, anders:
- `Error` als een actie een fout bevat, anders:
- `Success` als een actie (en ook alle overige acties) een succes bevat, anders:
- `New` als een actie of werkschema niet is gestart.

Het opslaan van de configuratie wordt ook als een actie gezien. Deze krijgt ook een succes in dien het opslaan is gelukt.

## OpenAPI (Swagger)
Bij het starten van de preingest API kan de health status opgevraagd wordne via <http://[servernaam]:[poort]/api/preingest/check>. Een OpenAPI specificatie is beschikbaar via <http://[servernaam]:[poort]/swagger/v1/swagger.json>. Swagger UI is beschikbaar via http://[servernaam]:[poort]/swagger.

## Local database
Voor het automatisch verwerken van een werkschema is een database toegepast om de voortgang te bewaren. Dit is een SQLite database en de database bestanden worde lokaal opgeslagen op een werkmap. De opslag locatie is te vinden in de appsettings.json. Deze kan bijv. overgeschreven worden via docker commandline via environments of docker-compose opstart bestand.

A single-file SQLite database is used to keep track of the current processes. This only holds temporary data and will be
re-created on startup if needed. So, in case of failures:

- Stop the pre-ingest (Docker) processes
- Remove the database file (see `DBFOLDER` below)
- Remove all session folders from the data folder (see `DATAFOLDER` below)
- Restart the pre-ingest for archives that still need processing

The database [should NOT be stored on a network share](https://sqlite.org/forum/forumpost/33f1a3a91d?t=h). And to avoid
database errors you may want to exclude its working folder from any virus scanning as well.

## Websocket (SignalR)
De preingest tool heeft ook de mogelijkheid om real-time informatie terug te koppelen d.m.v. SignalR websocket. Voorbeelden met connectie naar SignalR websocket zijn te vinden map `wwwroot`. De [workerservice](https://github.com/noord-hollandsarchief/preingest-workerservice) maakt ook gebruik van SignalR websocket.

## Docker
De preingest REST API wordt als een Docker image gecompileerd. Om de image te kunnen gebruiken is een Docker omgeving nodig met ondersteuning voor Linux. Voor Windows besturingsystemen is Hyper-V nodig of WSL2.

De preingest REST API vereist enkele map verwijzingen tijden opstarten van de image. De mappen zijn `/data` (verwijzing naar de map met alle collecties) en `/db` (verwijzing naar de opslag locatie voor de database).

Indien de image wordt gestart d.m.v. docker-compose, maak gebruik van een .env bestand 
    
    ```env
    DATAFOLDER=/path/to/data-folder
    # The database MUST be stored in a folder on the local machine, not on a network share
    DBFOLDER=/local/path/to/database-folder
    SIPCREATORFOLDER=/path/to/sip-creator-installation-folder
    TOMCATLOGFOLDER=/path/to/tomcat-log-folder
    TRANSFERAGENTTESTFOLDER=/path/to/transfer-agent-test-folder
    TRANSFERAGENTPRODFOLDER=/path/to/transfer-agent-production-folder
    XSLWEBPREWASHFOLDER=/path/to/prewash-xml-stylesheets-folder
    ```
    
## Enkele known issues en troubleshooting
- An action never completes: if somehow the orchestrating API misses out on the completed signal of a delegated action,
  then that action may stay in its running state forever (and the frontend will just increase the elapsed time). As a
  quick fix, remove the results of the very file from the database and file system (in the frontend: select it in the
  overview page to see the options) and restart all processing of the file. If the same problem keeps occurring then
  please contact us.
  
- `clamav exited with code 137` and `/bootstrap.sh: line 35: 15 Killed clamd`: increase the memory for the Docker host.

- `SQLite Error 14: 'unable to open database file'`

  - ensure the database file is NOT stored on a network share
  - exclude the database file (default: `$DBFOLDER/preingest.db`) from virus scanning

- `Access to the path '...' is denied` and zero-byte files are created on a CIFS/SMB network share: ensure to [include
   `nobrl` in the driver options](https://github.com/dotnet/runtime/issues/42790#issuecomment-817758887).

## Trademarks
Preservica™ is een handelsmerk van [Preservica Ltd](https://preservica.com/). Noord-Hollands Archief is niet aangesloten aan Preservica™. 



  

