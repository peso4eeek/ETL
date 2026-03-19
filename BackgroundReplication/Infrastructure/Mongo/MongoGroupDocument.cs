using MongoDB.Bson.Serialization.Attributes;

namespace BackgroundReplication.Infrastructure.Mongo;

public class MongoGroupDocument
{
    [BsonId] 
    public int Id { get; set; }
    public required string Name { get; set; }
    public List<MongoStudent> Students { get; set; } = new();
    public List<MongoTeacher> Teachers { get; set; } = new();
}