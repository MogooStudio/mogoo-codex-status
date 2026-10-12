param([string]$DataDirectory, [switch]$DryRun)
$ErrorActionPreference = 'Stop'
if (-not $DataDirectory) { $DataDirectory = Join-Path $env:LOCALAPPDATA 'mogoo-codex-status' }
$taskRecord = [ordered]@{schemaVersion=1;origin=$(if ($DryRun) {'dry-run'} else {'codex-hook'});receivedAtUtc=[DateTimeOffset]::UtcNow.ToString('o');event=$null;source=$null;result='pending';pid=$null}
$taskGuard=$null; $taskAcquired=$false
try {
  $taskInput=[Console]::In.ReadToEnd() | ConvertFrom-Json
  $taskEvent=[string]$taskInput.hook_event_name
  if ($taskEvent -notin @('SessionStart','UserPromptSubmit')) { exit 0 }
  $taskSource=if ($taskEvent -eq 'SessionStart') {[string]$taskInput.source} else {$null}
  if ($taskEvent -eq 'SessionStart' -and $taskSource -notin @('startup','resume')) { exit 0 }
  $taskRecord.event=$taskEvent; $taskRecord.source=$taskSource
  # Retain event metadata only. Never log the prompt or read the transcript.
  $taskConfigPath=Join-Path $DataDirectory 'config.json'
  $taskSettings=if (Test-Path -LiteralPath $taskConfigPath) {Get-Content -LiteralPath $taskConfigPath -Raw | ConvertFrom-Json} else {$null}
  if ($taskSettings -and $taskSettings.autoLaunchEnabled -eq $false) {$taskRecord.result='paused'; return}
  $taskDesktop=@(Get-Process -Name 'ChatGPT','Codex' -ErrorAction SilentlyContinue | Where-Object {try {$_.MainWindowHandle -ne 0 -and $_.Path -match 'WindowsApps[\\/]OpenAI\.Codex_'} catch {$false}})
  if ($taskDesktop.Count -eq 0 -and -not $DryRun) {$taskRecord.result='no-desktop-window'; return}
  $taskRecord.desktopPid=if ($taskDesktop.Count) {$taskDesktop[0].Id} else {$null}
  $taskRoot=Split-Path -Parent $PSScriptRoot
  $taskNode=if ($taskSettings -and (Test-Path -LiteralPath $taskSettings.nodePath -PathType Leaf)) {$taskSettings.nodePath} else {$null}
  if (-not $taskNode) {$taskCommand=Get-Command node.exe -ErrorAction SilentlyContinue; if ($taskCommand) {$taskNode=$taskCommand.Source}}
  if (-not $taskNode) {
    $taskBundled=Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'
    if (Test-Path -LiteralPath $taskBundled -PathType Leaf) {$taskNode=$taskBundled}
  }
  if (-not $taskNode) {throw 'Node.js 22.13+ is required'}
  $taskBinaries=@(Get-ChildItem -LiteralPath (Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\bin') -Filter 'codex.exe' -File -Recurse -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc -Descending)
  if ($taskBinaries.Count -eq 0) {throw 'Current Codex CLI is missing'}
  if ($DryRun) {
    $taskRecord.result='would-launch'; $taskRecord.codexBinary=$taskBinaries[0].FullName
    $taskRecord.skin=if ($taskSettings) {$taskSettings.skin} else {'codex'}
    $taskRecord.displayMode=if ($taskSettings) {$taskSettings.displayMode} else {'full'}
    return
  }
  $taskGuard=[Threading.Mutex]::new($false,'Local\mogoo-codex-status-launch')
  try {$taskAcquired=$taskGuard.WaitOne(2000)} catch [Threading.AbandonedMutexException] {$taskAcquired=$true}
  if (-not $taskAcquired) {$taskRecord.result='launch-already-in-progress'; return}
  $taskSettings=if (Test-Path -LiteralPath $taskConfigPath) {Get-Content -LiteralPath $taskConfigPath -Raw | ConvertFrom-Json} else {$null}
  if ($taskSettings -and $taskSettings.autoLaunchEnabled -eq $false) {$taskRecord.result='paused'; return}
  try {$taskExisting=[Threading.Mutex]::OpenExisting('Local\mogoo-codex-status'); $taskExisting.Dispose(); $taskRecord.result='already-running'; return}
  catch [Threading.WaitHandleCannotBeOpenedException] {}
  $taskApp=Join-Path $DataDirectory 'app'
  $taskPackage=Get-Content -LiteralPath (Join-Path $taskRoot 'package.json') -Raw | ConvertFrom-Json
  $taskInstalled=if (Test-Path -LiteralPath (Join-Path $taskApp 'package.json')) {Get-Content -LiteralPath (Join-Path $taskApp 'package.json') -Raw | ConvertFrom-Json} else {$null}
  if (-not $taskSettings -or -not $taskInstalled -or $taskInstalled.version -ne $taskPackage.version -or -not (Test-Path -LiteralPath (Join-Path $taskApp 'bin\MogooCodexStatus.exe'))) {
    $taskPrepared=& $taskNode (Join-Path $taskRoot 'scripts\control.mjs') prepare 2>$null
    if ($LASTEXITCODE -ne 0) {throw 'Runtime preparation failed'}
    $taskSettings=Get-Content -LiteralPath $taskConfigPath -Raw | ConvertFrom-Json
  }
  $taskSettings.codexBinary=$taskBinaries[0].FullName
  $taskSettings.nodePath=$taskNode
  [IO.File]::WriteAllText($taskConfigPath,($taskSettings | ConvertTo-Json -Depth 10 -Compress),[Text.UTF8Encoding]::new($false))
  $taskExecutable=Join-Path $taskApp 'bin\MogooCodexStatus.exe'
  $taskChild=Start-Process -FilePath $taskExecutable -ArgumentList @('--config',('"'+$taskConfigPath+'"'),'--desktop-pid',[string]$taskDesktop[0].Id) -WindowStyle Hidden -PassThru
  $taskRecord.pid=$taskChild.Id; $taskRecord.result='launch-requested'
  for ($taskAttempt=0; $taskAttempt -lt 10; $taskAttempt++) {
    try {$taskCreated=[Threading.Mutex]::OpenExisting('Local\mogoo-codex-status'); $taskCreated.Dispose(); break}
    catch [Threading.WaitHandleCannotBeOpenedException] {Start-Sleep -Milliseconds 100}
  }
} catch {
  $taskRecord.result='failed'; $taskRecord.errorType=$_.Exception.GetType().Name
} finally {
  if ($taskAcquired) {$taskGuard.ReleaseMutex()}
  if ($taskGuard) {$taskGuard.Dispose()}
  if ($taskRecord.event) {
    try {
      New-Item -ItemType Directory -Path $DataDirectory -Force | Out-Null
      # One bounded diagnostic record, never a message log.
      [IO.File]::WriteAllText((Join-Path $DataDirectory 'hook-status.json'),($taskRecord | ConvertTo-Json -Depth 4 -Compress),[Text.UTF8Encoding]::new($false))
    } catch {}
  }
}
exit 0
