namespace BackgroundReplication.Infrastructure.Postgres;

public record FlatGroupRecord{
    public int GroupId { get; set; }
    public string GroupName { get; set; }
    public DateTime? GroupDeletedAt { get; set; }
    
    public int? StudentId { get; set; }
    public string StudentName { get; set; }
    public DateTime? StudentDeletedAt { get; set; }
    
    public int? TeacherId { get; set; }
    public string TeacherName { get; set; }
    public DateTime? TeacherDeletedAt { get; set; }
}