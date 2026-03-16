param(
    [string] $Root = '',
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [switch] $KeepWorkspace
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function New-TempDirectory([string] $prefix) {
    $path = Join-Path $env:TEMP ($prefix + '-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $path | Out-Null
    return $path
}

function Write-Utf8NoBom([string] $path, [string] $contents) {
    [System.IO.File]::WriteAllText($path, $contents, [System.Text.UTF8Encoding]::new($false))
}

function Get-RelativeFileMap([string] $rootPath) {
    $map = @{}
    $normalizedRoot = (Resolve-Path -LiteralPath $rootPath).Path.TrimEnd('\', '/') + '\'
    $files = Get-ChildItem -LiteralPath $rootPath -Recurse -File
    foreach ($f in $files) {
        $full = (Resolve-Path -LiteralPath $f.FullName).Path
        if ($full.StartsWith($normalizedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            $rel = $full.Substring($normalizedRoot.Length)
        }
        else {
            $rel = $f.Name
        }
        $rel = $rel -replace '\\', '/'
        $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $f.FullName).Hash
        $map[$rel] = $hash
    }
    return $map
}

function Assert-MapsEqual($a, $b, [string] $label) {
    $aKeys = @($a.Keys) | Sort-Object
    $bKeys = @($b.Keys) | Sort-Object
    if ($aKeys.Count -ne $bKeys.Count) {
        throw "$label file lists differ.`nA: $($aKeys -join ', ')`nB: $($bKeys -join ', ')"
    }

    foreach ($k in $aKeys) {
        if (-not $b.ContainsKey($k)) {
            throw "$label file lists differ.`nA: $($aKeys -join ', ')`nB: $($bKeys -join ', ')"
        }
        if ($a[$k] -ne $b[$k]) {
            throw "$label differs for '$k' (A=$($a[$k]) B=$($b[$k]))"
        }
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')

Write-Host "Building AOS ($Configuration)..." -ForegroundColor Cyan
dotnet build (Join-Path $repoRoot 'nirmata.Aos/nirmata.Aos.csproj') -c $Configuration | Out-Host

$aosDll = Join-Path $repoRoot "nirmata.Aos/bin/$Configuration/net10.0/aos.dll"
if (-not (Test-Path -LiteralPath $aosDll)) {
    throw "aos.dll not found at '$aosDll' after build."
}

$workspaceRoot = $Root
$cleanup = $false
if ([string]::IsNullOrWhiteSpace($workspaceRoot)) {
    $workspaceRoot = New-TempDirectory 'nirmata-aos-engine-mvp'
    $cleanup = -not $KeepWorkspace
}

Write-Host "Workspace root: $workspaceRoot" -ForegroundColor Cyan

try {
    dotnet $aosDll init --root $workspaceRoot | Out-Host
    dotnet $aosDll validate schemas --root $workspaceRoot | Out-Host
    dotnet $aosDll validate workspace --root $workspaceRoot | Out-Host

    $planPath = Join-Path $workspaceRoot 'plan.json'
    $planJson =
"{
  ""schemaVersion"": 1,
  ""outputs"": [
    { ""relativePath"": ""b.txt"", ""contentsUtf8"": ""B"" },
    { ""relativePath"": ""a/alpha.txt"", ""contentsUtf8"": ""Alpha\nLine2\n"" },
    { ""relativePath"": ""a/beta.txt"", ""contentsUtf8"": ""Beta"" },
    { ""relativePath"": ""z.txt"", ""contentsUtf8"": ""Z"" }
  ]
}
"
    Write-Utf8NoBom $planPath $planJson

    Push-Location $workspaceRoot
    try {
        $runIdA = (dotnet $aosDll execute-plan --plan $planPath | Select-Object -Last 1).Trim()
        $runIdB = (dotnet $aosDll execute-plan --plan $planPath | Select-Object -Last 1).Trim()
    }
    finally {
        Pop-Location
    }

    if ([string]::IsNullOrWhiteSpace($runIdA) -or [string]::IsNullOrWhiteSpace($runIdB)) {
        throw "Expected run IDs from execute-plan, got empty output."
    }

    $aosRoot = Join-Path $workspaceRoot '.aos'
    $outputsA = Join-Path $aosRoot "evidence/runs/$runIdA/outputs"
    $outputsB = Join-Path $aosRoot "evidence/runs/$runIdB/outputs"
    $logA = Join-Path $aosRoot "evidence/runs/$runIdA/logs/execute-plan.actions.json"
    $logB = Join-Path $aosRoot "evidence/runs/$runIdB/logs/execute-plan.actions.json"

    if (-not (Test-Path -LiteralPath $outputsA)) { throw "Missing outputs at '$outputsA'." }
    if (-not (Test-Path -LiteralPath $outputsB)) { throw "Missing outputs at '$outputsB'." }
    if (-not (Test-Path -LiteralPath $logA)) { throw "Missing actions log at '$logA'." }
    if (-not (Test-Path -LiteralPath $logB)) { throw "Missing actions log at '$logB'." }

    $mapA = Get-RelativeFileMap $outputsA
    $mapB = Get-RelativeFileMap $outputsB
    Assert-MapsEqual $mapA $mapB 'execute-plan outputs'

    $hashLogA = (Get-FileHash -Algorithm SHA256 -LiteralPath $logA).Hash
    $hashLogB = (Get-FileHash -Algorithm SHA256 -LiteralPath $logB).Hash
    if ($hashLogA -ne $hashLogB) {
        throw "execute-plan actions log differs across identical runs (A=$hashLogA B=$hashLogB)"
    }

    dotnet $aosDll validate workspace --root $workspaceRoot | Out-Host

    Write-Host "OK: Engine MVP flow + repro check passed." -ForegroundColor Green
    Write-Host "Run A: $runIdA" -ForegroundColor Green
    Write-Host "Run B: $runIdB" -ForegroundColor Green
    Write-Host "Evidence root: $(Join-Path $aosRoot 'evidence/runs')" -ForegroundColor Green
}
finally {
    if ($cleanup -and (Test-Path -LiteralPath $workspaceRoot)) {
        Remove-Item -Recurse -Force -LiteralPath $workspaceRoot
    }
}

