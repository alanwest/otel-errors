var apiKey = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");
if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("YOUR_API_KEY_HERE"))
{
    throw new Exception("Set your API key in the .env file");
}

var workloads = new Workloads();

var services = new ServiceInstance[]
{
    new ServiceInstance(workloads["Errors"], "otel-errors"),
};

var tasks = new List<Task>();

foreach (var service in services)
{
    tasks.Add(service.Run());
}

await Task.WhenAll(tasks);
