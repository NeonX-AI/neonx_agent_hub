[CmdletBinding()]
param(
    [string]$CertificatePath,
    [string]$CertificatePassword,
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"
$platformRoot = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $platformRoot "..\..")).Path
$source = Join-Path $platformRoot "src\Program.cs"
$manifest = Join-Path $platformRoot "src\app.manifest"
$versionsFile = Join-Path $repoRoot "config\versions.env"
$logo = Join-Path $repoRoot "assets\images\logo\logo-light.png"
$openClawLogo = Join-Path $repoRoot "assets\images\logo\openclaw.png"
$obj = Join-Path $platformRoot "obj"
$generatedVersions = Join-Path $obj "ComponentVersions.cs"
$icon = Join-Path $obj "neonx.ico"
$dist = Join-Path $repoRoot "dist\windows"
$output = Join-Path $dist "NeonX-Agent-Hub.exe"
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"

function Import-VersionEnvironment {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) { throw "Version configuration not found: $Path" }

    $values = @{}
    foreach ($rawLine in Get-Content -LiteralPath $Path) {
        $line = $rawLine.Trim()
        if (-not $line -or $line.StartsWith("#")) { continue }
        if ($line -notmatch '^([A-Z][A-Z0-9_]*)=(.*)$') { throw "Invalid version configuration line: $rawLine" }

        $name = $Matches[1]
        $value = $Matches[2].Trim()
        if (-not $value) { throw "Version configuration value is empty: $name" }
        $values[$name] = $value
        [Environment]::SetEnvironmentVariable($name, $value, [EnvironmentVariableTarget]::Process)
    }

    $required = @(
        "NEONX_HUB_VERSION",
        "NEONX_OPENCLAW_VERSION",
        "NEONX_CODEX_PLUGIN_VERSION",
        "NEONX_NODEJS_VERSION",
        "NEONX_NODEJS_X64_SHA256",
        "NEONX_NODEJS_ARM64_SHA256"
    )
    foreach ($name in $required) {
        if (-not $values.ContainsKey($name)) { throw "Missing version configuration value: $name" }
    }
    if ($values.NEONX_HUB_VERSION -notmatch '^\d+\.\d+\.\d+$') { throw "NEONX_HUB_VERSION must contain three numeric parts." }
    if ($values.NEONX_OPENCLAW_VERSION -notmatch '^\d+\.\d+\.\d+$') { throw "NEONX_OPENCLAW_VERSION must contain three numeric parts." }
    if ($values.NEONX_CODEX_PLUGIN_VERSION -notmatch '^\d+\.\d+\.\d+$') { throw "NEONX_CODEX_PLUGIN_VERSION must contain three numeric parts." }
    if ($values.NEONX_NODEJS_VERSION -notmatch '^\d+\.\d+\.\d+$') { throw "NEONX_NODEJS_VERSION must contain three numeric parts." }
    foreach ($name in @("NEONX_NODEJS_X64_SHA256", "NEONX_NODEJS_ARM64_SHA256")) {
        if ($values[$name] -notmatch '^[A-Fa-f0-9]{64}$') { throw "$name must be a 64-character SHA-256 value." }
    }

    return $values
}

function New-ComponentVersionsSource {
    param([hashtable]$Versions, [string]$DestinationPath)

    $hub = $Versions.NEONX_HUB_VERSION
    $openClaw = $Versions.NEONX_OPENCLAW_VERSION
    $codexPlugin = $Versions.NEONX_CODEX_PLUGIN_VERSION
    $nodeJs = $Versions.NEONX_NODEJS_VERSION
    $nodeX64Hash = $Versions.NEONX_NODEJS_X64_SHA256.ToUpperInvariant()
    $nodeArm64Hash = $Versions.NEONX_NODEJS_ARM64_SHA256.ToUpperInvariant()
    $assemblyVersion = $hub + ".0"

    $content = @"
namespace NeonX.OpenClawInstaller
{
    internal static class ComponentVersions
    {
        internal const string Hub = "$hub";
        internal const string OpenClaw = "$openClaw";
        internal const string CodexPlugin = "$codexPlugin";
        internal const string NodeJs = "$nodeJs";
        internal const string NodeJsDistribution = "v$nodeJs";
        internal const string NodeX64Sha256 = "$nodeX64Hash";
        internal const string NodeArm64Sha256 = "$nodeArm64Hash";
        internal const string AssemblyVersion = "$assemblyVersion";
    }
}
"@
    Set-Content -LiteralPath $DestinationPath -Value $content -Encoding UTF8
}

