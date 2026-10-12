import test from 'node:test';
import assert from 'node:assert/strict';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {mkdtemp,readFile,writeFile,mkdir,readdir,realpath,rm} from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile);
const control=fileURLToPath(new URL('../scripts/control.mjs',import.meta.url));
const hook=fileURLToPath(new URL('../codex/hooks/launch-status.ps1',import.meta.url));
const json=async file=>JSON.parse(await readFile(file,'utf8'));
async function fixture() {
  const dir=await mkdtemp(path.join(os.tmpdir(),'mogoo-status-lifecycle-'));
  const data=path.join(dir,'mogoo-codex-status');await mkdir(data,{recursive:true});
  const cli=path.join(dir,'OpenAI','Codex','bin','current','codex.exe');await mkdir(path.dirname(cli),{recursive:true});await writeFile(cli,'fixture');
  return {dir,data,cli,env:{...process.env,LOCALAPPDATA:dir}};
}
async function cleanup(dir) {assert.equal(path.dirname(dir),os.tmpdir());assert.ok(path.basename(dir).startsWith('mogoo-status-lifecycle-'));await rm(dir,{recursive:true,force:true});}
test('runtime setup, repeated update and pause preserve UI settings without a Windows startup side effect',{skip:process.platform!=='win32'},async()=>{
  const f=await fixture();
  const snapshot=async()=>JSON.parse((await run('powershell.exe',['-NoProfile','-NonInteractive','-Command',"$k=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\\Microsoft\\Windows\\CurrentVersion\\Run'); $v=@{}; if($k){foreach($n in $k.GetValueNames()){$v[$n]=@{kind=$k.GetValueKind($n).ToString();value=$k.GetValue($n,$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)}};$k.Dispose()}; $v | ConvertTo-Json -Depth 4"],{windowsHide:true})).stdout);
  try {
    const before=await snapshot();
    await writeFile(path.join(f.data,'config.json'),JSON.stringify({skin:'codex-light',displayMode:'compact',offsetRight:300,offsetTop:5,autoLaunchEnabled:false}));
    for(let i=0;i<2;i++) {
      await run(process.execPath,[control,'prepare'],{env:f.env,windowsHide:true});
      const c=await json(path.join(f.data,'config.json'));
      assert.equal(c.skin,'codex-light');assert.equal(c.displayMode,'compact');assert.equal(c.offsetRight,300);assert.equal(c.autoLaunchEnabled,false);
      assert.equal(c.codexBinary,f.cli);assert.equal(c.pluginRoot,path.join(f.data,'app'));
    }
    const oldScript=path.join(f.data,'app','scripts','autostart.ps1');await writeFile(oldScript,'obsolete fixture');
    await run(process.execPath,[control,'prepare'],{env:f.env,windowsHide:true});
    assert.ok(!(await readdir(path.dirname(oldScript))).includes('autostart.ps1'));
    for(const command of ['stop','uninstall']) {await run(process.execPath,[control,command],{env:f.env,windowsHide:true});assert.equal((await json(path.join(f.data,'config.json'))).autoLaunchEnabled,false);}
    assert.deepEqual(await snapshot(),before);
  } finally {await cleanup(f.dir);}
});
test('hooks select session/prompt events, honor pause and retain no prompt or transcript',{skip:process.platform!=='win32'},async()=>{
  const f=await fixture();const record=path.join(f.data,'hook-status.json');
  const submit=input=>new Promise((resolve,reject)=>{const child=execFile('powershell.exe',['-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',hook,'-DryRun','-DataDirectory',f.data],{env:f.env,windowsHide:true},(error,stdout,stderr)=>error?reject(error):resolve({stdout,stderr}));child.stdin.end(JSON.stringify({...input,prompt:'PRIVATE_PROMPT_SENTINEL',transcript_path:'PRIVATE_TRANSCRIPT_SENTINEL'}));});
  try {
    await writeFile(path.join(f.data,'config.json'),JSON.stringify({nodePath:process.execPath,skin:'codex-light',displayMode:'compact',autoLaunchEnabled:true}));
    for(const input of [{hook_event_name:'SessionStart',source:'startup'},{hook_event_name:'SessionStart',source:'resume'},{hook_event_name:'UserPromptSubmit'}]) {
      assert.deepEqual(await submit(input),{stdout:'',stderr:''});const text=await readFile(record,'utf8');assert.ok(!text.includes('PRIVATE_'));
      const r=JSON.parse(text);assert.equal(r.result,'would-launch');assert.equal(await realpath(r.codexBinary),await realpath(f.cli));assert.equal(r.skin,'codex-light');assert.equal(r.pid,null);
    }
    const last=await readFile(record,'utf8');for(const input of [{hook_event_name:'SessionStart',source:'compact'},{hook_event_name:'Stop'}]) {await submit(input);assert.equal(await readFile(record,'utf8'),last);}
    await writeFile(path.join(f.data,'config.json'),JSON.stringify({autoLaunchEnabled:false}));await submit({hook_event_name:'UserPromptSubmit'});assert.equal((await json(record)).result,'paused');
    assert.deepEqual((await readdir(f.data)).sort(),['config.json','hook-status.json']);
  } finally {await cleanup(f.dir);}
});
