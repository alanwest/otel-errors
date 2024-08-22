using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

public class OpenTelemetryProvider : IDisposable
{
    private const string instrumentationScopeName = "my.instrumentation.scope";

    private TracerProvider tracerProvider;
    private MeterProvider meterProvider;
    private readonly ILoggerFactory loggerFactory;
    private readonly ActivitySource activitySource;
    private readonly Meter meter;
    private readonly Histogram<double> httpServerRequestDuration;
    private readonly Histogram<double> httpClientRequestDuration;
    private readonly ILogger logger;

    public OpenTelemetryProvider(string serviceName)
    {
        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureResource(builder =>
            {
                builder.AddService(serviceName);
            })
            .AddSource(instrumentationScopeName)
            .AddOtlpExporter()
            .Build();

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(builder =>
            {
                builder.AddService(serviceName);
            })
            .AddMeter(instrumentationScopeName)
            .AddOtlpExporter()
            .Build();

        this.loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(logging =>
            {
                logging
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
                    .AddOtlpExporter();
            });
        });

        this.activitySource = new ActivitySource(instrumentationScopeName);
        this.meter = new Meter(instrumentationScopeName);
        this.httpServerRequestDuration = this.meter.CreateHistogram<double>("http.server.request.duration", "s", "Duration of HTTP server requests.");
        this.httpClientRequestDuration = this.meter.CreateHistogram<double>("http.client.request.duration", "s", "Duration of HTTP client requests.");
        this.logger = loggerFactory.CreateLogger<Program>();
    }

    public ActivitySource Tracer => this.activitySource;
    public ILogger Logger => this.logger;

    public void RecordHttpServerRequestDuration(TimeSpan duration, KeyValuePair<string, object>[]? attributes)
    {
        var tags = attributes != null
            ? GenerateTagList(attributes, "http.request.method", "http.route", "http.response.status_code")
            : default;
        httpServerRequestDuration.Record(duration.TotalSeconds, tags);
    }

    internal void RecordHttpClientRequestDuration(TimeSpan duration, KeyValuePair<string, object>[]? attributes)
    {
        var tags = attributes != null
            ? GenerateTagList(attributes, "http.request.method", "http.response.status_code", "server.address")
            : default;
        httpClientRequestDuration.Record(duration.TotalSeconds, tags);
    }

    public void Dispose()
    {
        this.activitySource.Dispose();
        this.meter.Dispose();
        this.tracerProvider.Dispose();
        this.meterProvider.Dispose();
        this.loggerFactory.Dispose();
    }

    private TagList GenerateTagList(KeyValuePair<string, object>[] attributes, params string[] keys)
    {
        var result = new TagList();
        foreach (var key in keys)
        {
            AddAttribute(ref result, key, attributes?.FirstOrDefault(x => x.Key == key).Value);
        }
        return result;
    }

    private void AddAttribute(ref TagList tags, string key, object? value)
    {
        if (value != null)
        {
            tags.Add(new KeyValuePair<string, object?>(key, value));
        }
    }
}
