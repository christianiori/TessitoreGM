param(
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "src\TessitoreGM.Gm\TessitoreGM.Gm.csproj"
$artifacts = Join-Path $repositoryRoot "artifacts"
$output = Join-Path $artifacts "TessitoreGM-$Runtime"
$archive = Join-Path $artifacts "TessitoreGM-$Runtime.zip"

if (Test-Path -LiteralPath $output)
{
    Remove-Item -LiteralPath $output -Recurse -Force
}
if (Test-Path -LiteralPath $archive)
{
    Remove-Item -LiteralPath $archive -Force
}

dotnet publish $project `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    --output $output `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0)
{
    throw "La pubblicazione di TessitoreGM non è riuscita."
}

Compress-Archive -Path (Join-Path $output "*") -DestinationPath $archive
Write-Host "Pacchetto creato: $archive"
