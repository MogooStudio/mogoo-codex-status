import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,mkdir,writeFile,chmod,rm} from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import {statusDataDir,statusExecutable,findCodex,supported} from '../server/platform.mjs';
import {hookEvent} from '../server/hook-event.mjs';
test('platform paths isolate Windows and macOS user data and executable formats',()=>{
  assert.equal(statusDataDir({platform:'win32',home:'user',env:{LOCALAPPDATA:'local'}}),path.join('local','mogoo-codex-status'));
  assert.equal(statusDataDir({platform:'darwin',home:'user',env:{LOCALAPPDATA:'wrong'}}),path.join('user','Library','Application Support','mogoo-codex-status'));
  assert.ok(statusExecutable('root','darwin').endsWith(path.join('MogooCodexStatus.app','Contents','MacOS','MogooCodexStatus')));
  assert.ok(statusExecutable('root','win32').endsWith('MogooCodexStatus.exe'));assert.equal(supported('linux'),false);
});
test('macOS CLI discovery prefers the actual running bundle over another installation',async()=>{
  const directory=await mkdtemp(path.join(os.tmpdir(),'mogoo-status-platform-'));
  try {
    const bundle=path.join(directory,'Custom Applications','Codex.app');const binary=path.join(bundle,'Contents','Resources','bin','codex');
    await mkdir(path.dirname(binary),{recursive:true});await writeFile(binary,'fixture');await chmod(binary,0o755);
    assert.equal(await findCodex({platform:'darwin',home:directory,env:{PATH:''},bundlePath:bundle}),binary);
  } finally {assert.equal(path.dirname(directory),os.tmpdir());assert.ok(path.basename(directory).startsWith('mogoo-status-platform-'));await rm(directory,{recursive:true,force:true});}
});
test('macOS chat hook selection is independent of prompt text and never uses compact or Stop',()=>{
  assert.deepEqual(hookEvent({hook_event_name:'UserPromptSubmit',prompt:'private'}),{event:'UserPromptSubmit',source:null});
  assert.deepEqual(hookEvent({hook_event_name:'SessionStart',source:'resume'}),{event:'SessionStart',source:'resume'});
  assert.equal(hookEvent({hook_event_name:'SessionStart',source:'compact'}),null);assert.equal(hookEvent({hook_event_name:'Stop'}),null);
});
test('macOS 优先使用当前 ChatGPT.app 内部嵌套的 CodexCLI',async()=>{
  const directory=await mkdtemp(path.join(os.tmpdir(),'mogoo-status-platform-'));
  try {
    const bundle=path.join(directory,'ChatGPT.app');
    const binary=path.join(bundle,'Contents','Resources','codex-cli','CodexCLI.app','Contents','MacOS','codex');
    const fallback=path.join(directory,'old-cli','codex');
    for(const file of [binary,fallback]) {await mkdir(path.dirname(file),{recursive:true});await writeFile(file,'fixture');await chmod(file,0o755);}
    assert.equal(await findCodex({platform:'darwin',home:directory,env:{PATH:path.dirname(fallback)},bundlePath:bundle}),binary);
  } finally {assert.equal(path.dirname(directory),os.tmpdir());assert.ok(path.basename(directory).startsWith('mogoo-status-platform-'));await rm(directory,{recursive:true,force:true});}
});
