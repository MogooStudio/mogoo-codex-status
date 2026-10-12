import { mkdir, readFile, rename, writeFile } from 'node:fs/promises';
import path from 'node:path';
export async function readJson(file, fallback = null) {
  try { return JSON.parse(await readFile(file, 'utf8')); } catch { return fallback; }
}
export async function writeJson(file, value) {
  await mkdir(path.dirname(file), { recursive: true });
  const temp = `${file}.${process.pid}.tmp`;
  await writeFile(temp, JSON.stringify(value), 'utf8');
  await rename(temp, file);
}
