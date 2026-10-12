import {mkdir,readFile,readdir,unlink,writeFile} from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const manifest=JSON.parse(await readFile(path.join(root,'codex','plugin.json'),'utf8'));
const pkg=JSON.parse(await readFile(path.join(root,'package.json'),'utf8'));
assert.equal(manifest.name,pkg.name);assert.equal(manifest.version,pkg.version);
const check=process.argv.includes('--check');const requireMac=process.argv.includes('--require-macos');const plugin=path.join(root,'plugins',pkg.name);
const catalog={name:pkg.name,interface:{displayName:pkg.name},plugins:[{name:pkg.name,source:{source:'local',path:`./plugins/${pkg.name}`},policy:{installation:'AVAILABLE',authentication:'ON_INSTALL'},category:'Developer Tools'}]};
// The current Codex client discovers bundled hooks through the native manifest.
const overlay={name:pkg.name,version:pkg.version,description:manifest.description,author:manifest.author,homepage:manifest.homepage,repository:manifest.repository,license:pkg.license,skills:'./skills/',hooks:'./hooks/hooks.json',interface:manifest.extensions['com.openai'].interface};
const files=new Map([['.codex-plugin/plugin.json',Buffer.from(JSON.stringify(overlay,null,2)+'\n')],['package.json',Buffer.from(JSON.stringify({...pkg,scripts:{}},null,2)+'\n')]]);
async function collect(source,prefix,skip=()=>false) {for(const entry of await readdir(source,{withFileTypes:true})) {if(skip(entry.name))continue;const full=path.join(source,entry.name),dest=path.posix.join(prefix,entry.name);if(entry.isSymbolicLink())throw new Error('Symlink not supported');if(entry.isDirectory())await collect(full,dest,skip);else files.set(dest,await readFile(full));}}
// Each host builds its native app; the other host's verified artifact stays bundled.
for(const relative of ['MogooCodexStatus.exe','macos/MogooCodexStatus.app.zip']) {
  let content=await readFile(path.join(root,'bin',relative)).catch(error=>{if(error.code==='ENOENT')return null;throw error;});
  if(!content)content=await readFile(path.join(plugin,'bin',relative)).catch(error=>{if(error.code==='ENOENT')return null;throw error;});
  if(content)files.set('bin/'+relative,content);
  else if(relative.endsWith('.exe')||requireMac)throw new Error('Missing native distribution artifact: '+relative);
}
await collect(path.join(root,'server'),'server');
await collect(path.join(root,'scripts'),'scripts',name=>['build.ps1','build-macos.sh','package-plugin.mjs','render-preview.mjs','render-macos.mjs','test-native-drag.ps1','test-native-skins.ps1'].includes(name));
await collect(path.join(root,'codex','skills'),'skills');await collect(path.join(root,'codex','assets'),'assets');
await collect(path.join(root,'codex','hooks'),'hooks');
await collect(path.join(root,'docs','images'),'docs/images');
files.set('docs/skin-preview.html',await readFile(path.join(root,'docs','skin-preview.html')));
files.set('README.md',await readFile(path.join(root,'codex','PLUGIN-README.md')));
files.set('README.zh-CN.md',Buffer.from((await readFile(path.join(root,'README.zh-CN.md'),'utf8')).replaceAll('](codex/PLUGIN-README.md)','](README.md)')));
files.set('LICENSE',await readFile(path.join(root,'LICENSE')));
async function onDisk(directory,prefix='') {const names=[];for(const entry of await readdir(directory,{withFileTypes:true})) {const rel=path.posix.join(prefix,entry.name);if(entry.isDirectory())names.push(...await onDisk(path.join(directory,entry.name),rel));else names.push(rel);}return names.sort();}
if(check) {
  assert.deepEqual(await onDisk(plugin),[...files.keys()].sort());
  for(const [relative,content] of files)assert.deepEqual(await readFile(path.join(plugin,relative)),content,`Stale: ${relative}`);
  assert.deepEqual(JSON.parse(await readFile(path.join(root,'.agents','plugins','marketplace.json'),'utf8')),catalog);
  console.log('Marketplace package matches source and native build.');
} else {
  await mkdir(plugin,{recursive:true});
  // Remove only these obsolete generated files; never delete a directory tree.
  for(const relative of ['plugin.json','scripts/autostart.ps1'])await unlink(path.join(plugin,relative)).catch(error=>{if(error.code!=='ENOENT')throw error;});
  // Never recursively delete generated or user directories when synchronizing.
  for(const [relative,content] of files) {const target=path.join(plugin,relative);await mkdir(path.dirname(target),{recursive:true});await writeFile(target,content);}
  await mkdir(path.join(root,'.agents','plugins'),{recursive:true});await writeFile(path.join(root,'.agents','plugins','marketplace.json'),JSON.stringify(catalog,null,2)+'\n');
  console.log(JSON.stringify({name:pkg.name,version:pkg.version,pluginRoot:plugin,files:files.size}));
}
