namespace MqttSecurity.Core.Models;
public sealed class Reservation
{
    public Guid Id { get; }
    public string Username { get; }
    public DateTimeOffset CreatedAt { get; }
    public bool Confirmed { get; set; }
    public Reservation(Guid id, string username, DateTimeOffset createdAt)
    {
        Id = id;
        Username = username;
        CreatedAt = createdAt;
    }
}
