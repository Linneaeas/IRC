using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Server;

public class DatabaseHandler
{
    private static IMongoCollection<BsonDocument>? usersCollection;
    private static IMongoCollection<BsonDocument>? messagesCollection;

    public static void Initialize()
    {
        MongoClient mongoClient = new MongoClient("mongodb://localhost:27017");
        IMongoDatabase database = mongoClient.GetDatabase("ChatApp");
        usersCollection = database.GetCollection<BsonDocument>("users");
        messagesCollection = database.GetCollection<BsonDocument>("messages");
    }

    public static bool AuthenticateUser(string username, string password)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("username", username),
            Builders<BsonDocument>.Filter.Eq("password", password)
        );
        var user = usersCollection.Find(filter).FirstOrDefault();
        return user != null;
    }

    public static void InsertUser(string username, string password)
    {
        var document = new BsonDocument
            {
                { "username", username },
                { "password", password }
            };
        usersCollection?.InsertOne(document);
        Console.WriteLine($"User {username} inserted into MongoDB.");
    }

    public static bool IsUsernameExists(string username)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("username", username);
        var existingUser = usersCollection?.Find(filter).FirstOrDefault();
        return existingUser != null;
    }

    public static void InsertMessage(string fromUsername, List<string> toUsernames, string chatMessage)
    {
        var document = new BsonDocument
    {
        { "from", fromUsername },
        { "to", new BsonArray(toUsernames) },
        { "chatMessage", chatMessage }
    };
        messagesCollection?.InsertOne(document);
        Console.WriteLine($"Message {chatMessage} from {fromUsername} to {string.Join(",", toUsernames)} inserted into MongoDB.");
    }
}

