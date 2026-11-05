#Requires -Version 5.1
<#
.SYNOPSIS
    Builds RaftDurabilityX10.dll (BepInEx 5 plugin for Raft) without needing the .NET SDK.

.DESCRIPTION
    Uses the C# compiler shipped with the .NET Framework (csc.exe), or Roslyn/MSBuild's
    csc.exe from Visual Studio when available.

.PARAMETER GameDir
    Raft installation folder (the one containing Raft.exe). Auto-detected from the
    Steam library folders when omitted.

.PARAMETER Install
    Copy the built DLL to <GameDir>\BepInEx\plugins\RaftDurabilityX10\ after building.

.EXAMPLE
    .\build.ps1
    .\build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\Raft" -Install
#>
param(
    [string]$GameDir,
    [switch]$Install
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-RaftDir {
    $steam = $null
    try { $steam = (Get-ItemProperty -Path 'HKCU:\Software\Valve\Steam' -ErrorAction Stop).SteamPath } catch { }
    if (-not $steam) {
        try { $steam = (Get-ItemProperty -Path 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam' -ErrorAction Stop).InstallPath } catch { }
    }
    if (-not $steam) { return $null }

    $libraries = New-Object System.Collections.Generic.List[string]
    $libraries.Add(($steam -replace '/', '\'))
    $folders = Join-Path $steam 'steamapps\libraryfolders.vdf'
    if (Test-Path $folders) {
        foreach ($m in [regex]::Matches((Get-Content $folders -Raw), '"path"\s+"([^"]+)"')) {
            $p = $m.Groups[1].Value -replace '\\\\', '\'
            if ($libraries -notcontains $p) { $libraries.Add($p) }
        }
    }

    foreach ($lib in $libraries) {
        $candidate = Join-Path $lib 'steamapps\common\Raft'
        if (Test-Path (Join-Path $candidate 'Raft.exe')) { return $candidate }
    }
    return $null
}

function Find-Csc {
    $candidates = @(
        (Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\Roslyn\csc.exe'),
        (Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\2019\*\MSBuild\Current\Bin\Roslyn\csc.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\Roslyn\csc.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2019\*\MSBuild\Current\Bin\Roslyn\csc.exe'),
        (Join-Path ${env:WINDIR} 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
        (Join-Path ${env:WINDIR} 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
    )
    foreach ($pattern in $candidates) {
        if ($pattern -like '*`**') {
            $hit = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($hit) { return $hit.FullName }
        } elseif (Test-Path $pattern) {
            return $pattern
        }
    }
    throw 'No C# compiler (csc.exe) found.'
}

if (-not $GameDir) { $GameDir = Find-RaftDir }
if (-not $GameDir -or -not (Test-Path (Join-Path $GameDir 'Raft.exe'))) {
    throw 'Raft installation not found. Pass -GameDir "X:\path\to\Raft".'
}

$managed = Join-Path $GameDir 'Raft_Data\Managed'
$bepinex = Join-Path $GameDir 'BepInEx\core'

$references = @(
    (Join-Path $managed 'Assembly-CSharp.dll'),
    (Join-Path $managed 'UnityEngine.dll'),
    (Join-Path $managed 'UnityEngine.CoreModule.dll'),
    (Join-Path $managed 'UnityEngine.UI.dll'),
    (Join-Path $managed 'UnityEngine.UIModule.dll'),
    (Join-Path $managed 'netstandard.dll'),
    (Join-Path $bepinex 'BepInEx.dll'),
    (Join-Path $bepinex '0Harmony.dll')
)

foreach ($ref in $references) {
    if (-not (Test-Path $ref)) { throw "Missing reference assembly: $ref" }
}

$csc = Find-Csc
$sources = Get-ChildItem -Path (Join-Path $root 'src\RaftDurabilityX10') -Filter '*.cs' | ForEach-Object { $_.FullName }

$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$outDll = Join-Path $dist 'RaftDurabilityX10.dll'
if (Test-Path $outDll) { Remove-Item $outDll -Force }

$arguments = @(
    '/nologo',
    '/target:library',
    '/platform:x64',
    '/optimize+',
    ('/out:' + $outDll)
)
foreach ($ref in $references) { $arguments += ('/reference:' + $ref) }
foreach ($src in $sources) { $arguments += $src }

Write-Host "Compiler : $csc"
Write-Host "Raft dir : $GameDir"
Write-Host "Building : $outDll"

& $csc @arguments 2>&1 | ForEach-Object { Write-Host $_ }
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed (exit code $LASTEXITCODE)."
}

$dll = Get-Item $outDll
Write-Host ("OK - {0} ({1:N0} bytes)" -f $dll.FullName, $dll.Length)

if ($Install) {
    $pluginDir = Join-Path $GameDir 'BepInEx\plugins\RaftDurabilityX10'
    New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
    Copy-Item $outDll (Join-Path $pluginDir 'RaftDurabilityX10.dll') -Force
    Write-Host ("Installed to {0}" -f $pluginDir)
}
