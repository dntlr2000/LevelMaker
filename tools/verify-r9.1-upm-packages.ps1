param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe',
    [string]$Workspace = (Split-Path -Parent $PSScriptRoot),
    [string]$ValidationRoot = 'E:\CodexValidation\RogueDungeonLabR91',
    [string]$TempRoot = 'E:\CodexTemp'
)

$ErrorActionPreference = 'Stop'

# 검증 project·log·build 경로가 금지된 C 드라이브에 있지 않은지 확인합니다.
function Assert-ValidationPath {
    param([string]$Path, [string]$Label)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if ([string]::Equals(
            [System.IO.Path]::GetPathRoot($fullPath),
            'C:\',
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label must not be created on C drive: $fullPath"
    }
}

# Unity를 숨김 batchmode로 실행하고 제한 시간·종료 코드를 검증합니다.
function Invoke-Unity {
    param(
        [string[]]$Arguments,
        [string]$Label,
        [int]$TimeoutMilliseconds = 1800000
    )

    $process = Start-Process `
        -FilePath $UnityPath `
        -ArgumentList $Arguments `
        -PassThru `
        -WindowStyle Hidden
    try {
        if (-not $process.WaitForExit($TimeoutMilliseconds)) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            throw "$Label exceeded the $TimeoutMilliseconds ms timeout."
        }
        $process.Refresh()
        if ($process.ExitCode -ne 0) {
            throw "$Label failed with Unity exit code $($process.ExitCode)."
        }
    }
    finally {
        $process.Dispose()
    }
}

