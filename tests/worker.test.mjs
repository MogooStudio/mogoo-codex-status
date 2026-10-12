import test from 'node:test';
import assert from 'node:assert/strict';
import {DatabaseSync} from 'node:sqlite';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {mkdtemp,writeFile,readFile,rm} from 'node:fs/promises';
import os from 'node:os';import path from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile);
test('background activity cannot replace the visible chat; persisted state contains statistics only',async()=>{
  const dir=await mkdtemp(path.join(os.tmpdir(),'mogoo-status-worker-test-'));
  try {
    const db=new DatabaseSync(path.join(dir,'state_5.sqlite'));
    db.exec('CREATE TABLE threads (id TEXT, name TEXT, title TEXT, model TEXT, rollout_path TEXT, cwd TEXT)');
    const insert=db.prepare('INSERT INTO threads VALUES (?,?,?,?,?,?)');
    const visible=path.join(dir,'visible.jsonl'),background=path.join(dir,'background.jsonl');
    insert.run('visible','renamed','PRIVATE FIRST MESSAGE','model',visible,dir);insert.run('background','background','SECRET PROMPT','model',background,dir);db.close();
    const now=new Date().toISOString();
    await writeFile(visible,JSON.stringify({type:'event_msg',timestamp:now,payload:{type:'token_count',info:{last_token_usage:{total_tokens:250},model_context_window:1000},rate_limits:{primary:{used_percent:77,window_minutes:10080,resets_at:2000000000}}}})+'\n'+JSON.stringify({type:'response_item',payload:{text:'PRIVATE CONVERSATION CONTENT'}})+'\n');
    await writeFile(background,JSON.stringify({type:'event_msg',timestamp:now,payload:{type:'token_count',info:{last_token_usage:{total_tokens:999},model_context_window:1000}}})+'\n');
    await writeFile(path.join(dir,'session_index.jsonl'),JSON.stringify({id:'visible',thread_name:'visible old title'})+'\n');
    await writeFile(path.join(dir,'selection.json'),JSON.stringify({updatedAt:now,title:'visible old title',codexMode:true}));
    const config=path.join(dir,'config.json');await writeFile(config,JSON.stringify({dataDir:dir,codexHome:dir}));
    await run(process.execPath,['--no-warnings',fileURLToPath(new URL('../server/worker.mjs',import.meta.url)),'--config',config,'--once']);
    const raw=await readFile(path.join(dir,'state.json'),'utf8');const state=JSON.parse(raw);
    assert.equal(state.thread.id,'visible');assert.equal(state.context.usedPercent,25);assert.equal(state.limits[0].remainingPercent,23);
    assert.ok(!raw.includes('PRIVATE') && !raw.includes('SECRET') && !raw.includes(background));
  } finally { assert.equal(path.dirname(dir),os.tmpdir());assert.ok(path.basename(dir).startsWith('mogoo-status-worker-test-'));await rm(dir,{recursive:true,force:true}); }
});
