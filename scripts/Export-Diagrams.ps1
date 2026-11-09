param(
    [Parameter(Mandatory = $true)]
    [string] $DrawioPath
)

$ErrorActionPreference = 'Stop'
$images = Join-Path $PSScriptRoot '..\docs\images'
foreach ($source in Get-ChildItem $images -Filter '*.drawio' | Sort-Object Name) {
    $profile = Join-Path ([IO.Path]::GetTempPath()) "maf-diagram-export-$([Guid]::NewGuid())"
    New-Item -ItemType Directory $profile | Out-Null
    $output = [IO.Path]::ChangeExtension($source.FullName, '.svg')
    $stdout = Join-Path $profile 'export.stdout.log'
    $stderr = Join-Path $profile 'export.stderr.log'
    $process = Start-Process -FilePath $DrawioPath -Wait -PassThru `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr -ArgumentList @(
        "--user-data-dir=`"$profile`"", '-x', '-f', 'svg', '-e', '-b', '10',
        '-o', "`"$output`"", "`"$($source.FullName)`""
    )
    if ($process.ExitCode -ne 0 -or !(Test-Path $output)) {
        throw "draw.io export failed for $($source.Name) (exit $($process.ExitCode)). $(Get-Content $stderr -Raw) $(Get-Content $stdout -Raw)"
    }
    $svg = Get-Content $output -Raw
    $sourceText = (Get-Content $source.FullName -Raw).Replace("`r`n", "`n")
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes($sourceText))).ToLowerInvariant()
    $svg = $svg -replace '<svg ', "<svg data-source-sha256=`"$hash`" "
    [IO.File]::WriteAllText($output, $svg, [Text.UTF8Encoding]::new($false))
    Remove-Item -LiteralPath (Resolve-Path $profile).Path -Recurse -Force
}
& (Join-Path $PSScriptRoot 'Test-Diagrams.ps1')
