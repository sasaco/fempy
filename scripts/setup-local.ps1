param([switch]$ForceDependencies)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$repoRoot = Split-Path -Parent $PSScriptRoot
$toolsRoot = Join-Path $repoRoot 'tools/local-tools'
$frontendRoot = Join-Path $repoRoot 'FrameWebforJS'
$localRoot = Join-Path $repoRoot '.local'
New-Item -ItemType Directory -Force -Path $localRoot | Out-Null

function Invoke-Checked {
    param([string]$Executable, [string[]]$Arguments)
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Executable failed (exit $LASTEXITCODE). See the output above."
    }
}

$uvCommand = Get-Command uv -ErrorAction SilentlyContinue
$uvExecutable = if ($uvCommand) { $uvCommand.Source } else { Join-Path $env:USERPROFILE '.local/bin/uv.exe' }
if (-not (Test-Path -LiteralPath $uvExecutable)) {
    throw 'uv was not found. Install uv and restart Visual Studio. See the root README.md.'
}

# Keep the Angular 15 toolchain local; do not change the system Node installation.
$nodeExecutable = Join-Path $toolsRoot 'node_modules/node/bin/node.exe'
$npmScript = Join-Path $toolsRoot 'node_modules/npm/bin/npm-cli.js'
if (-not (Test-Path -LiteralPath $nodeExecutable) -or -not (Test-Path -LiteralPath $npmScript)) {
    $bootstrapNpm = Get-Command npm.cmd -ErrorAction SilentlyContinue
    if (-not $bootstrapNpm) { throw 'Node.js/npm was not found. Install Node.js and restart Visual Studio.' }
    Write-Host '[setup] Installing repository-local Node 18.20.8 and npm 9.9.4...'
    Invoke-Checked $bootstrapNpm.Source @('ci', '--prefix', $toolsRoot, '--no-audit', '--no-fund')
}
$env:PATH = (Split-Path -Parent $nodeExecutable) + ';' + $env:PATH
$env:NG_CLI_ANALYTICS = 'false'

Push-Location (Join-Path $repoRoot 'FrameWeb')
try {
    Write-Host '[setup] Restoring the locked Python environment...'
    Invoke-Checked $uvExecutable @('sync', '--locked', '--extra', 'dev', '--python', '3.12')
} finally { Pop-Location }

$template = Join-Path $frontendRoot 'src/environments/environment.local.example.ts'
foreach ($name in @('environment.ts', 'environment.local.ts')) {
    $target = Join-Path $frontendRoot "src/environments/$name"
    if (-not (Test-Path -LiteralPath $target)) {
        Copy-Item -LiteralPath $template -Destination $target
        Write-Host "[setup] Created $name (existing settings are preserved)."
    }
}

$dependencyFiles = @('package.json', 'paramquery-9.0.1/package.json', 'rxfire-6.0.5/package.json')
$fingerprint = ($dependencyFiles | ForEach-Object {
    (Get-FileHash -LiteralPath (Join-Path $frontendRoot $_) -Algorithm SHA256).Hash
}) -join ':'
$stamp = Join-Path $localRoot 'frontend-dependencies.sha256'
$previous = if (Test-Path -LiteralPath $stamp) { (Get-Content -LiteralPath $stamp -Raw).Trim() } else { '' }
if ($ForceDependencies -or $previous -ne $fingerprint -or -not (Test-Path -LiteralPath (Join-Path $frontendRoot 'node_modules/@angular/cli/bin/ng.js'))) {
    Push-Location $frontendRoot
    try {
        Write-Host '[setup] Installing frontend dependencies...'
        Invoke-Checked $nodeExecutable @($npmScript, 'install', '--no-audit', '--no-fund')
        Set-Content -LiteralPath $stamp -Value $fingerprint -Encoding ASCII
    } finally { Pop-Location }
}
Write-Host '[setup] Ready.'
