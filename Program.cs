using MongoDB.Driver;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Linq;

namespace CompetitiveRatingsUpdater
{
    class Program
    {
        private static MongoClientSettings settings = new MongoClientSettings();
        private const string MongoConnectionString = "Mongo Server Link";
        private const string DatabaseName = "The Database Name";
        private const string CollectionName = "The Collection Name";
        private const string XmlFileName = "RankData.xml";
        private static XmlDocument XmlFile = new XmlDocument();
        private static MongoClient client = new MongoClient();
        private static IMongoDatabase database;
        private static IMongoCollection<BsonDocument> _playerCollection;

        static void Main(string[] args)
        {

            //Connect with mongo
            MongoSetup();

            if (File.Exists(Directory.GetCurrentDirectory() + $@"/{XmlFileName}"))
            {
                StartWatchingFile();

                //Attempt to add new players into the thing or update information as needed
                InsertPlayers();

                Console.ReadLine();
                
            }
            else
            {
                Console.WriteLine($"{XmlFileName} does not exist!");
                Console.WriteLine("Attempting to make a new one...");

                XmlNode root = XmlFile.CreateElement("RankData");
                XmlNode player = XmlFile.CreateElement("Player");
                XmlFile.AppendChild(root);
                root.AppendChild(player);
                XmlFile.Save(Directory.GetCurrentDirectory() + $@"/{XmlFileName}");

                Console.WriteLine("File created!");
                Console.WriteLine("Attempting to add Player Rank Data to the XML from the database...");

                ExtractPlayers();
                Console.WriteLine();

                StartWatchingFile();

                Console.ReadLine();
                
            }
        }

        private static void MongoSetup()
        {
            Console.WriteLine("Attempting to connect to MongoDB");
            settings = MongoClientSettings.FromConnectionString(MongoConnectionString);
            client = new MongoClient(settings);
            database = client.GetDatabase(DatabaseName);
            _playerCollection = database.GetCollection<BsonDocument>(CollectionName);
            Console.WriteLine("Connection established!");
        }

        public static void InsertPlayers()
        {
            Console.WriteLine("Generating Player Data....");
            List<PlayerObject> playerDocuments = GenerateDocuments();
            List<BsonDocument> bsonDocuments = new List<BsonDocument>();
            Console.WriteLine();


            if (playerDocuments.Count == 0)
            {
                Console.WriteLine("There's no player data in here lmao");
                return;
            }

            Console.WriteLine("Comparing Player Data with Mongo Database");
            foreach (PlayerObject player in playerDocuments)
            {
                FilterDefinitionBuilder<BsonDocument> builder = Builders<BsonDocument>.Filter;
                FilterDefinition<BsonDocument> filter = builder.And(builder.Eq("name", player.username), builder.Eq("id", player.id));
                List<BsonDocument> result = _playerCollection.Find(filter).ToList();

                if (result.Count >= 2)
                {
                    Console.WriteLine($"ALERT: Multiple {player.username} #{player.id} exist in in the database!");
                    Console.WriteLine("This should never happen! Please check the database because that's really weird!!");
                }
                else if (result.Count == 1)
                {
                    if (result[0].GetValue("rank").ToString() != player.rank)
                    {
                        Console.WriteLine($"Updating Player Information for {player.username} #{player.id}...");
                        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update.Set("rank", player.rank).Set("rankDeviation", player.rankDeviation).Set("volatility", player.volatility);
                        _playerCollection.UpdateOne(filter, update);
                        Console.WriteLine("Updated!");
                    }
                    else
                    {
                        Console.WriteLine($"{player.username} #{player.id} already exists in the database");
                    }
                }
                else
                {
                    Console.WriteLine($"Player {player.username} #{player.id} will be inserted into the database...");
                    bsonDocuments.Add(new BsonDocument
                    {
                        { "name", player.username },
                        { "id", player.id },
                        { "rank", player.rank },
                        { "rankDeviation", player.rankDeviation },
                        { "volatility", player.volatility },
                    });
                }
            }

            if (bsonDocuments.Count > 0)
            {
                Console.WriteLine("Attempting to Insert new players into the database...");
                _playerCollection.InsertMany(bsonDocuments);
                Console.WriteLine("Completed Insert Task!");
            }
            else
                Console.WriteLine("There are no new players to add to the database!");
        }

