$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskFramework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$taskCompiler = Join-Path $taskFramework 'csc.exe'
if (-not (Test-Path -LiteralPath $taskCompiler)) { throw 'Windows .NET Framework 4.8 x64 is required.' }
$taskOutput = Join-Path $taskRoot 'bin'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskArgs = @('/nologo','/codepage:65001','/target:winexe','/platform:x64','/optimize+',('/out:' + (Join-Path $taskOutput 'MogooCodexStatus.exe')),('/win32manifest:' + (Join-Path $taskRoot 'native\app.manifest')))
foreach ($taskDll in @('System.Xaml.dll','System.Web.Extensions.dll','WPF\PresentationFramework.dll','WPF\PresentationCore.dll','WPF\WindowsBase.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll')) { $taskArgs += '/r:' + (Join-Path $taskFramework $taskDll) }
$taskArgs += Join-Path $taskRoot 'native\StatusOverlay.cs'
& $taskCompiler @taskArgs
if ($LASTEXITCODE -ne 0) { throw 'Native build failed.' }
Write-Output (Join-Path $taskOutput 'MogooCodexStatus.exe')
