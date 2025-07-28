using System.Diagnostics;
using Microsoft.Extensions.Logging;

public class ServiceInstance
{
    private static readonly ActivityContext UnsampledParentContext = new(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None, null, true);
    private static OpenTelemetryProvider remoteOpenTelemetryProvider = new OpenTelemetryProvider("downstream-service", "instrumentation.scope.remote");

    private OpenTelemetryProvider openTelemetryProvider;
    private readonly Workload workload;

    public ServiceInstance(Workload workload, string serviceName, IEnumerable<KeyValuePair<string, object>>? additionalResourceAttributes = null)
    {
        this.openTelemetryProvider = new OpenTelemetryProvider(serviceName, serviceName + ".instrumentation.scope", additionalResourceAttributes);
        this.workload = workload;
    }

    public async Task Run()
    {
        await Task.Run(() => {
            for (;;)
            {
                foreach (var scenario in this.workload.Scenarios)
                {
                    var spans = scenario.Spans.ToList();
                    var initialSpans = spans.Where(x => x.ParentId == 0 || !scenario.Spans.Any(y => y.Id == x.ParentId)).ToArray();
                    foreach (var span in initialSpans)
                    {
                        StartSpan(span, default, spans, 0);
                    }
                }
                Thread.Sleep(2000);
            }
        });
    }

    private double StartSpan(Span span, ActivityContext parentContext, List<Span> otherSpans, double delay)
    {
        if (span.IsRemoteParent)
        {
            parentContext = parentContext != default
                ? new ActivityContext(parentContext.TraceId, parentContext.SpanId, parentContext.TraceFlags, parentContext.TraceState, true)
                : new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, null, true);
        }

        var provider = span.IsRemoteParent ? remoteOpenTelemetryProvider : this.openTelemetryProvider;
        var tracer = provider.Tracer;
        var logger = provider.Logger;

        using var activity = tracer.StartActivity(span.Name, span.Kind, parentContext, null, null, DateTime.UtcNow.AddMilliseconds(delay));

        activity?.SetStatus(span.Status.Code, span.Status.Description);

        foreach (var attribute in span.Attributes)
        {
            activity?.SetTag(attribute.Key, attribute.Value);
        }

        foreach (var exception in span.Exceptions)
        {
            activity?.AddEvent(new ActivityEvent("exception", default, new ActivityTagsCollection(
                new Dictionary<string, object>
                {
                    { "exception.type", exception.Type },
                    { "exception.message", exception.Message },
                    { "exception.stacktrace", new StackTrace()},
                }!)));
        }

        foreach (var log in span.Logs)
        {
            if (log.Exception != null)
            {
                logger?.LogError(new Exception(log.Exception.Message), log.Exception.Message);
            }
            else
            {
                logger?.LogInformation(log.Body);
            }
        }

        otherSpans.Remove(span);
        var childSpans = otherSpans.Where(x => x.ParentId == span.Id).ToArray();
        double syncChildDuration = 0;
        double asyncChildDuration = 0;
        double nextChildDelay = TimeSpan.FromMilliseconds(Random.Shared.Next(10, 30)).TotalMilliseconds;
        foreach (var childSpan in childSpans)
        {
            var childDuration = StartSpan(childSpan, activity?.Context ?? UnsampledParentContext, otherSpans, nextChildDelay);
            if (childSpan.Async)
            {
                asyncChildDuration += childDuration;
            }
            else
            {
                syncChildDuration += childDuration;
            }

            nextChildDelay = TimeSpan.FromMilliseconds(Random.Shared.Next(10, 30)).TotalMilliseconds;
        }

        var exclusiveDuration = span.Duration.HasValue
            ? span.Duration.Value
            : TimeSpan.FromMilliseconds(Random.Shared.Next(10, 50)).TotalMilliseconds;
        var totalDuration = syncChildDuration + exclusiveDuration;
        activity?.SetEndTime(activity.StartTimeUtc.AddMilliseconds(totalDuration));

        if (!span.ExportSpan && activity != null)
        {
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        }

        if (activity?.Kind == ActivityKind.Server)
        {
            provider.RecordHttpServerRequestDuration(TimeSpan.FromMilliseconds(totalDuration + asyncChildDuration), span.Attributes);
        }

        if (activity?.Kind == ActivityKind.Client && !activity.TagObjects.Any(x => x.Key == "db.system" || x.Key == "db.system.name"))
        {
            provider.RecordHttpClientRequestDuration(TimeSpan.FromMilliseconds(totalDuration), span.Attributes);
        }

        if (activity != null && activity.TagObjects.Any(x => x.Key == "db.system.name"))
        {
            provider.RecordDbOperationDuration(TimeSpan.FromMilliseconds(totalDuration), span.Attributes);
        }

        return totalDuration;
    }
}