function New-NeonXIcon {
    param([string]$SourcePath, [string]$DestinationPath)

    Add-Type -AssemblyName System.Drawing
    $sourceImage = [Drawing.Bitmap]::FromFile($SourcePath)
    $sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
    $images = @()
    try {
        foreach ($size in $sizes) {
            $canvas = New-Object Drawing.Bitmap $size, $size, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $graphics = [Drawing.Graphics]::FromImage($canvas)
            $png = New-Object IO.MemoryStream
            try {
                $graphics.Clear([Drawing.Color]::Transparent)
                $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $destination = New-Object Drawing.Rectangle 0, 0, $size, $size
                $graphics.DrawImage($sourceImage, $destination, 0, 0, $sourceImage.Width, $sourceImage.Height, [Drawing.GraphicsUnit]::Pixel)
                $canvas.Save($png, [Drawing.Imaging.ImageFormat]::Png)
                $images += ,$png.ToArray()
            } finally {
                $png.Dispose()
                $graphics.Dispose()
                $canvas.Dispose()
            }
        }

        $writer = New-Object IO.BinaryWriter([IO.File]::Create($DestinationPath))
        try {
            $writer.Write([UInt16]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]$sizes.Count)
            $offset = 6 + (16 * $sizes.Count)
            for ($index = 0; $index -lt $sizes.Count; $index++) {
                $size = $sizes[$index]
                $data = $images[$index]
                $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
                $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
                $writer.Write([byte]0)
                $writer.Write([byte]0)
                $writer.Write([UInt16]1)
                $writer.Write([UInt16]32)
                $writer.Write([UInt32]$data.Length)
                $writer.Write([UInt32]$offset)
                $offset += $data.Length
            }
            foreach ($data in $images) { $writer.Write($data) }
        } finally { $writer.Dispose() }
    } finally {
        $sourceImage.Dispose()
    }
}

if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path -LiteralPath $compiler)) { throw "The .NET Framework 4.x C# compiler was not found." }
if (-not (Test-Path -LiteralPath $logo)) { throw "Logo not found: $logo" }
if (-not (Test-Path -LiteralPath $openClawLogo)) { throw "OpenClaw logo not found: $openClawLogo" }

New-Item -ItemType Directory -Force -Path $obj, $dist | Out-Null
$versions = Import-VersionEnvironment -Path $versionsFile
New-ComponentVersionsSource -Versions $versions -DestinationPath $generatedVersions
New-NeonXIcon -SourcePath $logo -DestinationPath $icon
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }

Write-Host "Building $output ..." -ForegroundColor Cyan
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /win32manifest:"$manifest" `
    /win32icon:"$icon" /reference:System.dll /reference:System.Core.dll `
    /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    /resource:"$logo",NeonXLogo /resource:"$openClawLogo",OpenClawLogo `
    /out:"$output" "$source" "$generatedVersions"
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $output)) { throw "Build failed: $LASTEXITCODE" }

if ($CertificatePath) {
    $signTool = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if (-not $signTool) { throw "signtool.exe was not found in PATH." }
    $arguments = @("sign", "/fd", "SHA256", "/tr", $TimestampUrl, "/td", "SHA256", "/f", $CertificatePath)
    if ($CertificatePassword) { $arguments += @("/p", $CertificatePassword) }
    $arguments += $output
    & $signTool.Source @arguments
    if ($LASTEXITCODE -ne 0) { throw "Code signing failed: $LASTEXITCODE" }
}

$file = Get-Item -LiteralPath $output
$hash = Get-FileHash -LiteralPath $output -Algorithm SHA256
Write-Host "Build completed successfully" -ForegroundColor Green
Write-Host "File: $output"
Write-Host "Size: $($file.Length) bytes"
Write-Host "SHA256: $($hash.Hash)"
