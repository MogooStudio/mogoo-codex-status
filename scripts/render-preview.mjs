import {mkdir,copyFile} from 'node:fs/promises';import path from 'node:path';import {spawn} from 'node:child_process';import {fileURLToPath} from 'node:url';
import {writeJson} from '../server/io.mjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');const dataDir=path.join(root,'.tmp','preview');await mkdir(dataDir,{recursive:true});
const output=path.join(root,'docs','images','skins');await mkdir(output,{recursive:true});
const matrix=process.argv.includes('--matrix');
for(const skin of ['codex','codex-light']) {
 for(const width of matrix ? [280,360,480,560] : [560]) {
  for(const status of matrix ? ['normal','warning','waiting'] : ['normal']) {
   const at=new Date().toISOString(),now=Math.floor(Date.now()/1000);await writeJson(path.join(dataDir,'config.json'),{dataDir,skin,previewWidth:width});
   await writeJson(path.join(dataDir,'state.json'),{updatedAt:at,selectionStatus:'matched',thread:{title:'mogoo-codex-status · 预览'},context:status==='waiting' ? null : {usedTokens:status==='warning' ? 245520 : 87704,capacityTokens:258400,usedPercent:status==='warning' ? 95.02 : 33.94,updatedAt:at},limits:[{name:'codex',label:'5小时',remainingPercent:status==='warning' ? 3 : 87,usedPercent:status==='warning' ? 97 : 13,resetsAt:now+2*3600+18*60+40,updatedAt:at},{name:'codex',label:'7天',remainingPercent:24,usedPercent:76,resetsAt:now+3*86400+18*3600,updatedAt:at}]});
   const file=width===560 && status==='normal' ? path.join(output,`${skin}.png`) : path.join(dataDir,'skins',`${skin}-${width}-${status}.png`);
   await new Promise((resolve,reject)=>{const child=spawn(path.join(root,'bin','MogooCodexStatus.exe'),['--config',path.join(dataDir,'config.json'),'--render',file],{windowsHide:true,stdio:'ignore'});child.on('error',reject);child.on('exit',code=>code===0?resolve():reject(new Error(`Render failed: ${skin}/${width}/${status}`)));});
  }
 }
 console.log(`Rendered ${skin}${matrix ? ' · widths 280/360/480/560, normal/warning/waiting' : ''}`);
}
for(const skin of ['codex','codex-light']) {
 const at=new Date().toISOString();await writeJson(path.join(dataDir,'config.json'),{dataDir,skin,previewWidth:480});
 await writeJson(path.join(dataDir,'state.json'),{updatedAt:at,context:{usedPercent:33.94,usedTokens:87704,capacityTokens:258400,updatedAt:at},limits:[{name:'codex',label:'7天',remainingPercent:24,usedPercent:76,resetsAt:Math.floor(Date.now()/1000)+3*86400+18*3600+40,updatedAt:at}]});
 await new Promise((resolve,reject)=>{const child=spawn(path.join(root,'bin','MogooCodexStatus.exe'),['--config',path.join(dataDir,'config.json'),'--render',path.join(output,`${skin}-single.png`)],{windowsHide:true,stdio:'ignore'});child.on('error',reject);child.on('exit',code=>code===0?resolve():reject(new Error(`Single-limit render failed: ${skin}`)));});
 await writeJson(path.join(dataDir,'config.json'),{dataDir,skin,displayMode:'compact'});
 await new Promise((resolve,reject)=>{const child=spawn(path.join(root,'bin','MogooCodexStatus.exe'),['--config',path.join(dataDir,'config.json'),'--render',path.join(output,`${skin}-compact.png`)],{windowsHide:true,stdio:'ignore'});child.on('error',reject);child.on('exit',code=>code===0?resolve():reject(new Error(`Compact render failed: ${skin}`)));});
}
await copyFile(path.join(output,'codex.png'),path.join(root,'docs','images','status-strip.png'));
console.log(output);
