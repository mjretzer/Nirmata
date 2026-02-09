# Compatibility wrapper: canonical script moved to `.aos/fixtures/scripts/engine-mvp.ps1`.
#
# This wrapper exists to preserve older documentation/links that reference `scripts/engine-mvp.ps1`.

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$canonical = Join-Path $repoRoot '.aos/fixtures/scripts/engine-mvp.ps1'

if (-not (Test-Path -LiteralPath $canonical)) {
    throw "Canonical engine MVP script not found at '$canonical'."
}

& $canonical @args
exit $LASTEXITCODE
