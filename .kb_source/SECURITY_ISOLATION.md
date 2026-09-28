# Security & Process Sandboxing Architecture

## 1. Environment Variable Injection
When launching a swarm process, `UseShellExecute` is explicitly set to `false`. This allows granular environment variable customization without altering global system state:

- `USERPROFILE` -> Redirected to `<CustomHomeDir>`
- `HOME` -> Redirected to `<CustomHomeDir>`
- `APPDATA` -> Redirected to `<CustomHomeDir>\AppData\Roaming`
- `LOCALAPPDATA` -> Redirected to `<CustomHomeDir>\AppData\Local`

## 2. Path Whitespace Prevention
A common failure mode in CLI tools occurs when paths contain trailing whitespace (e.g. `mkdir C:\Users\username \.gemini`). 
The `ProfileManager` rigorously sanitizes all paths with `.Trim()` and replaces invalid separators before invoking `Directory.CreateDirectory`.

## 3. Credential Segregation
Each worker profile stores its own OAuth tokens, cookies, and local database cache within its isolated directory:
- Profile 1 -> `C:\Users\...\.gemini-profiles\profile-1\.gemini`
- Profile 2 -> `C:\Users\...\.gemini-profiles\profile-2\.gemini`

No profile can read, overwrite, or invalidate authentication tokens of another profile.
