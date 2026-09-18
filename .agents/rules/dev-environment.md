# Development Environment

Windows PowerShell and the repository root are the canonical execution
environment. Do not require WSL, Bash, `python3`, activation scripts, or global
Python packages.

## Python: `FrameWeb/`

- Dependency metadata: `FrameWeb/pyproject.toml`
- Reproducible resolution: `FrameWeb/uv.lock`

```powershell
uv sync --project FrameWeb --locked --extra dev
uv --directory FrameWeb run --locked --extra dev python -m pytest tests -q
```

Use `uv add --project FrameWeb ...` only for an approved dependency change and
commit the manifest and lockfile together. Ruff, ty, marimo, and poe are not
repository requirements unless the manifest later declares them.

## Angular: `FrameWebforJS/`

- Dependency metadata: `FrameWebforJS/package.json`
- Reproducible resolution: `FrameWebforJS/package-lock.json`

```powershell
npm --prefix FrameWebforJS ci
npm --prefix FrameWebforJS run test -- --watch=false --browsers=ChromeHeadless
npm --prefix FrameWebforJS run build
```

Use scripts declared in `package.json`; do not substitute undeclared global
Angular or TypeScript tools.

## .NET

`FrameWeb.sln` contains the local startup and printing projects and targets
.NET 8 through `tools/FrameWeb.Startup/FrameWeb.Startup.csproj`.

```powershell
dotnet restore FrameWeb.sln
dotnet build FrameWeb.sln
dotnet run --project tools/FrameWeb.Startup
```

`FrameGConverter/FrameGConverter.sln` is separate and is built only when that
component is in scope.

## Agent Infrastructure

```powershell
& .agents/check.ps1
```

Run commands from the root so paths and ownership checks remain consistent.
