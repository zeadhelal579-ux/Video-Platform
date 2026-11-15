// Stores a bounded set of local playback checkpoints for one account.
type Checkpoint = { seconds: number; updatedAt: number };
type ProgressKey = { userId: string; videoId: string; storageMode: "real" | "demo" };

const MIN_SECONDS = 5;
const MAX_ENTRIES = 50;

function storageKey({ userId, storageMode }: ProgressKey): string {
  return `vp.resume.v1:${storageMode}:${userId}`;
}

function isCheckpoint(value: unknown): value is Checkpoint {
  if (!value || typeof value !== "object") return false;
  const { seconds, updatedAt } = value as Partial<Checkpoint>;
  return typeof seconds === "number" && Number.isFinite(seconds) && seconds >= MIN_SECONDS
    && typeof updatedAt === "number" && Number.isFinite(updatedAt) && updatedAt >= 0;
}

function readEntries(key: ProgressKey): Map<string, Checkpoint> {
  const raw = window.localStorage.getItem(storageKey(key));
  if (!raw) return new Map();
  try {
    const parsed: unknown = JSON.parse(raw);
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) return new Map();
    return new Map(Object.entries(parsed).filter(
      (entry): entry is [string, Checkpoint] => isCheckpoint(entry[1]),
    ));
  } catch {
    return new Map();
  }
}

export function readProgress(key: ProgressKey): number | null {
  try {
    return readEntries(key).get(key.videoId)?.seconds ?? null;
  } catch {
    return null;
  }
}

export function saveProgress(key: ProgressKey, seconds: number | null): void {
  try {
    const entries = readEntries(key);
    if (seconds !== null && Number.isFinite(seconds) && seconds >= MIN_SECONDS) {
      entries.set(key.videoId, { seconds, updatedAt: Date.now() });
    } else {
      entries.delete(key.videoId);
    }
    const latest = [...entries].sort((a, b) => b[1].updatedAt - a[1].updatedAt).slice(0, MAX_ENTRIES);
    window.localStorage.setItem(storageKey(key), JSON.stringify(Object.fromEntries(latest)));
  } catch {
    // Playback is available even when browser storage is blocked or full.
  }
}
