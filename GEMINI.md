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
