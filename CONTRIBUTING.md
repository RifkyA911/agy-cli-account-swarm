# Contributing to agy-cli-account-swarm

Thank you for your interest in contributing! This project is open source under the MIT License.

## Prerequisites
- Windows 10/11 x64
- .NET 9.0 SDK
- Visual Studio 2022 (v17.12+) or VS Code with C# DevKit
- Google Antigravity CLI (`agy`) installed and in PATH

## Development Workflow
1. Fork the repository on GitHub.
2. Clone your fork locally:
   ```powershell
   git clone https://github.com/YOUR_USERNAME/agy-cli-account-swarm.git
   cd agy-cli-account-swarm
   ```
3. Restore and compile:
   ```powershell
   dotnet restore
   dotnet build
   ```
4. Run locally:
   ```powershell
   dotnet run
   ```

## Coding Conventions
- Use standard C# 13 and MVVM patterns (`CommunityToolkit.Mvvm`).
- Ensure no warnings during `dotnet build`.
- Maintain clean Separation of Concerns: keep Views clean of business logic.
- Follow `.editorconfig` style guidelines.

## Submitting Pull Requests
- Branch out from `master` with a descriptive branch name (`feat/your-feature` or `fix/your-bug`).
- Include screenshots or short clips for UI changes.
- Submit PR referencing any related issues.
