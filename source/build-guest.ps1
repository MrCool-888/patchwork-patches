param([Parameter(Mandatory=$true)][string]$OriginalApp, [Parameter(Mandatory=$true)][string]$OutputDirectory, [Parameter(Mandatory=$true)][string]$Compiler, [string]$RuntimeDirectory, [switch]$Tests, [switch]$Live, [switch]$Ui)
$ErrorActionPreference = 'Stop'
$taskCompiler = [IO.Path]::GetFullPath($Compiler)
if (!(Test-Path -LiteralPath $taskCompiler -PathType Leaf)) { throw 'Specify a modern Roslyn csc.exe compiler.' }
if (([int]$Tests.IsPresent + [int]$Live.IsPresent + [int]$Ui.IsPresent) -gt 1) { throw 'Choose only one probe mode.' }
$taskRuntime = $RuntimeDirectory
if (!$taskRuntime) {
    $taskRuntime = Get-ChildItem -LiteralPath (Join-Path $env:ProgramFiles 'dotnet\shared\Microsoft.NETCore.App') -Directory | Where-Object Name -Match '^8\.0\.\d+$' | Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (!$taskRuntime -or !(Test-Path -LiteralPath (Join-Path $taskRuntime 'System.Runtime.dll'))) { throw 'A .NET 8 runtime reference directory is required.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$taskRefs = @(Get-ChildItem -LiteralPath $taskRuntime -Filter 'System*.dll' | Where-Object Name -NotLike '*.Native.dll' | ForEach-Object { '/r:' + $_.FullName })
$taskNames = @('Microsoft.WinUI.dll','WinRT.Runtime.dll','Microsoft.Windows.SDK.NET.dll','Microsoft.InteractiveExperiences.Projection.dll','ProtonVPN.Common.Legacy.dll','ProtonVPN.Common.Core.dll','ProtonVPN.Client.Logic.Auth.dll','ProtonVPN.Api.dll')
$taskRefs += $taskNames | ForEach-Object { '/r:' + (Join-Path $OriginalApp $_) }
$taskRefs += Get-ChildItem -LiteralPath $OriginalApp -Filter 'ProtonVPN.*Contracts.dll' | ForEach-Object { '/r:' + $_.FullName }
$taskTarget = if ($Tests -or $Live -or $Ui) { 'exe' } else { 'library' }
$taskSources = @((Join-Path $PSScriptRoot 'GuestClient.cs'))
if ($Tests) { $taskSources += Join-Path $PSScriptRoot 'GuestLifecycleProbe.cs' }
if ($Live) { $taskSources += Join-Path $PSScriptRoot 'GuestLiveProbe.cs' }
if ($Ui) { $taskSources += Join-Path $PSScriptRoot 'GuestUiProbe.cs' }
$taskOutput = Join-Path $OutputDirectory $(if ($Live) { 'GuestLiveProbe.dll' } elseif ($Tests) { 'GuestLifecycleProbe.dll' } elseif ($Ui) { 'GuestUiProbe.dll' } else { 'Patchwork.ProtonGuest.dll' })
& $taskCompiler /nologo /noconfig /nostdlib+ ('/target:' + $taskTarget) /optimize+ /platform:x64 ('/out:' + $taskOutput) @taskRefs @taskSources
if ($LASTEXITCODE -ne 0) { throw 'Guest module compilation failed.' }
