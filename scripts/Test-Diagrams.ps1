$ErrorActionPreference = 'Stop'
$images = Join-Path $PSScriptRoot '..\docs\images'
$sources = @(Get-ChildItem $images -Filter '*.drawio')
foreach ($source in $sources) {
    [xml] $diagram = Get-Content $source.FullName -Raw
    $cells = @($diagram.SelectNodes('//mxCell'))
    $ids = @($cells | ForEach-Object { $_.GetAttribute('id') })
    if ($ids.Count -ne @($ids | Select-Object -Unique).Count -or
        '0' -notin $ids -or '1' -notin $ids) {
        throw "Invalid cell IDs: $($source.Name)"
    }
    foreach ($edge in $cells | Where-Object { $_.GetAttribute('edge') -eq '1' }) {
        if ($edge.GetAttribute('source') -notin $ids -or
            $edge.GetAttribute('target') -notin $ids -or
            $null -eq $edge.SelectSingleNode('mxGeometry[@relative="1"]')) {
            throw "Invalid edge $($edge.id): $($source.Name)"
        }
    }
    $output = [IO.Path]::ChangeExtension($source.FullName, '.svg')
    if (!(Test-Path $output)) { throw "Missing SVG: $output" }
    [xml] $svg = Get-Content $output -Raw
    $sourceText = (Get-Content $source.FullName -Raw).Replace("`r`n", "`n")
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes($sourceText))).ToLowerInvariant()
    if ($svg.DocumentElement.GetAttribute('data-source-sha256') -ne $hash) {
        throw "Stale SVG: $output. Run scripts\Export-Diagrams.ps1."
    }
    if (!$svg.DocumentElement.GetAttribute('content')) {
        throw "SVG has no editable diagram: $output"
    }
}
Write-Host "Validated $($sources.Count) editable diagram/SVG pairs. Review arrows and layout visually too."
