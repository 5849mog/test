[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $env:USERPROFILE "SteadyDesk-UpdateKeys")
)

$ErrorActionPreference = "Stop"

$privateKeyPath = Join-Path $OutputDirectory "update-signing-private.pem"
$publicKeyPath = Join-Path $OutputDirectory "update-signing-public.pem"
$publicKeyBase64Path = Join-Path $OutputDirectory "update-signing-public-base64.txt"

if (Test-Path $privateKeyPath) {
    throw "Private key already exists at '$privateKeyPath'. Choose another output directory to create a separate keypair."
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$rsa = [System.Security.Cryptography.RSA]::Create(3072)
try {
    $privatePem = $rsa.ExportRSAPrivateKeyPem()
    $publicPem = $rsa.ExportSubjectPublicKeyInfoPem()
    $publicBase64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($publicPem))
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)

    [IO.File]::WriteAllText($privateKeyPath, $privatePem + [Environment]::NewLine, $utf8NoBom)
    [IO.File]::WriteAllText($publicKeyPath, $publicPem + [Environment]::NewLine, $utf8NoBom)
    [IO.File]::WriteAllText($publicKeyBase64Path, $publicBase64, $utf8NoBom)
}
finally {
    $rsa.Dispose()
}

Write-Host "更新签名密钥已生成。"
Write-Host "公钥 PEM: $publicKeyPath"
Write-Host "公钥 Base64（用于仓库变量）: $publicKeyBase64Path"
Write-Host "私钥已保存到: $privateKeyPath"
Write-Host "请勿把私钥放入仓库或发送到聊天中；由仓库账户监护人按 README 的说明配置为 GitHub Secret。"
