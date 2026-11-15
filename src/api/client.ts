// File: client.ts
// Purpose: The one place that knows an HTTP API exists (ARCHITECTURE.md
// section 3.1). Owns the in-memory access token, attaches it to every
// request, and implements the silent-refresh-then-retry-once flow FLOW.md
// step 12 describes.
// Logic/Approach: The access token lives in a plain module variable, never
// localStorage (FLOW.md step 7 — reduces exposure to XSS). It is plain
// module state rather than React state so this file has no React
// dependency; AuthContext.tsx subscribes to it via subscribeToAccessToken
// to stay in sync for rendering. `credentials: "include"` is required on
// every call: in development the Api (localhost:5000) and this app
// (localhost:5173) are different origins, and fetch does not send or store
// cookies cross-origin unless told to — without this, the httpOnly refresh
// cookie would never be sent or set, silently breaking the whole refresh flow.
//
// Where the Api lives. In development it is a separate server (see
// back/.../launchSettings.json). A production build is served by the web
// container, whose nginx also proxies /api to the Api — one origin — so the
// default there is "" (relative URLs like "/api/videos"). VITE_API_BASE_URL
// overrides both defaults, and an explicit empty value also means "same origin".
const configuredBaseUrl: string | undefined = import.meta.env.VITE_API_BASE_URL;
const API_BASE_URL = (configuredBaseUrl ?? (import.meta.env.DEV ? "http://localhost:5000" : "")).replace(/\/+$/, "");

// ---- In-memory access token store ----

let accessToken: string | null = null;
const listeners = new Set<(token: string | null) => void>();

export function getAccessToken(): string | null {
  return accessToken;
}

export function setAccessToken(token: string | null): void {
  accessToken = token;
  listeners.forEach((listener) => listener(token));
}

// Lets AuthContext mirror this module's token into React state without
// this file importing React. Returns an unsubscribe function.
export function subscribeToAccessToken(listener: (token: string | null) => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

// ---- Errors ----

export class ApiError extends Error {
  readonly status: number;
  readonly payload: unknown;

  constructor(message: string, status: number, payload?: unknown) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.payload = payload;
  }
}

// ---- Silent refresh, deduplicated ----
// If several requests 401 around the same time, they must all wait on the
// SAME refresh call rather than each firing their own — a stampede of
// concurrent /refresh calls would rotate the refresh token out from under
// each other (AuthEndpoints.cs revokes-and-replaces on every use).

let refreshInFlight: Promise<boolean> | null = null;

function refreshAccessToken(): Promise<boolean> {
  refreshInFlight ??= performRefresh().finally(() => {
    refreshInFlight = null;
  });
  return refreshInFlight;
}

async function performRefresh(): Promise<boolean> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/auth/refresh`, {
      method: "POST",
      credentials: "include",
    });

    if (!response.ok) {
      setAccessToken(null);
      return false;
    }

    const data = (await response.json()) as { accessToken: string };
    setAccessToken(data.accessToken);
    return true;
  } catch {
    setAccessToken(null);
    return false;
  }
}

// Attempts a silent sign-in from the httpOnly refresh cookie, if the
// browser still has a valid one from an earlier visit. Called once, on
// app load, by AuthContext — this is what lets a page reload keep someone
// signed in even though the access token itself lives only in memory.
export function bootstrapSession(): Promise<boolean> {
  return refreshAccessToken();
}

// ---- Core request wrapper ----

function buildRequest(path: string, options: RequestInit): [string, RequestInit] {
  const headers = new Headers(options.headers);
  if (!headers.has("Content-Type") && options.body !== undefined && typeof options.body === "string") {
    headers.set("Content-Type", "application/json");
  }

  const token = getAccessToken();
  if (token) headers.set("Authorization", `Bearer ${token}`);

  return [
    `${API_BASE_URL}${path}`,
    {
      ...options,
      headers,
      credentials: "include",
    },
  ];
}

// FLOW.md step 12, implemented literally: watch every response; on 401,
// silently refresh, then retry the original request once, transparently.
// Auth endpoints are excluded on purpose — a wrong password on /login is a
// meaningful 401 the caller must see as-is, not a trigger to refresh a
// session that was never established.
export async function apiFetch(path: string, options: RequestInit = {}): Promise<Response> {
  const [url, init] = buildRequest(path, options);
  let response = await fetch(url, init);

  const isAuthEndpoint = path.startsWith("/api/auth/");
  if (response.status === 401 && !isAuthEndpoint) {
    const refreshed = await refreshAccessToken();
    if (refreshed) {
      const [retryUrl, retryInit] = buildRequest(path, options);
      response = await fetch(retryUrl, retryInit);
    }
  }

  return response;
}

// ---- Response parsing, shared by api/auth.ts and api/videos.ts ----

async function readBody(response: Response): Promise<unknown> {
  const text = await response.text();
  if (!text) return undefined;
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return text;
  }
}

// The Videos endpoints (VideoEndpoints.cs) fail with a bare JSON string —
// `TypedResults.BadRequest(result.Error)`, and Result.Error is a plain
// string (Domain/Common/Result.cs) — not `{ message: ... }`. Login/Refresh
// fail with an empty 401 body. This covers both: use the string body
// verbatim when there is one, otherwise fall back to a generic message.
export async function throwIfNotOk(response: Response, fallbackMessage: string): Promise<void> {
  if (response.ok) return;

  const body = await readBody(response);
  const message = typeof body === "string" && body.length > 0 ? body : fallbackMessage;
  throw new ApiError(message, response.status, body);
}

export async function parseJson<T>(response: Response): Promise<T> {
  return (await readBody(response)) as T;
}
