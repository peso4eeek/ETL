using BackgroundReplication;
using MongoDB.Driver;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
var pgConnectionString = builder.Configuration.GetConnectionString("Postgres");
var mongoConnectionString = builder.Configuration.GetConnectionString("Mongo");
if (string.IsNullOrEmpty(pgConnectionString) || string.IsNullOrEmpty(mongoConnectionString))
{
    throw new Exception("Не заданы строки подключения!");
}

builder.Services.AddNpgsqlDataSource(pgConnectionString);

builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoConnectionString));
var host = builder.Build();
host.Run();