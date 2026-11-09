$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$errors = [Collections.Generic.List[string]]::new()
$files = @(& git -C $root ls-files --cached --others --exclude-standard | Sort-Object -Unique)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate repository files.' }

foreach ($relative in $files) {
    $path = Join-Path $root $relative
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    if ($relative -match '^(book|chapter_notebooks)/' -or
        $relative -match '^\.github/skills/maf-pattern-demo/assets/') { continue }
    $parent = Split-Path $path
    if ($relative.EndsWith('.md')) {
        $text = Get-Content $path -Raw
        foreach ($match in [regex]::Matches($text, '\]\(([^)]+)\)')) {
            $href = $match.Groups[1].Value.Trim()
            if ($href -match '^([a-zA-Z][a-zA-Z0-9+.-]*:|#)' -or
                $href -match '[<>{}]') { continue }
            $local = [Uri]::UnescapeDataString(($href -split '#', 2)[0])
            $target = Join-Path $parent ($local.Replace('/', [IO.Path]::DirectorySeparatorChar))
            if (!(Test-Path -LiteralPath $target)) {
                $errors.Add("${relative}: broken link $href")
            }
        }
    }
    if ($relative.EndsWith('.csproj')) {
        [xml] $projectXml = Get-Content $path -Raw
        foreach ($reference in $projectXml.SelectNodes('//ProjectReference | //Compile[@Include]')) {
            $include = $reference.GetAttribute('Include')
            if ($include -match '[*$?]') { continue }
            $target = Join-Path $parent ($include.Replace('/', [IO.Path]::DirectorySeparatorChar))
            if (!(Test-Path -LiteralPath $target)) {
                $errors.Add("${relative}: broken project/source reference $include")
            }
        }
    }
    if ($relative.EndsWith('.slnx')) {
        [xml] $solution = Get-Content $path -Raw
        foreach ($solutionProject in $solution.SelectNodes('//Project')) {
            $target = Join-Path $parent $solutionProject.GetAttribute('Path')
            if (!(Test-Path -LiteralPath $target)) {
                $errors.Add("${relative}: missing solution project $($solutionProject.Path)")
            }
        }
    }
    if ($relative -like 'dotnet/src/*/azure.yaml') {
        $text = Get-Content $path -Raw
        $startup = [regex]::Matches($text, 'startupCommand: dotnet run --project ([^\r\n ]+)')
        $entries = [regex]::Matches($text, 'entryPoint: ([^\r\n ]+)')
        if ($startup.Count -ne $entries.Count) {
            $errors.Add("${relative}: startup/entry-point count mismatch")
        }
        for ($i = 0; $i -lt $startup.Count; $i++) {
            $projectFile = $startup[$i].Groups[1].Value
            $entry = $entries[$i].Groups[1].Value
            if (!(Test-Path (Join-Path $parent $projectFile))) {
                $errors.Add("${relative}: missing startup project $projectFile")
            }
            if ($entry -ne [IO.Path]::ChangeExtension($projectFile, '.dll')) {
                $errors.Add("${relative}: startup DLL mismatch $entry")
            }
        }
    }
}
if ($errors.Count) { throw ($errors -join "`n") }
Write-Host 'Validated local documentation links, project references, solutions and hosted entry-point names.'
