const number = value => typeof value === 'number' && Number.isFinite(value) && value >= 0;
const clamp = value => Math.min(100, Math.max(0, value));
export function contextMetric(info, timestamp) {
  const used = info?.last_token_usage?.total_tokens;
  const capacity = info?.model_context_window;
  if (!number(used) || !number(capacity) || capacity === 0) return null;
  return { usedTokens: used, capacityTokens: capacity, usedPercent: clamp(100 * used / capacity), remainingTokens: Math.max(0, capacity - used), updatedAt: timestamp, approximate: true };
}
export function limitMetrics(value, timestamp, source = 'snapshot') {
  if (!value) return [];
  const buckets = value.rateLimitsByLimitId ?? value.rate_limits_by_limit_id;
  const single = value.rateLimits ?? value.rate_limits ?? value;
  const entries = buckets && typeof buckets === 'object' ? Object.entries(buckets) : [[single.limitId ?? single.limit_id ?? 'codex', single]];
  return entries.flatMap(([id, bucket]) => ['primary', 'secondary'].flatMap(kind => {
    const w = bucket?.[kind];
    const used = w?.usedPercent ?? w?.used_percent;
    const duration = w?.windowDurationMins ?? w?.window_minutes;
    const resets = w?.resetsAt ?? w?.resets_at;
    if (!number(used)) return [];
    return [{ id, name: bucket.limitName ?? bucket.limit_name ?? id, kind, usedPercent: clamp(used), remainingPercent: clamp(100-used), windowMinutes: number(duration) && duration > 0 ? duration : null, resetsAt: number(resets) && resets > 0 ? resets : null, updatedAt: timestamp, source }];
  }));
}
export function windowLabel(minutes) {
  if (!minutes) return '限额';
  if (minutes % 1440 === 0) return `${minutes/1440}天`;
  if (minutes % 60 === 0) return `${minutes/60}小时`;
  return `${minutes}分钟`;
}
export function withoutTitleTime(title) { return String(title ?? '').replace(/\s*·\s*\d{2}-\d{2}\s+\d{2}:\d{2}\s*$/, '').trim(); }
export function selectThread(threads, aliases, title) {
  if (!title?.trim()) return { status: 'unknown', thread: null };
  const exact = new Set();
  for (const row of threads) if (row.name === title || row.title === title) exact.add(row.id);
  for (const row of aliases) if (row.thread_name === title) exact.add(row.id);
  let matches = threads.filter(row => exact.has(row.id));
  if (!matches.length) {
    const key = withoutTitleTime(title);
    const ids = new Set(aliases.filter(row => withoutTitleTime(row.thread_name) === key).map(row => row.id));
    matches = threads.filter(row => ids.has(row.id) || withoutTitleTime(row.name) === key);
  }
  if (matches.length === 1) return { status: 'matched', thread: matches[0] };
  return { status: matches.length ? 'ambiguous' : 'unknown', thread: null };
}
