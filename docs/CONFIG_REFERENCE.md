# Configuration Reference

The application stores persistent configurations in `%APPDATA%\agy-cli-account-swarm\settings.json`.

## Settings Schema

```json
{
  "Profiles": [
    {
      "Id": "guid-uuid-here",
      "Name": "Worker Alpha",
      "CustomHomeDir": "C:\\Users\\username\\.gemini-profiles\\worker-alpha",
      "GeminiApiKey": "",
      "AnthropicApiKey": "",
      "GoogleCloudProject": "",
      "PreferredModel": "claude-3-opus",
      "Tier": "Pro",
      "QuotaLimit": 150000,
      "IsActive": true
    }
  ],
  "AgyCliPath": "agy",
  "PreferredLayout": "Tiled",
  "StayInBackground": true,
  "AudioEffectsEnabled": true,
  "Language": "en",
  "PreferredChartMode": "Bar"
}
```

### Property Definitions
- **`Profiles`**: Array of isolated account definitions.
- **`CustomHomeDir`**: Absolute path mapped to `%USERPROFILE%` for the child process.
- **`PreferredModel`**: Default LLM requested when running sessions (e.g. `claude-3-opus`, `claude-3.5-sonnet`, `gemini-2.5-pro`).
- **`Tier`**: Subscription tier badge (`Basic`, `Plus`, `Pro`, `Ultra`, or `Unverified`).
- **`QuotaLimit`**: Daily token budget limit before sound alert & warning trigger.
- **`Language`**: Application UI localization (`en` or `id`).
- **`PreferredChartMode`**: Coordinate projection model (`Bar`, `Line`, or `Area`).