        private static void ExtractPlayers()
        {
            Console.WriteLine("Retrieving Player Data...");
            List<BsonDocument> bsonDocuments = _playerCollection.Find(FilterDefinition<BsonDocument>.Empty).ToList();

            if (bsonDocuments.Count > 0)
            {
                foreach (BsonDocument bson in bsonDocuments)
                {
                    XmlNode root = XmlFile.SelectSingleNode("RankData");
                    XmlNode playerNode = XmlFile.CreateElement("Player");
                    XmlAttribute name = XmlFile.CreateAttribute("name");
                    name.Value = bson.GetValue("name").ToString();
                    XmlAttribute id = XmlFile.CreateAttribute("id");
                    id.Value = bson.GetValue("id").ToString();
                    XmlElement rank = XmlFile.CreateElement("Rank");
                    XmlElement rankDeviation = XmlFile.CreateElement("RankDeviation");
                    XmlElement volatility = XmlFile.CreateElement("Volatility");
                    root.AppendChild(playerNode);
                    playerNode.Attributes.Append(name);
                    playerNode.Attributes.Append(id);
                    playerNode.AppendChild(rank);
                    rank.InnerText = bson.GetValue("rank").ToString();
                    playerNode.AppendChild(rankDeviation);
                    rankDeviation.InnerText = bson.GetValue("rankDeviation").ToString();
                    playerNode.AppendChild(volatility);
                    volatility.InnerText = bson.GetValue("volatility").ToString();
                    ClearConsoleLine();
                    Console.Write($"Player {bson.GetValue("name")} loaded... ");
                }
                ClearConsoleLine();
                Console.WriteLine("All players from database loaded!");
            }
            else
            {
                Console.WriteLine("Oh there's nothing in the database.");
                Console.WriteLine("lmao");
            }
        }

        private static void StartWatchingFile()
        {
            //Watch that file!!!! Watch it!!!
            FileSystemWatcher watcher = new FileSystemWatcher(AppDomain.CurrentDomain.BaseDirectory);
            watcher.NotifyFilter = NotifyFilters.LastWrite
                                | NotifyFilters.LastAccess
                                | NotifyFilters.FileName;
            watcher.Filter = XmlFileName;
            watcher.Changed += OnChanged;
            watcher.Error += OnError;

            watcher.EnableRaisingEvents = true;
        }

        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                return;
            }
            Console.WriteLine();
            Console.WriteLine("File changed!");
            //If the file changed it must have something different in it! Run the function!
            InsertPlayers();
        }

        private static void OnError(object sender, ErrorEventArgs e)
        {
            PrintException(e.GetException());
        }

        private static void PrintException(Exception ex)
        {
            if (ex != null)
            {
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine("Stacktrace");
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine();
                PrintException(ex.InnerException);
            }
        }

        private static void ClearConsoleLine()
        {
            //Console.SetCursorPosition(0, Console.CursorTop - 1);
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }

        private static List<PlayerObject> GenerateDocuments()
        {
            //Generate documents based on XML document
            List<PlayerObject> playerDatas = new List<PlayerObject>();

            Console.WriteLine("Attempting to read XML...");
            //Read XML doc here, this will be used to add to the playerDatas List
            using (StreamReader reader = new StreamReader(XmlFileName))
            {
                string body = reader.ReadToEnd();

                XmlDocument XmlDoc = new XmlDocument();
                XmlDoc.LoadXml(body);
                Console.WriteLine("XML read! Attempting to load players...");

                XmlNodeList nodes = XmlDoc.SelectNodes("//Player");
                foreach (XmlNode node in nodes)
                {
                    if (node.Attributes.Count > 1)
                    {
                        PlayerObject newPlayer = new PlayerObject()
                        {
                            username = node.Attributes[0].Value,
                            id = node.Attributes[1].Value,
                            rank = node.FirstChild.InnerText,
                            rankDeviation = node.ChildNodes[1].InnerText,
                            volatility = node.ChildNodes[2].InnerText,
                        };
                        ClearConsoleLine();
                        Console.Write($"Player {node.Attributes[0].Value} #{node.Attributes[1].Value} loaded...");
                        playerDatas.Add(newPlayer);
                    }
                }
            }
            ClearConsoleLine();
            Console.WriteLine("All players loaded!");
            Console.WriteLine("Generation complete!");
            return playerDatas;
        }
    }

    public class PlayerObject
    {
        public string username { get; set; }
        public string id { get; set; }
        public string rank { get; set; }
        public string rankDeviation { get; set; }
        public string volatility { get; set; }
    }
}
