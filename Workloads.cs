using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

public class Workloads
{
    private readonly Workload[] workloads;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly Regex WorkloadRegex = new(@"^.*\..*\.(?<name>.*)\.json$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public Workloads()
    {
        var workloads = new List<Workload>();
        var assembly = Assembly.GetExecutingAssembly();
        var manifestResourceNames = assembly.GetManifestResourceNames().Where(x => x.Contains(".workloads."));
        Debug.Assert(manifestResourceNames.Any());

        foreach (var name in manifestResourceNames)
        {
            var workloadJson = assembly.GetManifestResourceStream(name);
            var match = WorkloadRegex.Match(name);
            Debug.Assert(match != null);
            Debug.Assert(match.Success);

            var workloadName = match.Groups["name"].Value;
            var scenarios = JsonSerializer.Deserialize<Scenario[]>(workloadJson!, JsonSerializerOptions)!;
            workloads.Add(new Workload(workloadName, scenarios));
        }

        this.workloads = workloads.ToArray();
    }

    public Workload this[string name]
    {
        get
        {
            return workloads.First(x => x.WorkloadName == name);
        }
    }
}

public class Workload
{
    public string WorkloadName { get; private set; }
    public Scenario[] Scenarios { get; private set; }

    public Workload(string workloadName, Scenario[] scenarios)
    {
        this.WorkloadName = workloadName;
        this.Scenarios = scenarios;
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
