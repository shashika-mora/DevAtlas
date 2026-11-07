[CmdletBinding()]
param([string]$OutputDirectory = "artifacts/release-v0.1.0")
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$repository = Split-Path -Parent $PSScriptRoot
Push-Location $repository
try {
    [xml]$properties = Get-Content -LiteralPath "Directory.Build.props"
    $version = [string]$properties.Project.PropertyGroup.Version
    $output = [IO.Path]::GetFullPath((Join-Path $repository $OutputDirectory))
    if (-not $output.StartsWith($repository + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Release output must stay inside the repository."
    }
    if (Test-Path -LiteralPath $output) { throw "Choose a fresh output directory; existing artifacts will not be overwritten." }
    $publish = Join-Path $output "DevAtlas-$version-win-x64"
    New-Item -ItemType Directory -Path $publish -Force | Out-Null
    dotnet publish src/DevAtlas.App/DevAtlas.App.csproj -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -p:PublishSingleFile=false -o $publish
    if ($LASTEXITCODE -ne 0) { throw "Publish failed." }
    foreach ($document in @("LICENSE", "README.md", "CHANGELOG.md")) {
        Copy-Item -LiteralPath (Join-Path $repository $document) -Destination $publish
    }
    $commit = git rev-parse HEAD
    $dirty = -not [string]::IsNullOrEmpty((git status --porcelain --untracked-files=no | Out-String).Trim())
    @{ version = $version; commit = $commit; dirty = $dirty; sdk = (dotnet --version); runtime = "win-x64" } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $publish "BUILD-INFO.json") -Encoding utf8
    Copy-Item -LiteralPath "docs/RELEASE-0.1.0.md" -Destination (Join-Path $publish "RELEASE-NOTES.md")
    python eng/collect_notices.py src/DevAtlas.App/obj/project.assets.json $publish
    if ($LASTEXITCODE -ne 0) { throw "Dependency inventory failed." }
    $archive = Join-Path $output "DevAtlas-$version-win-x64.zip"
    Compress-Archive -LiteralPath $publish -DestinationPath $archive -CompressionLevel Optimal
    Copy-Item -LiteralPath (Join-Path $publish "DEPENDENCIES.json") -Destination $output
    $checksums = foreach ($file in @($archive, (Join-Path $output "DEPENDENCIES.json"))) {
        $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $([IO.Path]::GetFileName($file))"
    }
    [IO.File]::WriteAllLines((Join-Path $output "SHA256SUMS.txt"), $checksums, [Text.UTF8Encoding]::new($false))
    Write-Output "Release artifacts: $output"
}
finally { Pop-Location }
