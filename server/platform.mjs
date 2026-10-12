import path from 'node:path';
import os from 'node:os';
import {access,readdir,stat} from 'node:fs/promises';
import {constants} from 'node:fs';

export function supported(platform=process.platform) {return platform==='win32'||platform==='darwin';}
export function statusDataDir({platform=process.platform,home=os.homedir(),env=process.env}={}) {
  if(platform==='win32')return path.join(env.LOCALAPPDATA??path.join(home,'AppData','Local'),'mogoo-codex-status');
  if(platform==='darwin')return path.join(home,'Library','Application Support','mogoo-codex-status');
  throw new Error('Supported platforms are Windows and macOS.');
}
export function statusExecutable(root,platform=process.platform) {
  if(platform==='win32')return path.join(root,'bin','MogooCodexStatus.exe');
  if(platform==='darwin')return path.join(root,'bin','macos','MogooCodexStatus.app','Contents','MacOS','MogooCodexStatus');
  throw new Error('Supported platforms are Windows and macOS.');
}
async function executable(file) {try {await access(file,constants.X_OK);return file;}catch{return null;}}
export async function findCodex({platform=process.platform,home=os.homedir(),env=process.env,bundlePath=null}={}) {
  if(platform==='win32') {
    const base=path.join(env.LOCALAPPDATA??path.join(home,'AppData','Local'),'OpenAI','Codex','bin');
    const rows=await readdir(base,{withFileTypes:true}).catch(()=>[]);const candidates=[];
    for(const row of rows)if(row.isDirectory()) {const file=path.join(base,row.name,'codex.exe');const s=await stat(file).catch(()=>null);if(s?.isFile())candidates.push({file,time:s.mtimeMs});}
    return candidates.sort((a,b)=>b.time-a.time)[0]?.file??null;
  }
  if(platform==='darwin') {
    const bundles=[bundlePath,'/Applications/Codex.app',path.join(home,'Applications','Codex.app')].filter(Boolean);
    for(const bundle of bundles) {
      const resources=path.join(bundle,'Contents','Resources');
      const bundled=await executable(path.join(resources,'codex-cli','CodexCLI.app','Contents','MacOS','codex'));
      if(bundled)return bundled;
      const queue=[{directory:resources,depth:0}];
      while(queue.length) {
        const {directory,depth}=queue.shift();const rows=await readdir(directory,{withFileTypes:true}).catch(()=>[]);
        for(const row of rows) {const full=path.join(directory,row.name);if(row.isFile()&&row.name==='codex') {const found=await executable(full);if(found)return found;}
          if(row.isDirectory()&&depth<3&&!row.name.endsWith('.asar'))queue.push({directory:full,depth:depth+1});}
      }
    }
    for(const directory of (env.PATH??'').split(path.delimiter)) {const found=await executable(path.join(directory,'codex'));if(found)return found;}
  }
  return null;
}
