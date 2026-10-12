import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,writeFile,appendFile,rm} from 'node:fs/promises';
import path from 'node:path';import os from 'node:os';
import {UsageReader} from '../server/session-reader.mjs';
const token=(n)=>JSON.stringify({type:'event_msg',timestamp:'2026-10-10T09:00:00Z',payload:{type:'token_count',info:{last_token_usage:{total_tokens:n},model_context_window:1000},rate_limits:{primary:{used_percent:76}}}})+'\n';
test('partial UTF-8 records, appends, compaction and truncated files',async()=>{
  const dir=await mkdtemp(path.join(os.tmpdir(),'mogoo-status-test-'));const file=path.join(dir,'session.jsonl');
  try {
    await writeFile(file,token(100));const reader=new UsageReader(file);assert.equal((await reader.update()).info.last_token_usage.total_tokens,100);
    const next=Buffer.from(token(250));await appendFile(file,next.subarray(0,50));assert.equal((await reader.update()).info.last_token_usage.total_tokens,100);
    await appendFile(file,next.subarray(50));assert.equal((await reader.update()).info.last_token_usage.total_tokens,250);
    await appendFile(file,JSON.stringify({type:'compacted'})+'\n');assert.equal((await reader.update()).info,null);
    await appendFile(file,token(30));assert.equal((await reader.update()).info.last_token_usage.total_tokens,30);
    await writeFile(file,token(5));assert.equal((await reader.update()).info.last_token_usage.total_tokens,5);
  } finally {await rm(dir,{recursive:true,force:true});}
});
test('quoted event text in messages cannot overwrite statistics; huge tool output is discarded',()=>{
  const reader=new UsageReader('unused');reader.consume(Buffer.from(token(10)));
  reader.consume(Buffer.from(JSON.stringify({type:'response_item',payload:{type:'token_count',info:{last_token_usage:{total_tokens:9999}}}})+'\n'));
  reader.consume(Buffer.alloc(5*1024*1024,65));reader.consume(Buffer.from('\n'+token(50)));
  assert.equal(reader.info.last_token_usage.total_tokens,50);assert.equal(reader.pending.length,0);
});
