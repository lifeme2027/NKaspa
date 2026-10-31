# Offline sample smoke test.
# Runs Mnemonic, Address, Spend (no --broadcast), PsktMultisig, SignMessage, and Json.
# Does not run NodeQuery and does not submit a transaction.
# 离线冒烟：助记词、地址、花费（不带 --broadcast）、PSKT、消息签名、JSON。不跑 NodeQuery，不提交交易。

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dotnet = "dotnet"
if ($env:DOTNET_ROOT) {
    $candidate = Join-Path $env:DOTNET_ROOT "dotnet.exe"
    if (Test-Path -LiteralPath $candidate) {
        $dotnet = $candidate
    }
}

function Invoke-Sample {
    param(
        [Parameter(Mandatory = $true)][string]$Project,
        [string[]]$Arguments = @()
    )

    $path = Join-Path $root $Project
    Write-Host "smoke $Project"
    if ($Arguments.Length -eq 0) {
        & $dotnet run -c Release --project $path
    }
    else {
        & $dotnet run -c Release --project $path -- @Arguments
    }

    if ($LASTEXITCODE -ne 0) {
        throw "smoke failed: $Project ($LASTEXITCODE)"
    }
}

Invoke-Sample "samples\Mnemonic\Mnemonic.csproj"
Invoke-Sample "samples\Mnemonic\Mnemonic.csproj" @(
    "abandon", "abandon", "abandon", "abandon", "abandon", "abandon",
    "abandon", "abandon", "abandon", "abandon", "abandon", "about"
)
Invoke-Sample "samples\Address\Address.csproj"
Invoke-Sample "samples\Spend\Spend.csproj"
Invoke-Sample "samples\PsktMultisig\PsktMultisig.csproj"
Invoke-Sample "samples\SignMessage\SignMessage.csproj" @("NKaspa")
Invoke-Sample "samples\Json\Json.csproj"
Invoke-Sample "samples\Tutorials\Tutorials.csproj"
Write-Host "smoke ok"
