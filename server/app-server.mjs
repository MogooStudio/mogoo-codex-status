import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
export class QuotaClient {
  constructor(binary, home) { this.binary = binary; this.home = home; this.process = null; this.pending = new Map(); this.serial = 0; }
  async connect() {
    if (this.process) return;
    const env = { ...process.env, CODEX_HOME: this.home };
    for (const key of ['CODEX_THREAD_ID','CODEX_SESSION_ID','CODEX_APP_TOOLS_PIPE_PATH']) delete env[key];
    this.process = spawn(this.binary,['app-server','--disable','hooks'],{ windowsHide:true, stdio:['pipe','pipe','pipe'],env });
    this.process.stderr.resume(); // Never persist authentication diagnostics.
    const lines = createInterface({ input:this.process.stdout });
    lines.on('line',line => {
      let value; try { value=JSON.parse(line); } catch { return; }
      const pending = this.pending.get(value.id);
      if (!pending) return;
      this.pending.delete(value.id); clearTimeout(pending.timer);
      value.error ? pending.reject(new Error('quota-query-failed')) : pending.resolve(value.result);
    });
    const child = this.process;
    const closed = () => { if (this.process !== child) return; this.process=null; for (const p of this.pending.values()) { clearTimeout(p.timer); p.reject(new Error('quota-unavailable')); } this.pending.clear(); };
    child.on('error',closed); child.on('exit',closed);
    try {
      await this.call('initialize',{ clientInfo:{name:'mogoo-codex-status',version:'0.1.0'} });
      child.stdin.write(JSON.stringify({ method:'initialized' })+'\n');
    } catch(error) { this.close(); throw error; }
  }
  call(method,params) {
    return new Promise((resolve,reject) => {
      const id = ++this.serial;
      const timer = setTimeout(() => { this.pending.delete(id); reject(new Error('quota-timeout')); },12000);
      this.pending.set(id,{resolve,reject,timer});
      this.process?.stdin.write(JSON.stringify({id,method,...(params === undefined ? {} : {params})})+'\n',error => { if(error) { clearTimeout(timer); this.pending.delete(id); reject(new Error('quota-unavailable')); } });
    });
  }
  async read() { await this.connect(); return this.call('account/rateLimits/read'); }
  close() { this.process?.kill(); this.process=null; for(const p of this.pending.values()) {clearTimeout(p.timer);p.reject(new Error('quota-stopped'));} this.pending.clear(); }
}
