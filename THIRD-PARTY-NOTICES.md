# Third-Party Components — Licenses

All components used to build and package Reseller System are free for
commercial and non-commercial use. No paid licenses, subscriptions, or
mandatory cloud services are required to build, install, or run the system.

| Component | Used for | License | Cost |
|---|---|---|---|
| [.NET 8 SDK/Runtime](https://dotnet.microsoft.com/) | Server, clients | MIT | Free |
| [PostgreSQL](https://www.postgresql.org/) (EDB Windows binaries) | Database engine | PostgreSQL License (permissive, BSD/MIT-style) | Free |
| [Inno Setup](https://jrsoftware.org/isinfo.php) | Windows installers | Inno Setup License (free, incl. commercial use; source available) | Free |
| [Avalonia UI](https://avaloniaui.net/) | Desktop client (Windows/macOS) | MIT | Free |
| [Serilog](https://serilog.net/) + sinks | Logging | Apache License 2.0 | Free |
| [FluentValidation](https://fluentvalidation.net/) | Validation | Apache License 2.0 | Free |
| [Npgsql](https://www.npgsql.org/) | PostgreSQL driver | PostgreSQL License | Free |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MVVM helpers | MIT | Free |
| [xunit](https://xunit.net/), [FluentAssertions](https://fluentassertions.com/), [NSubstitute](https://nsubstitute.github.io/), [Testcontainers](https://testcontainers.com/) | Testing only, not shipped | Apache 2.0 / MIT | Free |
| Windows Service Control Manager (`sc.exe`), `netsh`, `hdiutil`, `codesign` | Service registration, firewall, macOS packaging | Built into Windows/macOS | Free |

No component in this list requires a purchase, subscription, or account to
build or run Stage 1. If a future stage introduces a paid or
account-gated dependency (e.g. code-signing certificates for
Gatekeeper-notarized macOS builds, or a commercial Windows code-signing
cert to avoid SmartScreen warnings), it will be called out explicitly here
as optional, with a free fallback path documented.
