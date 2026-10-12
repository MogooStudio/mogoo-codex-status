import path from 'node:path';
import { parseArgs } from 'node:util';
import { readJson, writeJson } from './io.mjs';
import { UsageReader } from './session-reader.mjs';
import { ThreadRegistry } from './registry.mjs';
import { QuotaClient } from './app-server.mjs';
import { contextMetric, limitMetrics, windowLabel } from './metrics.mjs';

const { values } = parseArgs({options:{config:{type:'string'},once:{type:'boolean',default:false},'parent-pid':{type:'string'}}});
const config = await readJson(values.config);
if (!config?.dataDir || !config?.codexHome) throw new Error('invalid-configuration');
const registry = new ThreadRegistry(config.codexHome);
const quota = config.codexBinary ? new QuotaClient(config.codexBinary,config.codexHome) : null;
const readers = new Map();
let latestQuota = await readJson(path.join(config.dataDir,'quota.json'));
let refreshing = false, nextRefresh = 0, stopping = false, refreshMarker = '';

async function refreshQuota() {
  if (!quota || refreshing || Date.now()<nextRefresh) return;
  refreshing=true; nextRefresh=Date.now()+60000;
  try {
    const result = await quota.read();
    const at = new Date().toISOString();
    const limits = limitMetrics(result,at,'live');
    // Persist statistics only, never account IDs, credentials, balances, or reset-credit IDs.
    latestQuota={limits,updatedAt:at};
    await writeJson(path.join(config.dataDir,'quota.json'),latestQuota);
  } catch { quota.close(); } finally { refreshing=false; }
}
async function tick() {
  if(values['parent-pid']) {try {process.kill(Number(values['parent-pid']),0);} catch {await stop();}}
  const marker=await readJson(path.join(config.dataDir,'refresh.json'));
  if(marker?.at && marker.at!==refreshMarker) {refreshMarker=marker.at;nextRefresh=0;}
  const selection = await readJson(path.join(config.dataDir,'selection.json'));
  const fresh = selection && Date.now()-Date.parse(selection.updatedAt)<10000;
  let status='no-window', thread=null, context=null, snapshot=null;
  const header=fresh && selection.source==='header-ocr' && Array.isArray(selection.titleCandidates);
  if(fresh && (header || (selection.title && selection.codexMode))) {
    try {
      const selected = header ? await registry.resolveHeader(selection.titleCandidates) : await registry.resolve(selection.title); status=selected.status; thread=selected.thread;
      if(thread) {
        let reader=readers.get(thread.rollout_path);
        if(!reader) {reader=new UsageReader(thread.rollout_path);readers.set(thread.rollout_path,reader); if(readers.size>8) readers.delete(readers.keys().next().value);}
        const usage=await reader.update();
        context=contextMetric(usage.info,usage.contextAt);
        snapshot={limits:limitMetrics(usage.rate,usage.rateAt),updatedAt:usage.rateAt};
      }
    } catch {status='data-unavailable';thread=null;}
    if(!values.once) void refreshQuota();
  }
  let activeQuota = latestQuota;
  if(snapshot?.updatedAt && (!activeQuota?.updatedAt || Date.parse(snapshot.updatedAt)>Date.parse(activeQuota.updatedAt))) activeQuota=snapshot;
  const limits=(activeQuota?.limits ?? []).map(w => ({...w,label:windowLabel(w.windowMinutes),stale:Date.now()-Date.parse(w.updatedAt)>120000,resetPending:w.resetsAt !== null && w.resetsAt*1000<=Date.now()}));
  const state={version:1,updatedAt:new Date().toISOString(),selectedTitle:selection?.title ?? '',selectionStatus:status,thread:thread ? {id:thread.id,title:header?(thread.name||selection.title):selection.title,model:thread.model} : null,context,limits};
  await writeJson(path.join(config.dataDir,'state.json'),state);
  return state;
}
async function stop() { if(stopping)return;stopping=true;quota?.close();registry.close();process.exit(0); }
process.on('SIGTERM',stop);process.on('SIGINT',stop);
if(!values.once) {process.stdin.setEncoding('utf8');process.stdin.on('data',value=>{if(value.trim()==='stop')void stop();});}
if(values.once) await refreshQuota();
await tick();
if(values.once) {quota?.close();registry.close();}
else { while(!stopping) { await new Promise(r=>setTimeout(r,1000)); try {await tick();} catch {} } }
