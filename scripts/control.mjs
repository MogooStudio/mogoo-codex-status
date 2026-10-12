import { spawn } from 'node:child_process';
import { cp, mkdir, readdir, stat, unlink, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
import { readJson, writeJson } from '../server/io.mjs';
export const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
export const dataDir=path.join(process.env.LOCALAPPDATA ?? path.join(os.homedir(),'AppData','Local'),'mogoo-codex-status');
const configPath=path.join(dataDir,'config.json');
async function run(file,args) {
  return new Promise((resolve,reject)=>{const child=spawn(file,args,{windowsHide:true,stdio:['ignore','pipe','pipe']});let out='',err='';child.stdout.on('data',d=>out+=d);child.stderr.on('data',d=>err+=d);child.on('error',reject);child.on('exit',code=>code===0?resolve(out):reject(new Error(`Command failed (${code}): ${err.slice(0,1500)}`)));});
}
async function findCodex() {
  const base=path.join(process.env.LOCALAPPDATA ?? '', 'OpenAI','Codex','bin');
  const entries=await readdir(base,{withFileTypes:true}).catch(()=>[]);
  const candidates=[];for(const item of entries)if(item.isDirectory()) {const file=path.join(base,item.name,'codex.exe');const s=await stat(file).catch(()=>null);if(s)candidates.push({file,time:s.mtimeMs});}
  return candidates.sort((a,b)=>b.time-a.time)[0]?.file ?? null;
}
async function configFor(pluginRoot=root) {
  const previous=await readJson(configPath,{});
  return {...previous,version:1,dataDir,pluginRoot,nodePath:process.execPath,codexHome:process.env.CODEX_HOME ?? path.join(os.homedir(),'.codex'),codexBinary:await findCodex()};
}
async function setAutomatic(enabled) {const c=await configFor(path.join(dataDir,'app'));c.autoLaunchEnabled=enabled;await writeJson(configPath,c);return c;}
async function requestStop() {
  await mkdir(dataDir,{recursive:true});await writeFile(path.join(dataDir,'stop.signal'),'stop');
  const runtime=await readJson(path.join(dataDir,'runtime.json'));if(!runtime?.pid)return;
  for(let attempt=0;attempt<30;attempt++) {try{process.kill(runtime.pid,0);}catch{return;}await new Promise(r=>setTimeout(r,200));}
  throw new Error('Status strip has not stopped; runtime files were not replaced.');
}
async function prepare() {
  await requestStop();const appRoot=path.join(dataDir,'app');await mkdir(appRoot,{recursive:true});
  if(path.resolve(root).toLowerCase()!==path.resolve(appRoot).toLowerCase()) {
    for(const folder of ['bin','server','scripts'])await cp(path.join(root,folder),path.join(appRoot,folder),{recursive:true});
    await cp(path.join(root,'package.json'),path.join(appRoot,'package.json'));
  }
  await unlink(path.join(appRoot,'scripts','autostart.ps1')).catch(error=>{if(error.code!=='ENOENT')throw error;});
  const config=await configFor(appRoot);if(config.autoLaunchEnabled===undefined)config.autoLaunchEnabled=true;
  await writeJson(configPath,config);return config;
}
async function launch(config) {
  const executable=path.join(config.pluginRoot,'bin','MogooCodexStatus.exe');
  if(!existsSync(executable))throw new Error('Native executable is missing; run pnpm build.');
  const child=spawn(executable,['--config',configPath],{detached:true,windowsHide:true,stdio:'ignore'});
  await new Promise((resolve,reject)=>{child.once('spawn',resolve);child.once('error',reject);});child.unref();
}
export async function inspect() {
  await mkdir(dataDir,{recursive:true});
  const file=path.join(dataDir,'inspection.json');
  await run(path.join(root,'bin','MogooCodexStatus.exe'),['--inspect',file]);
  return readJson(file);
}
export async function control(command) {
  if(process.platform!=='win32')throw new Error('This release supports Windows only.');
  if(command==='inspect')return inspect();
  if(command==='stop' || command==='uninstall') {await setAutomatic(false);await requestStop();return {status:command==='stop'?'paused':'uninstalled',autoLaunchEnabled:false,settingsPreserved:true};}
  if(command==='prepare') {await prepare();return {status:'prepared',dataDir,startup:'codex-hooks'};}
  if(command==='install') {
    await prepare();const config=await setAutomatic(true);
    await launch(config);return {status:'installed',dataDir,startup:'codex-hooks',autoLaunchEnabled:true};
  }
  if(command==='start') {await prepare();const config=await setAutomatic(true);await launch(config);return {status:'started',dataDir,autoLaunchEnabled:true};}
  if(command==='status')return {config:await readJson(configPath),state:await readJson(path.join(dataDir,'state.json')),selection:await readJson(path.join(dataDir,'selection.json')),runtime:await readJson(path.join(dataDir,'runtime.json'))};
  throw new Error('Use prepare, install, uninstall, start, stop, inspect, or status.');
}
if(process.argv[1] && path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  try {console.log(JSON.stringify(await control(process.argv[2] ?? 'status'),null,2));}catch(error){console.error(error.message);process.exitCode=1;}
}
