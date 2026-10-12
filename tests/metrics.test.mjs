import test from 'node:test';
import assert from 'node:assert/strict';
import {contextMetric,limitMetrics,selectThread,windowLabel} from '../server/metrics.mjs';
test('context uses the most recent request, never cumulative consumption or subtracting cached input',()=>{
  assert.equal(contextMetric({last_token_usage:{total_tokens:250},total_token_usage:{total_tokens:9000},model_context_window:1000},'now').usedPercent,25);
  assert.equal(contextMetric({last_token_usage:{total_tokens:0},model_context_window:1000}).usedPercent,0);
  assert.equal(contextMetric({last_token_usage:{total_tokens:100},model_context_window:0}),null);
  assert.equal(contextMetric({model_context_window:1000}),null);
});
test('real window durations and multiple buckets; unavailable is not zero',()=>{
  const limits=limitMetrics({rateLimitsByLimitId:{codex:{primary:{usedPercent:76,windowDurationMins:10080,resetsAt:1791960668},secondary:null},extra:{primary:{usedPercent:101,windowDurationMins:300},secondary:{usedPercent:null}}}},'now','live');
  assert.equal(limits.length,2);assert.equal(limits[0].remainingPercent,24);assert.equal(windowLabel(limits[0].windowMinutes),'7天');assert.equal(limits[1].remainingPercent,0);assert.equal(limits[1].resetsAt,null);
  assert.equal(limitMetrics({primary:{used_percent:0,window_minutes:15,resets_at:2000000000}},'now')[0].remainingPercent,100);
  assert.deepEqual(limitMetrics({primary:{usedPercent:null}}),[]);
});
test('resolve visible title through historical renames and never choose newest ambiguous thread',()=>{
  const rows=[{id:'a',name:'新名字 · 10-10 17:30'},{id:'b',name:'其他聊天'}];
  assert.equal(selectThread(rows,[{id:'a',thread_name:'旧名字 · 10-10 17:08'}],'旧名字 · 10-10 17:08').thread.id,'a');
  assert.equal(selectThread(rows,[{id:'a',thread_name:'旧名字 · 10-10 17:08'}],'旧名字 · 10-10 17:09').thread.id,'a');
  assert.equal(selectThread([{id:'a',name:'重名'},{id:'b',name:'重名'}],[],'重名').status,'ambiguous');
  assert.equal(selectThread(rows,[],'未知').thread,null);
});
