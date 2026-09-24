[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [string]$PackageUrl,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedPublicKeyBase64,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must use stable semantic version format, such as 1.2.0."
}

$expectedUrl = "https://my-1253786342.cos.ap-shanghai.myqcloud.com/releases/v$Version/steady-desk-update-win-x64.zip"
if (-not [string]::Equals($PackageUrl, $expectedUrl, [StringComparison]::Ordinal)) {
    throw "PackageUrl does not match the expected versioned COS path."
}

if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
    throw "Update package does not exist: $PackagePath"
}

$package = Get-Item -LiteralPath $PackagePath
if ($package.Length -le 0 -or $package.Length -gt 256MB) {
    throw "Update package size must be between 1 byte and 256 MiB."
}

$privatePem = $env:STEADYDESK_UPDATE_PRIVATE_KEY_PEM
if ([string]::IsNullOrWhiteSpace($privatePem)) {
    throw "STEADYDESK_UPDATE_PRIVATE_KEY_PEM is not configured."
}

$sha256 = (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
$sizeBytes = [long]$package.Length
$payload = [string]::Join("`n", @(
    "1",
    "SteadyDesk",
    $Version,
    $PackageUrl,
    $sizeBytes.ToString([Globalization.CultureInfo]::InvariantCulture),
    $sha256
))

$rsa = [System.Security.Cryptography.RSA]::Create()
$expectedRsa = [System.Security.Cryptography.RSA]::Create()
try {
    $rsa.ImportFromPem($privatePem)
    $expectedPublicPem = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($ExpectedPublicKeyBase64))
    $expectedRsa.ImportFromPem($expectedPublicPem)
    $signingPublicParameters = $rsa.ExportParameters($false)
    $expectedPublicParameters = $expectedRsa.ExportParameters($false)
    $signingModulus = [Convert]::ToBase64String($signingPublicParameters.Modulus)
    $expectedModulus = [Convert]::ToBase64String($expectedPublicParameters.Modulus)
    $signingExponent = [Convert]::ToBase64String($signingPublicParameters.Exponent)
    $expectedExponent = [Convert]::ToBase64String($expectedPublicParameters.Exponent)
    if (-not [string]::Equals($signingModulus, $expectedModulus, [StringComparison]::Ordinal)
        -or -not [string]::Equals($signingExponent, $expectedExponent, [StringComparison]::Ordinal)) {
        throw "Signing private key does not match UPDATE_PUBLIC_KEY_BASE64."
    }

    $signature = [Convert]::ToBase64String($rsa.SignData(
        [Text.Encoding]::UTF8.GetBytes($payload),
        [Security.Cryptography.HashAlgorithmName]::SHA256,
        [Security.Cryptography.RSASignaturePadding]::Pss
    ))
}
finally {
    $rsa.Dispose()
    $expectedRsa.Dispose()
}

$manifest = [ordered]@{
    SchemaVersion = 1
    Product = "SteadyDesk"
    Version = $Version
    PackageUrl = $PackageUrl
    PackageSizeBytes = $sizeBytes
    Sha256 = $sha256
    Signature = $signature
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$json = ConvertTo-Json -InputObject $manifest -Depth 3
[IO.File]::WriteAllText($OutputPath, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
Write-Host "Signed update manifest created: $OutputPath"
