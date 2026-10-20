[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EngineRoot,

    [ValidateSet("d3d11", "d3d12", "vulkan")]
    [string]$Backend = "d3d12",

    [ValidateRange(300, 10000)]
    [int]$SmokeFrames = 3000,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [string]$ArtifactsPath,

    [string]$TaskArtifactsPath,

    [string]$DotNet = "dotnet"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Description,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Operation
    )

    & $Operation
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

if (-not $IsWindows) {
    throw "Windows GPU validation must run on Windows x64 hardware."
}

$architecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
if ($architecture -ne "X64") {
    throw "Windows GPU validation requires an x64 process; current architecture is '$architecture'."
}

$sourceRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$engineRootPath = (Resolve-Path $EngineRoot).Path
$engineSolution = Join-Path $engineRootPath "InnoEngine.sln"
$editorProject = Join-Path $engineRootPath "entry/InnoEngine.Editor/InnoEngine.Editor.csproj"
$buildProject = Join-Path $engineRootPath "entry/InnoEngine.Build/InnoEngine.Build.csproj"

if (-not (Test-Path $engineSolution)) {
    throw "EngineRoot '$engineRootPath' does not contain InnoEngine.sln."
}

$outputArguments = @()
if ($ArtifactsPath) {
    $outputArguments += "-p:ArtifactsPath=$([IO.Path]::GetFullPath($ArtifactsPath, $engineRootPath))"
}
if ($TaskArtifactsPath) {
    $outputArguments += "-p:InnoTaskArtifacts=$([IO.Path]::GetFullPath($TaskArtifactsPath, $engineRootPath))"
}

Push-Location $engineRootPath
try {
    Invoke-Checked "Windows Editor build and product native preparation" {
        & $DotNet build $editorProject --configuration $Configuration `
            --disable-build-servers -m:1 -nr:false -p:UseSharedCompilation=false @outputArguments
    }
    $evaluatedOutput = @(& $DotNet msbuild $editorProject -nologo -getProperty:TargetPath `
        "-p:Configuration=$Configuration" -p:DesignTimeBuild=true @outputArguments)
    if ($LASTEXITCODE -ne 0 -or $evaluatedOutput.Count -ne 1) {
        throw 'The Editor project did not resolve one executable assembly.'
    }
    $editorAssembly = $evaluatedOutput[0].Trim()
}
finally {
    Pop-Location
}

if (-not (Test-Path $editorAssembly)) {
    throw "The Editor build did not produce '$editorAssembly'."
}

$projectRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("InnoRendering2DValidation-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory $projectRoot | Out-Null
Copy-Item -Recurse (Join-Path $sourceRoot "Assets") (Join-Path $projectRoot "Assets")
Copy-Item (Join-Path $sourceRoot "Settings.Project.inno") (Join-Path $projectRoot "Settings.Project.inno")
if (Test-Path (Join-Path $sourceRoot "Plugins")) {
    Copy-Item -Recurse (Join-Path $sourceRoot "Plugins") (Join-Path $projectRoot "Plugins")
}

$logDirectory = Join-Path $sourceRoot "Logs"
New-Item -ItemType Directory -Force $logDirectory | Out-Null
$validationLog = Join-Path $logDirectory "Rendering2D-$Backend-GpuValidation.log"
$processOutput = & $DotNet $editorAssembly `
    $projectRoot `
    --graphics-api $Backend `
    --smoke-frames $SmokeFrames 2>&1 | Tee-Object -FilePath $validationLog
$editorExitCode = $LASTEXITCODE

if ($editorExitCode -ne 0) {
    throw "Rendering2D $Backend GPU validation exited with code $editorExitCode. See '$validationLog'."
}

$validationText = @($processOutput) -join [Environment]::NewLine
$backendPattern = switch ($Backend) {
    "d3d11" { "Rendering initialized with Direct3D11" }
    "d3d12" { "Rendering initialized with Direct3D12" }
    "vulkan" { "Rendering initialized with Vulkan" }
}
if ($validationText -notmatch $backendPattern) {
    throw "The requested $Backend renderer was not initialized."
}
if ($validationText -notmatch "Smoke frame limit reached after $SmokeFrames frame\(s\)\.") {
    throw "The Editor did not submit the requested number of frames."
}
if ($validationText -match "BGFX FATAL|Unhandled exception|Teardown failure|Microsoft Basic Render Driver|SwiftShader|llvmpipe|\bWARP\b|unknown stable type|\[Warn\]\s+ASSET-REFERENCE") {
    throw "Rendering2D $Backend validation reported an import, backend, reference, or teardown failure."
}

Push-Location $engineRootPath
try {
    Invoke-Checked "Project compiler build" {
        & $DotNet build $buildProject --configuration $Configuration `
            --disable-build-servers -m:1 -nr:false -p:UseSharedCompilation=false @outputArguments
    }
    $evaluatedCompiler = @(& $DotNet msbuild $buildProject -nologo -getProperty:TargetPath `
        "-p:Configuration=$Configuration" -p:DesignTimeBuild=true @outputArguments)
    if ($LASTEXITCODE -ne 0 -or $evaluatedCompiler.Count -ne 1) {
        throw 'The Build project did not resolve one compiler assembly.'
    }
    $compilerAssembly = $evaluatedCompiler[0].Trim()
}
finally {
    Pop-Location
}

Invoke-Checked "Rendering2D script compilation and IDE generation" {
    & $DotNet $compilerAssembly scripts --engine-root $engineRootPath --tools-target windows-x64 `
        --dotnet $DotNet --project $projectRoot --output (Join-Path $projectRoot 'Library/Validation/Scripts') --ide
}

Invoke-Checked "Rendering2D generated script build" {
    & $DotNet build (Join-Path $projectRoot "Inno.EditorScripts.csproj") `
        -p:NuGetAudit=false `
        --disable-build-servers `
        -m:1 `
        -warnaserror
}

Write-Host "Rendering2D Windows GPU validation passed on $architecture/$Backend. Log: $validationLog"
Write-Host "Disposable validation project retained for inspection: $projectRoot"
