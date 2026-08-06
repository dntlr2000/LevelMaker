param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe',
    [string]$Workspace = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

# Unity를 숨김 batchmode로 실행하고 실패 종료 코드를 즉시 전달합니다.
function Invoke-Unity {
    param(
        [string[]]$Arguments,
        [string]$Label,
        [int]$TimeoutMilliseconds = 900000
    )

    $process = Start-Process -FilePath $UnityPath -ArgumentList $Arguments -PassThru -WindowStyle Hidden
    try {
        if (-not $process.WaitForExit($TimeoutMilliseconds)) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            throw "$Label exceeded the $TimeoutMilliseconds ms timeout."
        }
        $process.Refresh()
        $exitCode = $process.ExitCode
        if ($exitCode -ne 0) {
            throw "$Label failed with Unity exit code $exitCode."
        }
    }
    finally {
        $process.Dispose()
    }
}

# 외부 package 버전만 가진 최소 Unity 6000.5 소비 프로젝트 골격을 생성합니다.
function New-ConsumerProject {
    param(
        [string]$ProjectPath,
        [hashtable]$Dependencies
    )

    New-Item -ItemType Directory -Path (Join-Path $ProjectPath 'Assets') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $ProjectPath 'Packages') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $ProjectPath 'ProjectSettings') -Force | Out-Null

    $manifest = [ordered]@{ dependencies = [ordered]@{} }
    foreach ($key in ($Dependencies.Keys | Sort-Object)) {
        $manifest.dependencies[$key] = $Dependencies[$key]
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $ProjectPath 'Packages\manifest.json'),
        ($manifest | ConvertTo-Json -Depth 5),
        [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText(
        (Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'),
        "m_EditorVersion: 6000.5.3f1`nm_EditorVersionWithRevision: 6000.5.3f1 (c2eb47b3a2a9)`n",
        [System.Text.UTF8Encoding]::new($false))
}

# .unitypackage를 batchmode 소비 프로젝트에 대화상자 없이 가져옵니다.
function Import-UnityPackage {
    param(
        [string]$ProjectPath,
        [string]$PackagePath,
        [string]$LogPath,
        [string]$Label
    )

    Invoke-Unity -Arguments @(
        '-batchmode',
        '-projectPath', $ProjectPath,
        '-importPackage', $PackagePath,
        '-quit',
        '-logFile', $LogPath
    ) -Label $Label
}

# sidecar가 선언한 Unity package 요구사항을 project manifest용 hashtable로 변환합니다.
function Get-RequiredPackages {
    param([string]$MetadataPath)

    $result = @{}
    $metadata = Get-Content -LiteralPath $MetadataPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($dependency in $metadata.requiredPackages) {
        if (-not [string]::IsNullOrWhiteSpace($dependency.packageId) -and
            -not [string]::IsNullOrWhiteSpace($dependency.version)) {
            $result[$dependency.packageId] = $dependency.version
        }
    }
    return $result
}

if (-not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity executable was not found: $UnityPath"
}

$workspaceRoot = (Resolve-Path -LiteralPath $Workspace).Path
$distribution = Join-Path $workspaceRoot 'Distribution\RogueDungeonLab\R9'
$logsRoot = Join-Path $workspaceRoot 'Logs\R9ConsumerVerification'
$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$runtimeProject = Join-Path $logsRoot "RuntimeConsumer_$timestamp"
$bakedProject = Join-Path $logsRoot "BakedConsumer_$timestamp"

$corePackage = Join-Path $distribution 'rogue-dungeon-lab-runtime-core.unitypackage'
$examplesPackage = Join-Path $distribution 'rogue-dungeon-lab-runtime-examples.unitypackage'
$authoringPackage = Join-Path $distribution 'rogue-dungeon-lab-bake-authoring-standalone.unitypackage'
$stageMetadata = Get-ChildItem -LiteralPath $distribution -Filter 'rogue-dungeon-lab-stage-*.unitypackage.json' |
    Where-Object { $_.Name -notlike '*-standalone.unitypackage.json' } |
    Select-Object -First 1
if ($null -eq $stageMetadata) {
    throw 'A modular Baked Stage package sidecar was not found.'
}
$stagePackage = $stageMetadata.FullName.Substring(0, $stageMetadata.FullName.Length - 5)
foreach ($requiredFile in @($corePackage, $examplesPackage, $authoringPackage, $stagePackage)) {
    if (-not (Test-Path -LiteralPath $requiredFile)) {
        throw "Required R9 package was not found: $requiredFile"
    }
}

New-Item -ItemType Directory -Path $logsRoot -Force | Out-Null
$coreModules = @{
    'com.unity.modules.jsonserialize' = '1.0.0'
    'com.unity.modules.physics' = '1.0.0'
    'com.unity.modules.ui' = '1.0.0'
}

# Core-only 소비 프로젝트는 외부 package와 Sample 없이 import·RuntimeBuild·Player build를 검증합니다.
New-ConsumerProject -ProjectPath $runtimeProject -Dependencies $coreModules
Import-UnityPackage `
    -ProjectPath $runtimeProject `
    -PackagePath $corePackage `
    -LogPath (Join-Path $logsRoot "RuntimeImport_$timestamp.log") `
    -Label 'R9 Runtime Core import'
Import-UnityPackage `
    -ProjectPath $runtimeProject `
    -PackagePath $examplesPackage `
    -LogPath (Join-Path $logsRoot "RuntimeExamplesImport_$timestamp.log") `
    -Label 'R9 Runtime Examples import'
$runtimeEditor = Join-Path $runtimeProject 'Assets\Editor'
New-Item -ItemType Directory -Path $runtimeEditor -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'r9-consumer\RuntimeConsumerSmoke.cs') -Destination $runtimeEditor
$runtimeBuild = Join-Path $runtimeProject 'Build\R9RuntimeConsumer.exe'
Invoke-Unity -Arguments @(
    '-batchmode',
    '-projectPath', $runtimeProject,
    '-executeMethod', 'RogueDungeonLabConsumerVerification.RuntimeConsumerSmoke.VerifyAndBuild',
    '-rdlBuildPath', $runtimeBuild,
    '-quit',
    '-logFile', (Join-Path $logsRoot "RuntimeBuild_$timestamp.log")
) -Label 'R9 Runtime Core consumer smoke'

