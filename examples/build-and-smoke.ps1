param(
    [Parameter(Mandatory = $true)][string]$PatchworkExe,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$PatchworkExe = (Resolve-Path -LiteralPath $PatchworkExe).Path
$patchworkDirectory = Split-Path -Parent $PatchworkExe
$cecil = Join-Path $patchworkDirectory 'Mono.Cecil.dll'
if (-not (Test-Path -LiteralPath $cecil)) { throw 'Mono.Cecil.dll must be next to Patchwork.exe.' }
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework 4.x compiler is required to build the managed fixture and harness.' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $env:TEMP ('pwfx-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) {
    if (Get-ChildItem -LiteralPath $OutputDirectory -Force | Select-Object -First 1) { throw 'OutputDirectory must not already contain files. Choose a fresh folder.' }
} else { New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null }
$runner = Join-Path $OutputDirectory 'runner'
New-Item -ItemType Directory -Path $runner -Force | Out-Null
$managedFixture = Join-Path $OutputDirectory 'managed-assembly'
New-Item -ItemType Directory -Path $managedFixture -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'managed-assembly\FixtureAssembly.cs') -Destination (Join-Path $managedFixture 'FixtureAssembly.cs')
Copy-Item -LiteralPath $PatchworkExe -Destination (Join-Path $runner 'Patchwork.exe')
Copy-Item -LiteralPath $cecil -Destination (Join-Path $runner 'Mono.Cecil.dll')
$harnessExe = Join-Path $runner 'FixtureHarness.exe'
$harnessSource = Join-Path $PSScriptRoot 'FixtureHarness.cs'
$references = @()
$references += '/reference:' + (Join-Path $runner 'Patchwork.exe')
$references += '/reference:' + (Join-Path $framework 'System.Web.Extensions.dll')
$references += '/reference:' + (Join-Path $framework 'System.IO.Compression.dll')
$references += '/reference:' + (Join-Path $framework 'System.IO.Compression.FileSystem.dll')
$compileArguments = @('/nologo', '/optimize+', '/target:exe', ('/out:' + $harnessExe)) + $references + @($harnessSource)
& $compiler @compileArguments
if ($LASTEXITCODE -ne 0) { throw 'Fixture harness compilation failed.' }
& $harnessExe $PatchworkExe $OutputDirectory generate
if ($LASTEXITCODE -ne 0) { throw 'Fixture and recipe generation failed.' }

foreach ($name in @('config-resources', 'managed-assembly', 'electron-asar', 'java-zip', 'raw-binary')) {
    foreach ($version in @('1.0.0', '1.1.0', '1.2.0')) {
        $selection = if ($version -eq '1.0.0') { 'a,b' } elseif ($version -eq '1.1.0') { 'a' } else { 'b' }
        $recipe = Join-Path $OutputDirectory (Join-Path $name ('pack-' + $version + '.json'))
        $target = Join-Path $OutputDirectory (Join-Path $name 'target')
        & $PatchworkExe --validate-patch $recipe --target $target --select $selection
        if ($LASTEXITCODE -ne 0) { throw "Production validator rejected $name $version selection $selection." }
    }
}

& $harnessExe $PatchworkExe $OutputDirectory smoke
if ($LASTEXITCODE -ne 0) { throw 'Production engine lifecycle smoke failed.' }
Write-Output "Five synthetic recipes validated and completed apply/update/deselect/restore: $OutputDirectory"
