[CmdletBinding()]
param(
    [string]$CertificatePath,
    [string]$CertificatePassword,
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"
$windowsBuild = Join-Path $PSScriptRoot "platforms\windows\build.ps1"
if (-not $IsMacOS -and -not $IsLinux) {
    & $windowsBuild @PSBoundParameters
    exit $LASTEXITCODE
}
throw "A build pipeline for this platform has not been implemented yet."