# 최소 Unity 6000.5 소비 project와 정렬된 UPM dependency manifest를 만듭니다.
function New-ConsumerProject {
    param(
        [string]$ProjectPath,
        [System.Collections.IDictionary]$Dependencies
    )

    Assert-ValidationPath -Path $ProjectPath -Label 'Consumer project'
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

# package root 절대 경로를 Unity manifest의 file dependency 값으로 변환합니다.
function ConvertTo-LocalPackageDependency {
    param([string]$PackagePath)

    $fullPath = [System.IO.Path]::GetFullPath($PackagePath).Replace('\', '/')
    return "file:$fullPath"
}

# 검증용 Editor harness를 소비 project의 Assets/Editor에 복사합니다.
function Copy-ConsumerHarness {
    param(
        [string]$ProjectPath,
        [string]$HarnessPath
    )

    $editorFolder = Join-Path $ProjectPath 'Assets\Editor'
    New-Item -ItemType Directory -Path $editorFolder -Force | Out-Null
    Copy-Item -LiteralPath $HarnessPath -Destination $editorFolder -Force
}

# Core의 Samples~ RuntimeBuild 예제를 Package Manager import 결과 위치로 복사합니다.
function Copy-CoreRuntimeSample {
    param(
        [string]$CorePackagePath,
        [string]$ProjectPath
    )

    $source = Join-Path $CorePackagePath 'Samples~\RuntimeBuild'
    $sampleParent = Join-Path $ProjectPath 'Assets\Samples\Rogue Dungeon Lab - Runtime Core\0.13.0'
    $destination = Join-Path $sampleParent 'RuntimeBuild Examples'
    New-Item -ItemType Directory -Path $sampleParent -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Recurse -Force
    Copy-Item `
        -LiteralPath "$source.meta" `
        -Destination "$destination.meta" `
        -Force
}

if (-not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity executable was not found: $UnityPath"
}

$workspaceRoot = (Resolve-Path -LiteralPath $Workspace).Path
$validationRootFull = [System.IO.Path]::GetFullPath($ValidationRoot)
$tempRootFull = [System.IO.Path]::GetFullPath($TempRoot)
Assert-ValidationPath -Path $validationRootFull -Label 'Validation root'
Assert-ValidationPath -Path $tempRootFull -Label 'Temporary root'
New-Item -ItemType Directory -Path $validationRootFull -Force | Out-Null
New-Item -ItemType Directory -Path $tempRootFull -Force | Out-Null
$env:TEMP = $tempRootFull
$env:TMP = $tempRootFull

$packagesRoot = Join-Path $workspaceRoot 'UpmPackages'
$corePackagePath = Join-Path $packagesRoot 'com.dntlr2000.rogue-dungeon-lab.core'
$labPackagePath = Join-Path $packagesRoot 'com.dntlr2000.rogue-dungeon-lab.lab'
$bakingPackagePath = Join-Path $packagesRoot 'com.dntlr2000.rogue-dungeon-lab.baking'
foreach ($packagePath in @($corePackagePath, $labPackagePath, $bakingPackagePath)) {
    if (-not (Test-Path -LiteralPath (Join-Path $packagePath 'package.json'))) {
        throw "R9.1 UPM package was not synchronized: $packagePath"
    }
}

$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$runRoot = Join-Path $validationRootFull "Run_$timestamp"
$coreProject = Join-Path $runRoot 'CoreConsumer'
$labProject = Join-Path $runRoot 'LabConsumer'
$bakingProject = Join-Path $runRoot 'BakingConsumer'
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null

$coreDependencies = [ordered]@{
    'com.dntlr2000.rogue-dungeon-lab.core' =
        (ConvertTo-LocalPackageDependency -PackagePath $corePackagePath)
}
New-ConsumerProject -ProjectPath $coreProject -Dependencies $coreDependencies
Copy-CoreRuntimeSample -CorePackagePath $corePackagePath -ProjectPath $coreProject
Copy-ConsumerHarness `
    -ProjectPath $coreProject `
    -HarnessPath (Join-Path $PSScriptRoot 'r9.1-consumer\UpmCoreConsumerSmoke.cs')
$coreBuild = Join-Path $runRoot 'Build\R9_1CoreConsumer.exe'
$coreLog = Join-Path $runRoot 'CoreConsumer.log'
New-Item -ItemType Directory -Path (Split-Path -Parent $coreBuild) -Force | Out-Null
Assert-ValidationPath -Path $coreBuild -Label 'Core Player build'
Assert-ValidationPath -Path $coreLog -Label 'Core log'
Invoke-Unity -Arguments @(
    '-batchmode',
    '-nographics',
    '-projectPath', $coreProject,
    '-executeMethod', 'RogueDungeonLabUpmConsumerVerification.UpmCoreConsumerSmoke.VerifyAndBuild',
    '-rdlBuildPath', $coreBuild,
    '-quit',
    '-logFile', $coreLog
) -Label 'R9.1 UPM Core consumer smoke'

$labDependencies = [ordered]@{
    'com.dntlr2000.rogue-dungeon-lab.core' =
        (ConvertTo-LocalPackageDependency -PackagePath $corePackagePath)
    'com.dntlr2000.rogue-dungeon-lab.lab' =
        (ConvertTo-LocalPackageDependency -PackagePath $labPackagePath)
}
New-ConsumerProject -ProjectPath $labProject -Dependencies $labDependencies
Copy-ConsumerHarness `
    -ProjectPath $labProject `
    -HarnessPath (Join-Path $PSScriptRoot 'r9.1-consumer\UpmLabConsumerSmoke.cs')
$labLog = Join-Path $runRoot 'LabConsumer.log'
Assert-ValidationPath -Path $labLog -Label 'Lab log'
Invoke-Unity -Arguments @(
    '-batchmode',
    '-nographics',
    '-projectPath', $labProject,
    '-executeMethod', 'RogueDungeonLabUpmConsumerVerification.UpmLabConsumerSmoke.Verify',
    '-quit',
    '-logFile', $labLog
) -Label 'R9.1 UPM Lab consumer smoke'

$bakingDependencies = [ordered]@{
    'com.dntlr2000.rogue-dungeon-lab.core' =
        (ConvertTo-LocalPackageDependency -PackagePath $corePackagePath)
    'com.dntlr2000.rogue-dungeon-lab.baking' =
        (ConvertTo-LocalPackageDependency -PackagePath $bakingPackagePath)
}
New-ConsumerProject -ProjectPath $bakingProject -Dependencies $bakingDependencies
Copy-ConsumerHarness `
    -ProjectPath $bakingProject `
    -HarnessPath (Join-Path $PSScriptRoot 'r9.1-consumer\UpmBakingConsumerSmoke.cs')
$bakedPackage = Join-Path $runRoot 'R9_1BakedStage.unitypackage'
$bakingLog = Join-Path $runRoot 'BakingConsumer.log'
Assert-ValidationPath -Path $bakedPackage -Label 'Baked stage package'
Assert-ValidationPath -Path $bakingLog -Label 'Baking log'
Invoke-Unity -Arguments @(
    '-batchmode',
    '-nographics',
    '-projectPath', $bakingProject,
    '-executeMethod', 'RogueDungeonLabUpmConsumerVerification.UpmBakingConsumerSmoke.Verify',
    '-rdlOutputPath', $bakedPackage,
    '-quit',
    '-logFile', $bakingLog
) -Label 'R9.1 UPM Baking consumer smoke'

$summary = [ordered]@{
    Version = '0.13.0'
    RunRoot = $runRoot
    CoreProject = $coreProject
    CoreBuild = $coreBuild
    LabProject = $labProject
    BakingProject = $bakingProject
    BakedStagePackage = $bakedPackage
    CorePackage = $corePackagePath
    LabPackage = $labPackagePath
    BakingPackage = $bakingPackagePath
}
$summaryPath = Join-Path $runRoot 'VERIFICATION_SUMMARY.json'
[System.IO.File]::WriteAllText(
    $summaryPath,
    ($summary | ConvertTo-Json -Depth 4),
    [System.Text.UTF8Encoding]::new($false))
[pscustomobject]$summary