# Baked 소비 프로젝트는 sidecar의 render-pipeline package, 독립 제작 도구와 Stage 묶음을 검증합니다.
$bakedDependencies = Get-RequiredPackages -MetadataPath $stageMetadata.FullName
foreach ($moduleId in $coreModules.Keys) {
    $bakedDependencies[$moduleId] = $coreModules[$moduleId]
}
New-ConsumerProject -ProjectPath $bakedProject -Dependencies $bakedDependencies
Import-UnityPackage `
    -ProjectPath $bakedProject `
    -PackagePath $authoringPackage `
    -LogPath (Join-Path $logsRoot "BakedAuthoringImport_$timestamp.log") `
    -Label 'R9 Bake Authoring import'
Import-UnityPackage `
    -ProjectPath $bakedProject `
    -PackagePath $stagePackage `
    -LogPath (Join-Path $logsRoot "BakedStageImport_$timestamp.log") `
    -Label 'R9 Baked Stage import'
$bakedEditor = Join-Path $bakedProject 'Assets\Editor'
New-Item -ItemType Directory -Path $bakedEditor -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'r9-consumer\BakedConsumerSmoke.cs') -Destination $bakedEditor
$bakedBuild = Join-Path $bakedProject 'Build\R9BakedConsumer.exe'
Invoke-Unity -Arguments @(
    '-batchmode',
    '-projectPath', $bakedProject,
    '-executeMethod', 'RogueDungeonLabConsumerVerification.BakedConsumerSmoke.VerifyAndBuild',
    '-rdlBuildPath', $bakedBuild,
    '-quit',
    '-logFile', (Join-Path $logsRoot "BakedBuild_$timestamp.log")
) -Label 'R9 Baked Stage consumer smoke'

$summary = [pscustomobject]@{
    RuntimeProject = $runtimeProject
    RuntimeBuild = $runtimeBuild
    BakedProject = $bakedProject
    BakedBuild = $bakedBuild
    StagePackage = $stagePackage
}
$summaryPath = Join-Path $logsRoot "VERIFICATION_SUMMARY_$timestamp.json"
[System.IO.File]::WriteAllText(
    $summaryPath,
    ($summary | ConvertTo-Json -Depth 3),
    [System.Text.UTF8Encoding]::new($false))
$summary
