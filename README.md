# OpenTelemetry Error Scenarios

Configure your New Relic API key in the [.env](./.env) file.
Update the New Relic endpoint to the staging OTLP endpoint if you wish.

Run:

```shell
docker compose up --build
```

This program emits telemetry based on the scenarios described in [Telemetry.json](./Telemetry.json).
This digram depicts those scenarios. Each circle represents a span in a transaction (i.e., a single process).
A red circle represents a span where `otel.status_code=ERROR`.

<img width="915" alt="Screenshot 2024-08-23 at 11 43 47 AM" src="https://github.com/user-attachments/assets/e2c5a74c-1eed-438b-b1a4-97e607c37aa1">
