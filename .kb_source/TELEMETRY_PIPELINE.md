# Real Telemetry Pipeline

agy-cli-account-swarm never uses fabricated or simulated telemetry data.

## Log Stream Parser
The telemetry engine inspects `history.jsonl` located in the profile's sandbox:

```
<CustomHomeDir>\.gemini\antigravity-cli\history.jsonl
```

### JSON Line Structure:
```json
{
  "display": "Prompt text entered by user",
  "timestamp": 1780240519232,
  "workspace": "D:/Works/Project",
  "conversationId": "a1e267a4-39c9-4b78-8f6b-8b65912d9405"
}
```

## Metrics Extracted:
1. **Timestamp Normalization**: Millisecond UNIX timestamp converted to local DateTime.
2. **Session Aggregation**: Grouped by daily buckets for 7-day trend analysis.
3. **Token Estimation**: Prompt character length normalized against standard byte-pair encoding (average 4 chars / token).
4. **Empty State Guarantee**: If `history.jsonl` contains zero records or does not exist, charts render flat zero baselines and indicate `0 Sessions / 0 Tokens`.
