import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {mkdir} from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {writeJson} from '../server/io.mjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');const binary=process.argv[2];
const data=path.join(root,'.tmp','macos-preview');const output=path.join(root,'docs','images','macos');await mkdir(output,{recursive:true});
const run=promisify(execFile);
for(const skin of ['codex','codex-light'])for(const displayMode of ['full','compact']) {
  const now=Date.now();await writeJson(path.join(data,'config.json'),{dataDir:data,skin,displayMode,previewWidth:480});
  await writeJson(path.join(data,'state.json'),{updatedAt:new Date(now).toISOString(),context:{usedPercent:65,usedTokens:166400,capacityTokens:256000,updatedAt:new Date(now).toISOString()},limits:[{name:'codex',label:'7天',remainingPercent:24,usedPercent:76,resetsAt:now/1000+3*86400+18*3600,stale:false,updatedAt:new Date(now).toISOString()}]});
  await run(binary,['--config',path.join(data,'config.json'),'--render',path.join(output,`${skin}-${displayMode}.png`)],{timeout:20000});
}
console.log('Rendered macOS light/dark and full/compact native panels.');
