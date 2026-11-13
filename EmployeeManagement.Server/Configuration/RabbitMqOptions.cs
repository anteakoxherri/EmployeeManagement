using System.ComponentModel.DataAnnotations;
using RabbitMQ.Client;

namespace EmployeeManagement.Server.Configuration;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";
    public bool Enabled { get; set; } = true;
    public bool ConsumerEnabled { get; set; } = true;
    [Required] public string HostName { get; set; } = "localhost";
    [Range(1, 65535)] public int Port { get; set; } = 5672;
    [Required] public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    [Required] public string QueueName { get; set; } = "employee-management.business-events";
    [Range(1, 100000)] public int BufferCapacity { get; set; } = 1000;
    [Range(1, 60)] public int TimeoutSeconds { get; set; } = 5;
    [Range(1, 60)] public int RetryDelaySeconds { get; set; } = 5;

    public bool HasCredentials => !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password);

    // Kredencialet vijne nga konfigurimi/User Secrets/environment, kurre nga kodi.
    internal ConnectionFactory CreateFactory() => new()
    {
        HostName = HostName, Port = Port, VirtualHost = VirtualHost, UserName = UserName, Password = Password,
        AutomaticRecoveryEnabled = false, RequestedConnectionTimeout = TimeSpan.FromSeconds(TimeoutSeconds),
        ContinuationTimeout = TimeSpan.FromSeconds(TimeoutSeconds)
    };
}
