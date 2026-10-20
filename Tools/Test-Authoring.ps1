[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EngineRoot,
    [Parameter(Mandatory)][string]$BuildAssembly,
    [Parameter(Mandatory)][string]$ToolsTarget,
    [Parameter(Mandatory)][string]$Output,
    [string]$DotNet = 'dotnet'
)

$ErrorActionPreference = 'Stop'
$dotnetHost = (Get-Command -Name $DotNet -ErrorAction Stop).Source
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$enginePath = (Resolve-Path -LiteralPath $EngineRoot).Path
$compilerPath = (Resolve-Path -LiteralPath $BuildAssembly).Path
$outputPath = [IO.Path]::GetFullPath($Output)
$scriptsPath = Join-Path $outputPath 'scripts'
$engineAssemblies = Split-Path -Parent $compilerPath
$tests = Join-Path $projectRoot 'Tests/InnoEngine.Rendering2D.Tests/InnoEngine.Rendering2D.Tests.csproj'

& $dotnetHost $compilerPath scripts --engine-root $enginePath --tools-target $ToolsTarget `
    --project $projectRoot --output $scriptsPath --ide --dotnet $dotnetHost
if ($LASTEXITCODE -ne 0) { throw 'Formal plugin script compilation failed.' }

& $dotnetHost test $tests --configuration Release --disable-build-servers `
    --artifacts-path (Join-Path $outputPath 'tests') `
    --results-directory (Join-Path $outputPath 'results') --logger 'trx;LogFileName=authoring.trx' `
    -m:1 -nr:false -p:UseSharedCompilation=false -warnaserror `
    "-p:Rendering2DEngineAssemblies=$engineAssemblies" "-p:Rendering2DScriptOutput=$scriptsPath"
if ($LASTEXITCODE -ne 0) { throw 'Plugin authoring contract tests failed.' }
