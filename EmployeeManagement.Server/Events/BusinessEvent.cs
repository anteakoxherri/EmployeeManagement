using System.Text.Json.Serialization;

namespace EmployeeManagement.Server.Events;

[JsonConverter(typeof(JsonStringEnumConverter<BusinessEventType>))]
public enum BusinessEventType
{
    UserCreated, UserUpdated, UserDeleted, ProfileUpdated, ProfilePictureUpdated,
    ProjectCreated, ProjectUpdated, ProjectDeleted, EmployeeAddedToProject, EmployeeRemovedFromProject,
    TaskCreated, TaskUpdated, TaskAssigned, TaskCompleted, TaskDeleted
}

// Kontrate e perbashket: vetem identifikues dhe kohe, pa emra, email, password ose permbajtje imazhesh.
// Per UserCreated, Timestamp eshte koha UTC e eventit te regjistrimit; nuk eshte fushe e re ne DB.
public sealed record BusinessEvent(BusinessEventType EventType, string EntityId,
    int? ProjectId = null, int? TaskId = null, string? UserId = null, string? AssignedToUserId = null)
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
