# Run from the repository root after fetching historical release commits.
# Existing entries are retained, so a shallow checkout cannot erase known hashes.
# Current files are also fingerprinted from embedded resources at runtime.
$ErrorActionPreference = 'Stop'
$prefix = 'TabletFriend/TabletFriend/files/'
$output = Join-Path $PWD 'TabletFriend/TabletFriend/Migration/bundled-hashes.json'
$hashes = if (Test-Path -LiteralPath $output) { Get-Content -LiteralPath $output -Raw | ConvertFrom-Json -AsHashtable } else { @{} }

function Read-GitBlob([string] $objectId) {
    $start = [System.Diagnostics.ProcessStartInfo]::new('git')
    $start.ArgumentList.Add('cat-file')
    $start.ArgumentList.Add('blob')
    $start.ArgumentList.Add($objectId)
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.CreateNoWindow = $true
    $process = [System.Diagnostics.Process]::Start($start)
    $content = [System.IO.MemoryStream]::new()
    try {
        $process.StandardOutput.BaseStream.CopyTo($content)
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "Could not read Git blob $objectId" }
        return ,$content.ToArray()
    }
    finally { $process.Dispose(); $content.Dispose() }
}

$commits = git log --all --format=%H -- $prefix
if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate Git history.' }
$seen = [Collections.Generic.HashSet[string]]::new()
foreach ($commit in $commits) {
    # Enumerate each tree: rev-list --objects reports only one name for shared blobs.
    $entries = git ls-tree -r $commit -- $prefix
    if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate historical files.' }
    foreach ($entry in $entries) {
        if ($entry -notmatch '^\d+ blob ([0-9a-f]+)\t(.+)$') { continue }
        $objectId, $path = $Matches[1], $Matches[2]
        if (!$path.StartsWith($prefix)) { continue }
        $name = $path.Substring($prefix.Length)
        if ($name -notmatch '^(layouts|themes|icons)/' -or !$seen.Add("$objectId/$name")) { continue }
        $content = Read-GitBlob $objectId
        if ($name.EndsWith('.yaml', [StringComparison]::OrdinalIgnoreCase)) {
            $text = [Text.Encoding]::UTF8.GetString($content).TrimStart([char]0xFEFF).Replace("`r`n", "`n")
            $content = [Text.Encoding]::UTF8.GetBytes($text)
        }
        $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($content))
        $hashes[$name] = @(@($hashes[$name]) + $hash | Where-Object { $_ } | Sort-Object -Unique)
    }
}

$sorted = [ordered]@{}
foreach ($name in ($hashes.Keys | Sort-Object)) { $sorted[$name] = @($hashes[$name]) }
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output)) | Out-Null
$sorted | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $output -Encoding utf8
Write-Output "Recorded known hashes for $($sorted.Count) bundled paths."
