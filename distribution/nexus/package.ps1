$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$exe = Join-Path $root 'artifacts/standalone/Editor.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Publish the Standalone profile first.' }
$output = Join-Path $root 'artifacts/nexus'
$stage = Join-Path $output ([Guid]::NewGuid().ToString('N'))
$zip = Join-Path $output 'Valkyrie-0.1.0-beta-win-x64.zip'
if (Test-Path -LiteralPath $zip) { throw 'Archive already exists; move it before rebuilding.' }
New-Item -ItemType Directory -Path $stage -Force | Out-Null
try {
    Copy-Item -LiteralPath $exe -Destination (Join-Path $stage 'Editor.exe')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'START-HERE.txt') -Destination $stage
    $hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText((Join-Path $stage 'SHA256SUMS.txt'), "$hash  Editor.exe`n", [Text.Encoding]::ASCII)
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $names = @($archive.Entries | ForEach-Object { $_.FullName } | Sort-Object)
        if (($names -join ',') -ne 'Editor.exe,SHA256SUMS.txt,START-HERE.txt') { throw 'Unexpected archive contents.' }
        $stream = $archive.GetEntry('Editor.exe').Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try {
            $actual = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
            if ($actual -ne $hash) { throw 'Archived executable checksum mismatch.' }
        } finally { $sha.Dispose(); $stream.Dispose() }
    } finally { $archive.Dispose() }
    Write-Output "Verified archive: $zip"
    Write-Output "Executable SHA256: $hash"
} finally {
    Remove-Item -LiteralPath $stage -Recurse -Force
}
