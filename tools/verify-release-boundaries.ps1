[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$forbiddenPaths = @(
    'Assets/EchoVaultAuthoring',
    'Assets/EchoVaultAuthoring.meta',
    'Distribution/EchoVault',
    'tools/echo-vault-m2-consumer',
    'tools/verify-echo-vault-m2.ps1'
)

$forbiddenTokens = @(
    'EchoVault',
    'EchoValut',
    'ECHO_VAULT',
    'echo-vault'
)

$releaseSurfaces = @(
    'Assets/RogueDungeonLab',
    'UpmPackages',
    'ProjectSettings/EditorBuildSettings.asset'
)

$textExtensions = @(
    '.asmdef', '.asset', '.cs', '.json', '.md', '.meta', '.prefab',
    '.ps1', '.toml', '.unity', '.yaml', '.yml'
)

$violations = [System.Collections.Generic.List[string]]::new()
$resolvedRoot = [IO.Path]::GetFullPath($ProjectRoot)

# 검사 가능한 텍스트 파일만 결정적 경로 순서로 반환합니다.
function Get-ReleaseTextFiles {
    param([string]$RelativePath)

    $absolutePath = Join-Path $resolvedRoot $RelativePath
    if (-not (Test-Path -LiteralPath $absolutePath)) {
        return @()
    }

    $item = Get-Item -LiteralPath $absolutePath
    if (-not $item.PSIsContainer) {
        return @($item)
    }

    return @(Get-ChildItem -LiteralPath $absolutePath -Recurse -File |
        Where-Object { $textExtensions -contains $_.Extension.ToLowerInvariant() } |
        Sort-Object FullName)
}

# 한 파일에서 제품 전용 토큰을 찾아 경계 위반 목록에 추가합니다.
function Add-TokenViolations {
    param([IO.FileInfo]$File)

    $relativePath = $File.FullName.Substring($resolvedRoot.Length + 1)
    foreach ($token in $forbiddenTokens) {
        if (Select-String -LiteralPath $File.FullName -SimpleMatch $token -Quiet) {
            $violations.Add("제품 전용 토큰 '$token': $relativePath")
        }
    }
}

foreach ($relativePath in $forbiddenPaths) {
    $absolutePath = Join-Path $resolvedRoot $relativePath
    if (Test-Path -LiteralPath $absolutePath) {
        $violations.Add("제품 전용 경로가 남아 있음: $relativePath")
    }
}

foreach ($surface in $releaseSurfaces) {
    foreach ($file in Get-ReleaseTextFiles -RelativePath $surface) {
        Add-TokenViolations -File $file
    }
}

if ($violations.Count -gt 0) {
    Write-Error ("릴리즈 경계 검사 실패:`n- " + ($violations -join "`n- "))
    exit 1
}

Write-Output '릴리즈 경계 검사 통과: 제품 전용 경로와 토큰이 범용 배포면에 없습니다.'
