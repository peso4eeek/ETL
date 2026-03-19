using BackgroundReplication.Infrastructure.Mongo;
using BackgroundReplication.Infrastructure.Postgres;
using Dapper;
using MongoDB.Driver;
using Npgsql;

namespace BackgroundReplication;

public class Worker(ILogger<Worker> logger, NpgsqlDataSource pgDataSource, IMongoClient mongoClient, IConfiguration config) : BackgroundService
{
    private DateTime _lastSyncTime = DateTime.MinValue;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await WaitForDatabaseAsync(stoppingToken);
        logger.LogInformation("ETL Worker запущен.");
        var mongoDatabase = mongoClient.GetDatabase("university");
        var intervalMinutes = config.GetValue<int>("IntervalMinutes", 5);
        var mongoCollection = mongoDatabase.GetCollection<MongoGroupDocument>("groups");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var syncStartTime =  DateTime.UtcNow;
                logger.LogInformation("Запуск репликации");
                await using var connection = await pgDataSource.OpenConnectionAsync(stoppingToken);
                var sql = @"SELECT 
                            g.id AS GroupId, g.name AS GroupName, g.deleted_at AS GroupDeletedAt,
                            s.id AS StudentId, s.name AS StudentName, s.deleted_at AS StudentDeletedAt,
                            t.id AS TeacherId, t.name AS TeacherName, t.deleted_at AS TeacherDeletedAt
                        FROM groups g
                        LEFT JOIN students s ON g.id = s.group_id
                        LEFT JOIN group_teachers gt ON g.id = gt.group_id
                        LEFT JOIN teachers t ON gt.teacher_id = t.id
                        WHERE (g.updated_at >= @LastSync OR g.deleted_at >= @LastSync)
                           OR (s.updated_at >= @LastSync OR s.deleted_at >= @LastSync)
                           OR (t.updated_at >= @LastSync OR t.deleted_at >= @LastSync)";
                var flatData = (await connection.QueryAsync<FlatGroupRecord>(sql, new { LastSync = _lastSyncTime })).ToList();
                if (flatData.Any())
                {
                    var groupedData = flatData.GroupBy(r => new { r.GroupId, r.GroupName, r.GroupDeletedAt }).ToList();
                    var bulkOperations = new List<WriteModel<MongoGroupDocument>>();

                    foreach (var group in groupedData)
                    {
                        if (group.Key.GroupDeletedAt != null)
                        {
                            bulkOperations.Add(new DeleteOneModel<MongoGroupDocument>(
                                Builders<MongoGroupDocument>.Filter.Eq(x => x.Id, group.Key.GroupId)
                            ));
                            continue;
                        }
                        var students = group
                            .Where(r => r.StudentId != null && r.StudentDeletedAt == null)
                            .Select(r => new MongoStudent { Id = r.StudentId.Value, Name = r.StudentName })
                            .DistinctBy(s => s.Id) 
                            .ToList();
                        var teachers = group
                            .Where(r => r.TeacherId != null && r.TeacherDeletedAt == null)
                            .Select(r => new MongoTeacher { Id = r.TeacherId.Value, Name = r.TeacherName })
                            .DistinctBy(t => t.Id)
                            .ToList();
                        var mongoDoc = new MongoGroupDocument
                        {
                            Id = group.Key.GroupId,
                            Name = group.Key.GroupName,
                            Students = students,
                            Teachers = teachers
                        };
                        var upsertModel = new ReplaceOneModel<MongoGroupDocument>(
                            Builders<MongoGroupDocument>.Filter.Eq(x => x.Id, mongoDoc.Id),
                            mongoDoc
                        ) { IsUpsert = true };

                        bulkOperations.Add(upsertModel);
                        
                    }
                    
                    if (bulkOperations.Any())
                    {
                        var result = await mongoCollection.BulkWriteAsync(bulkOperations, cancellationToken: stoppingToken);
                        logger.LogInformation("Обработано групп: {count}. Вставлено/обновлено: {upserts}, Удалено: {deleted}", 
                            bulkOperations.Count, result.ModifiedCount + result.Upserts.Count, result.DeletedCount);
                    }
                }
                else
                {
                    logger.LogError("Нет новых данных для реплекации");
                }

                _lastSyncTime = syncStartTime;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Ошибка в процессе синхронизации");
            }
            
            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }
    
    
    private async Task WaitForDatabaseAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Анализ состояния... Ожидаю окончания инициализации PostgreSQL");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await pgDataSource.OpenConnectionAsync(stoppingToken);
                
                logger.LogInformation("Postgres is healthy");
                return; 
            }
            catch (NpgsqlException)
            {
                logger.LogInformation("waiting for a response from postgres\n");
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
