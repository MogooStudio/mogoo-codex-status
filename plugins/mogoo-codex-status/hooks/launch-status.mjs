import path from 'node:path';
import {mkdir,stat,unlink,rmdir} from 'node:fs/promises';
import {control,dataDir,desktopInfo,launch} from '../scripts/control.mjs';
import {readJson,writeJson} from '../server/io.mjs';
import {hookEvent} from '../server/hook-event.mjs';
import {pathToFileURL} from 'node:url';

const alive=pid=>{try {if(!Number.isInteger(pid)||pid<=0)return false;process.kill(pid,0);return true;}catch{return false;}};
async function main() {
  let input='';for await(const chunk of process.stdin)input+=chunk;
  const event=hookEvent(JSON.parse(input));input='';if(!event)return;
  const record={schemaVersion:1,origin:'codex-hook',receivedAtUtc:new Date().toISOString(),...event,result:'pending',pid:null};
  const guard=path.join(dataDir,'launch.guard');let locked=false;
  try {
    await mkdir(dataDir,{recursive:true});
    let config=await readJson(path.join(dataDir,'config.json'));
    if(config?.autoLaunchEnabled===false) {record.result='paused';return;}
    try {await mkdir(guard);locked=true;}catch(error) {
      if(error.code!=='EEXIST')throw error;
      const owner=await readJson(path.join(guard,'owner.json'));const age=Date.now()-(await stat(guard)).mtimeMs;
      if(alive(owner?.pid)||age<15000) {record.result='launch-already-in-progress';return;}
      await unlink(path.join(guard,'owner.json')).catch(()=>{});await rmdir(guard);await mkdir(guard);locked=true;
    }
    await writeJson(path.join(guard,'owner.json'),{pid:process.pid});
    config=await readJson(path.join(dataDir,'config.json'));
    if(config?.autoLaunchEnabled===false) {record.result='paused';return;}
    const runtime=await readJson(path.join(dataDir,'runtime.json'));
    if(alive(runtime?.pid)&&Date.now()-Date.parse(runtime.updatedAt)<10000) {record.result='already-running';return;}
    await control('prepare');config=await readJson(path.join(dataDir,'config.json'));
    const desktop=await desktopInfo(config.pluginRoot);
    if(!desktop.pid) {record.result='no-desktop-window';return;}
    record.desktopPid=desktop.pid;record.pid=await launch(config,desktop);record.result='launch-requested';
    // Retain the guard until the native single-instance lock and heartbeat exist.
    for(let i=0;i<15;i++) {const status=await readJson(path.join(dataDir,'runtime.json'));if(status?.pid===record.pid)break;await new Promise(r=>setTimeout(r,100));}
  } catch(error) {record.result='failed';record.errorType=error.name;} finally {
    if(locked) {await unlink(path.join(guard,'owner.json')).catch(()=>{});await rmdir(guard).catch(()=>{});}
    await writeJson(path.join(dataDir,'hook-status.json'),record).catch(()=>{});
  }
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href)await main().catch(()=>{});
