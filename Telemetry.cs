using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

public class Telemetry
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
    
    public static Scenario[] GetScenarios()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceStream("Telemetry.json");
        return JsonSerializer.Deserialize<Scenario[]>(resource!, JsonSerializerOptions)!;
    }
}

public class Scenario
{
   public Span[] Spans { get; set; } = Array.Empty<Span>();
}

public class Span
{
    private static readonly Status DefaultStatus = new Status { Code = ActivityStatusCode.Unset };

    public int Id { get; set; }
    public int ParentId { get; set; }
    public bool IsRemoteParent { get; set; }
    public string Name { get; set; } = string.Empty;
    public ActivityKind Kind { get; set; }
    public Status Status { get; set; } = DefaultStatus;
    public KeyValuePair<string, object>[] Attributes { get; set; } = Array.Empty<KeyValuePair<string, object>>();
    public IEnumerable<ExceptionEvent> Exceptions { get; set; } = Array.Empty<ExceptionEvent>();
    public IEnumerable<Log> Logs { get; set; } = Array.Empty<Log>();
}

public class Status
{
    public ActivityStatusCode Code { get; set; }
    public string? Description { get; set; }
}

public class ExceptionEvent
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class Log
{
    public string Body { get; set; } = string.Empty;
    public ExceptionEvent? Exception { get; set; }
}
