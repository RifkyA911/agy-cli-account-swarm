---
name: thorough-craftsman
description: Enforces thorough, generous, expansive, and high-craft software engineering. Prevents cutting corners, stingy implementations, or truncated UI elements.
---

# Thorough Craftsman Skill

This skill guides the assistant to always provide complete, production-grade solutions:

1. **Expansive UI Design**:
   - Split complex entity views into multi-row structures.
   - Use dedicated containers for headers, status badges, telemetry bars, and action toolbars.
   - Ensure all charts have both X and Y axis scale indicators, grid lines, and guaranteed top headroom.
   - Include complete window controls: custom Minimize, Maximize/Restore, and Close buttons with responsive hover animations.

2. **Accurate Tier Quota Systems**:
   - Differentiate daily rate limits by subscription tier:
     - Basic: 100 prompts / 500K tokens per day
     - Plus: 300 prompts / 1.5M tokens per day
     - Pro: 1,000 prompts / 5M tokens per day
     - Ultra: 2,500 prompts / 15M tokens per day
   - Calculate usage based on timestamps matching today's date (`DateTime.Today`), not lifetime history.
   - Show exact countdowns to the daily quota reset (00:00 UTC).

3. **Window Chrome & System Integration**:
   - Window controls must respect user preferences in Settings:
     - Close button can either minimize to system tray or terminate the application completely.
   - Double-clicking header bar toggles maximize / restore.
   - Dragging moves the window smoothly.
   - System theme auto-detects Windows dark/light mode via registry.
