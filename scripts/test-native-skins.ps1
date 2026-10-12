$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskFramework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$taskDirectory = Join-Path $taskRoot '.tmp\native-skins-test'
New-Item -ItemType Directory -Path $taskDirectory -Force | Out-Null
$taskOutput = Join-Path $taskDirectory 'NativeSkinTests.exe'
$taskArgs = @('/nologo','/codepage:65001','/target:exe','/platform:x64','/main:MogooCodexStatus.Tests.NativeSkinTests',('/out:' + $taskOutput))
foreach ($taskDll in @('System.Xaml.dll','System.Web.Extensions.dll','WPF\PresentationFramework.dll','WPF\PresentationCore.dll','WPF\WindowsBase.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll')) { $taskArgs += '/r:' + (Join-Path $taskFramework $taskDll) }
$taskArgs += Join-Path $taskRoot 'native\StatusOverlay.cs'
$taskArgs += Join-Path $taskRoot 'tests\native-skins.cs'
& (Join-Path $taskFramework 'csc.exe') @taskArgs
if ($LASTEXITCODE -ne 0) { throw 'Native skin test compilation failed.' }
& $taskOutput (Join-Path $taskDirectory 'data')
if ($LASTEXITCODE -ne 0) { throw 'Native skin regression tests failed.' }
