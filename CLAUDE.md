# AppName — Antivirus Project

## Language
Always respond in Russian.

## Project Overview
Windows antivirus desktop app (C# + .NET 8 + Avalonia UI).
Architecture: Windows Service (engine) + GUI (Avalonia). Communication via Named Pipes (JSON).
See full spec: `docs/superpowers/specs/2026-06-10-antivirus-design.md`

## Cybersecurity Skills (use when working on these modules)

When implementing or reviewing these components, load the corresponding skill:

| Component | Skill to load |
|---|---|
| `SystemOptimizer` (autorun detection) | `.claude/skills/analyzing-malware-persistence-with-autoruns` |
| `ScanEngine` (IOC scoring, threat confidence) | `.claude/skills/analyzing-indicators-of-compromise` |
| `SystemOptimizer` (registry scanning) | `.claude/skills/analyzing-windows-registry-for-artifacts` |
| `QuarantineManager` (AES encryption) | `.claude/skills/analyzing-ransomware-encryption-mechanisms` |
| `RealTimeGuard` (ransomware detection) | `.claude/skills/analyzing-ransomware-network-indicators` |
| `ProcessMonitor` (execution history) | `.claude/skills/analyzing-prefetch-files-for-execution-history` |
| `ScanEngine` (evasion-aware detection) | `.claude/skills/analyzing-malware-sandbox-evasion-techniques` |
| `RealTimeGuard` (PowerShell monitoring) | `.claude/skills/analyzing-powershell-script-block-logging` |
| `ProcessMonitor` (AmCache artifacts) | `.claude/skills/analyzing-windows-amcache-artifacts` |
| YARA / detection rules | `.claude/skills/building-detection-rules-with-sigma` |

## Architecture Rules
- Service and App are separate .NET projects in one solution
- All engine logic lives in `AppName.Service`, never in `AppName.App`
- GUI communicates with service ONLY via IpcClient/IpcServer (Named Pipes)
- Use MVVM pattern in Avalonia (CommunityToolkit.Mvvm)
- All database access goes through Entity Framework Core + SQLite

## Security Rules
- Quarantine files use AES-256-CBC, unique key per file, CSPRNG-generated
- Never store plaintext sensitive data; use Windows DPAPI for local secrets
- Validate all IPC messages before processing (no deserialization of untrusted types)
- Log security events to Windows Event Log (Application channel)
- Require elevation (UAC) for service installation and registry writes

## Code Style
- C# 12, .NET 8, nullable reference types enabled
- No comments unless WHY is non-obvious
- Interfaces for all engine components (IAntivirusEngine, IRealTimeGuard, etc.)
- Async/await throughout — no blocking calls on UI thread
