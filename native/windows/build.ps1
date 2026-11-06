$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceDir = Join-Path $PSScriptRoot 'src'
$outDir = Join-Path $PSScriptRoot 'bin'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $framework 'csc.exe'
if (-not (Test-Path $csc)) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
    $csc = Join-Path $framework 'csc.exe'
}
if (-not (Test-Path $csc)) { throw 'The .NET Framework C# compiler (csc.exe) was not found.' }

$wpf = Join-Path $framework 'WPF'
$references = @(
    (Join-Path $wpf 'PresentationFramework.dll'),
    (Join-Path $wpf 'PresentationCore.dll'),
    (Join-Path $wpf 'WindowsBase.dll'),
    (Join-Path $framework 'System.Xaml.dll'),
    (Join-Path $framework 'System.Web.Extensions.dll'),
    (Join-Path $framework 'System.Drawing.dll'),
    (Join-Path $framework 'System.Windows.Forms.dll'),
    (Join-Path $framework 'System.Core.dll')
)
foreach ($reference in $references) {
    if (-not (Test-Path $reference)) { throw "Missing reference assembly: $reference" }
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$sources = Get-ChildItem -Path $sourceDir -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.FullName }
if ($sources.Count -eq 0) { throw 'No C# sources found.' }

$arguments = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/debug-',
    '/codepage:65001',
    ('/out:' + (Join-Path $outDir 'DSHNotchPet.exe')),
    ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest'))
)
$icon = Join-Path $PSScriptRoot 'app.ico'
if (Test-Path $icon) { $arguments += ('/win32icon:' + $icon) }
foreach ($reference in $references) { $arguments += ('/r:' + $reference) }
$arguments += $sources

& $csc $arguments
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
Write-Host "Built $(Join-Path $outDir 'DSHNotchPet.exe')"
