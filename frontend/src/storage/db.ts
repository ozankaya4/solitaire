// Thin promise wrapper over IndexedDB with clean, versioned schema migration.
//
// Schema history:
//   v1 — object stores: 'settings', 'progress', 'saves', 'stats' (out-of-line keys).
// To evolve: bump DB_VERSION and extend migrate() with a new `oldVersion < N` block
// that creates/renames stores or transforms data. Each block runs in order, so an
// old browser upgrading across several versions applies every step.

export const DB_NAME = 'solitaire-db';
export const DB_VERSION = 1;

export const STORES = ['settings', 'progress', 'saves', 'stats'] as const;
export type StoreName = (typeof STORES)[number];

let dbPromise: Promise<IDBDatabase> | null = null;

export function idbAvailable(): boolean {
  return typeof indexedDB !== 'undefined';
}

export function openDb(): Promise<IDBDatabase> {
  if (dbPromise) {
    return dbPromise;
  }
  dbPromise = new Promise<IDBDatabase>((resolve, reject) => {
    if (!idbAvailable()) {
      reject(new Error('IndexedDB unavailable'));
      return;
    }
    const request = indexedDB.open(DB_NAME, DB_VERSION);
    request.onupgradeneeded = (event) => {
      migrate(request.result, event.oldVersion);
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error ?? new Error('IndexedDB open failed'));
    request.onblocked = () => reject(new Error('IndexedDB open blocked'));
  });
  return dbPromise;
}

function migrate(db: IDBDatabase, oldVersion: number): void {
  if (oldVersion < 1) {
    for (const store of STORES) {
      if (!db.objectStoreNames.contains(store)) {
        db.createObjectStore(store);
      }
    }
  }
  // if (oldVersion < 2) { ...future migration... }
}

function toPromise<T>(request: IDBRequest): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    request.onsuccess = () => resolve(request.result as T);
    request.onerror = () => reject(request.error ?? new Error('IndexedDB request failed'));
  });
}

async function withStore<T>(
  store: StoreName,
  mode: IDBTransactionMode,
  run: (objectStore: IDBObjectStore) => IDBRequest,
): Promise<T> {
  const db = await openDb();
  return toPromise<T>(run(db.transaction(store, mode).objectStore(store)));
}

export function idbGet<T>(store: StoreName, key: string): Promise<T | undefined> {
  return withStore<T | undefined>(store, 'readonly', (s) => s.get(key));
}

export function idbGetAll<T>(store: StoreName): Promise<T[]> {
  return withStore<T[]>(store, 'readonly', (s) => s.getAll());
}

export function idbGetAllKeys(store: StoreName): Promise<IDBValidKey[]> {
  return withStore<IDBValidKey[]>(store, 'readonly', (s) => s.getAllKeys());
}

export function idbPut(store: StoreName, key: string, value: unknown): Promise<IDBValidKey> {
  return withStore<IDBValidKey>(store, 'readwrite', (s) => s.put(value, key));
}

export function idbDelete(store: StoreName, key: string): Promise<undefined> {
  return withStore<undefined>(store, 'readwrite', (s) => s.delete(key));
}
