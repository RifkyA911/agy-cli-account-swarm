# AGY Swarm Assistant Guidelines: Generous Engineering & Rigorous Quality Standard

## Core Principle: Uncompromising Craft & Generosity
1. **Never Be Stingy with Features**:
   - Deliver rich, expansive, and fully realized UI/UX. Do not settle for bare-minimum or truncated implementations.
   - When designing cards and lists, provide multi-row responsive layouts with adequate breathing room, clear typography, and visual hierarchy. Never cram competing elements into a single overflowing row.
   - When building charts, always include full axes (X and Y labels, ticks, and dotted guidelines), generous ceiling headroom (+35%), interactive hover cards, and zoom/pan controls.
   - When building custom windows, always provide complete window chrome (minimize, maximize/restore, close, drag-to-move, and double-click to toggle maximize).

2. **Authentic, Dynamic Telemetry**:
   - Zero dummy or synthetic fake data. Always parse authentic local storage and timestamps.
   - Daily quotas must be tier-aware (Basic = 100 prompts/day, Plus = 300, Pro = 1,000, Ultra = 2,500) and calculated strictly for the current day's activity, displaying exact reset countdowns (00:00 UTC).
   - Accurately isolate telemetry per account sandbox.

3. **Complete End-to-End Verification**:
   - Always check if background processes are locking files before publishing.
   - Always run `dotnet publish -c Release -o publish` in addition to `dotnet build` so the user's running directory is always 100% up-to-date.
   - Check and verify zero warnings, zero errors.

## Aturan Investigasi Kemampuan CLI
- **Wajib Verifikasi Mandiri**: Sebelum menyatakan bahwa `agy` "tidak bisa" melakukan sesuatu, asisten WAJIB mengecek langsung melalui `agy --help` atau menguji perintahnya di shell terminal. Jangan berasumsi atau menebak kapabilitas CLI tanpa bukti empiris.

## Fakta agy CLI (terverifikasi)
- **Tanggal Verifikasi**: 28 September 2026
- **Versi agy**: 1.2.12
- **Perintah Persis**:
  - Format JSON: `agy -p "/usage" --output-format json`
  - Format Teks: `agy -p "/usage"`
- **Karakteristik Eksekusi**:
  - `num_turns`: 0 (tidak memulai percakapan/agent turn)
  - `usage`: 0 input/output/thinking/cache tokens (tidak mengonsumsi kuota token sama sekali)
  - `conversation_id`: string kosong (tidak mencemari riwayat percakapan)
  - Durasi respon: ±5-8 detik
- **Contoh Struktur JSON Asli**:
```json
{
  "conversation_id": "",
  "status": "SUCCESS",
  "response": "Gemini Models\tWeekly Limit Remaining\t58%\t2026-10-02T02:01:21Z\nGemini Models\tFive Hour Limit Remaining\t96%\t2026-09-28T13:12:17Z\nClaude and GPT models\tWeekly Limit Remaining\t39%\t2026-10-02T01:07:40Z\nClaude and GPT models\tFive Hour Limit Remaining\t89%\t2026-09-28T13:11:39Z\n",
  "duration_seconds": 0,
  "num_turns": 0,
  "usage": {
    "input_tokens": 0,
    "output_tokens": 0,
    "thinking_tokens": 0,
    "cache_read_tokens": 0,
    "total_tokens": 0
  },
  "command": {
    "name": "usage",
    "data": {
      "description": "Within each group, models share a weekly limit and a 5-hour limit. Quota is consumed proportionally to the cost of the tokens. Thus, limits will last longer with shorter tasks or using more cost-effective models. The 5-hour limit smooths out aggregate demand to fairly distribute global capacity across all users, while your weekly limit is tied directly to your individual tier.",
      "groups": [
        {
          "name": "Gemini Models",
          "description": "Models within this group: Gemini Flash, Gemini Pro",
          "buckets": [
            {
              "id": "gemini-weekly",
              "name": "Weekly Limit Remaining",
              "description": "You have used some of your weekly limit, it will fully refresh in 3 days, 13 hours.",
              "window": "weekly",
              "remaining_fraction": 0.5702829957008362,
              "reset_time": "2026-10-02T02:01:21Z"
            },
            {
              "id": "gemini-5h",
              "name": "Five Hour Limit Remaining",
              "description": "You have used some of your 5-hour limit, it will fully refresh in 48 minutes.",
              "window": "5h",
              "remaining_fraction": 0.9590467214584351,
              "reset_time": "2026-09-28T13:12:17Z"
            }
          ]
        },
        {
          "name": "Claude and GPT models",
          "description": "Models within this group: Claude Opus, Claude Sonnet, GPT-OSS",
          "buckets": [
            {
              "id": "3p-weekly",
              "name": "Weekly Limit Remaining",
              "description": "You have used some of your weekly limit, it will fully refresh in 3 days, 12 hours.",
              "window": "weekly",
              "remaining_fraction": 0.38522493839263916,
              "reset_time": "2026-10-02T01:07:40Z"
            },
            {
              "id": "3p-5h",
              "name": "Five Hour Limit Remaining",
              "description": "You have used some of your 5-hour limit, it will fully refresh in 47 minutes.",
              "window": "5h",
              "remaining_fraction": 0.885555624961853,
              "reset_time": "2026-09-28T13:11:39Z"
            }
          ]
        }
      ]
    }
  }
}
```
- **Catatan & Kompatibilitas**:
  - Format output JSON dan skema bucket ini dapat berubah antar versi rilis `agy` di masa mendatang.
  - Implementasi parser harus defensif (null-safe, memeriksa hierarki `command.data.groups`, dan fallback jika format berubah).
