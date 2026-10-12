import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import { DatabaseSync } from 'node:sqlite';
import { selectThread } from './metrics.mjs';
export class ThreadRegistry {
  constructor(home) { this.home = home; this.db = null; this.dbPath = null; }
  async resolve(title) {
    const candidates = (await readdir(this.home)).filter(f => /^state_\d+\.sqlite$/.test(f)).sort((a,b) => Number(b.match(/\d+/)[0])-Number(a.match(/\d+/)[0]));
    if (!candidates.length) return { status: 'database-unavailable', thread: null };
    const dbPath = path.join(this.home,candidates[0]);
    if (this.dbPath !== dbPath) { this.db?.close(); this.db = new DatabaseSync(dbPath,{ readOnly: true }); this.dbPath = dbPath; this.db.exec('PRAGMA query_only=ON; PRAGMA busy_timeout=1000;'); }
    const columns = new Set(this.db.prepare('PRAGMA table_info(threads)').all().map(r => r.name));
    if (!['id','rollout_path','cwd'].every(c => columns.has(c))) return { status:'database-unsupported', thread:null };
    const name = columns.has('name') ? 'name' : "'' AS name";
    const model = columns.has('model') ? 'model' : "NULL AS model";
    // Never select first_user_message, previews, or conversation content.
    const threads = this.db.prepare(`SELECT id, ${name}, ${model}, rollout_path, cwd FROM threads`).all();
    let aliases = [];
    try { aliases = (await readFile(path.join(this.home,'session_index.jsonl'),'utf8')).split('\n').flatMap(l => { try { const r = JSON.parse(l); return typeof r.id === 'string' && typeof r.thread_name === 'string' ? [r] : []; } catch { return []; } }); } catch {}
    return selectThread(threads,aliases,title);
  }
  close() { this.db?.close(); }
}
