import { open, stat } from 'node:fs/promises';

// Discard large conversation/tool records instead of retaining their contents.
export class UsageReader {
  constructor(file) { this.file = file; this.reset(); }
  reset() { this.offset = 0; this.pending = Buffer.alloc(0); this.dropping = false; this.info = null; this.contextAt = null; this.rate = null; this.rateAt = null; this.identity = null; }
  consume(chunk) {
    let data = Buffer.concat([this.pending, chunk]); this.pending = Buffer.alloc(0);
    while (true) {
      const end = data.indexOf(10);
      if (end < 0) {
        if (data.length > 4*1024*1024) this.dropping = true;
        if (!this.dropping) this.pending = data;
        return;
      }
      if (!this.dropping && end <= 4*1024*1024) this.line(data.subarray(0,end).toString('utf8'));
      this.dropping = false; data = data.subarray(end+1);
    }
  }
  line(line) {
    if (!line.includes('token_count') && !line.includes('"compacted"')) return;
    let record; try { record = JSON.parse(line); } catch { return; }
    if (record.type === 'compacted') { this.info = null; this.contextAt = null; return; }
    if (record.type !== 'event_msg' || record.payload?.type !== 'token_count') return;
    if (record.payload.info) { this.info = record.payload.info; this.contextAt = record.timestamp; }
    if (record.payload.rate_limits) { this.rate = record.payload.rate_limits; this.rateAt = record.timestamp; }
  }
  async update() {
    const s = await stat(this.file);
    const identity = `${s.dev}:${s.ino}`;
    if (s.size < this.offset || (this.identity && identity !== this.identity)) this.reset();
    this.identity = identity;
    const file = await open(this.file, 'r');
    try {
      const buffer = Buffer.alloc(64*1024);
      while (this.offset < s.size) {
        const { bytesRead } = await file.read(buffer,0,Math.min(buffer.length,s.size-this.offset),this.offset);
        if (!bytesRead) break;
        this.consume(buffer.subarray(0,bytesRead)); this.offset += bytesRead;
      }
    } finally { await file.close(); }
    return { info: this.info, contextAt: this.contextAt, rate: this.rate, rateAt: this.rateAt };
  }
}
