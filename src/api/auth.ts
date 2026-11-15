// File: auth.ts
// Purpose: login(), register(), logout() (ARCHITECTURE.md section 3.1) —
// one typed function per AuthEndpoints.cs route.
// Logic/Approach: The two failure shapes AuthEndpoints.cs actually
// produces are handled separately, deliberately not through the shared
// throwIfNotOk in client.ts (that one is for the Videos endpoints' bare-
// string errors): Login/Refresh answer a wrong attempt with an empty 401
// body (TypedResults.Unauthorized(), no message — the point is to never
// reveal *why* a login failed), while Register answers with ASP.NET's
// ValidationProblem shape (`{ errors: { [code]: string[] }, title, ... }`,
// from TypedResults.ValidationProblem(...) over Identity's own
// IdentityResult errors).
import { apiFetch, ApiError, parseJson, setAccessToken } from "./client";

interface AuthResponseBody {
  accessToken: string;
  expiresAt: string;
}

interface ValidationProblemBody {
  errors?: Record<string, string[]>;
  title?: string;
}

async function extractValidationMessage(response: Response, fallback: string): Promise<string> {
  try {
    const body = (await response.json()) as ValidationProblemBody | string;

    // Some failures (rate limiting, unexpected errors) are a bare JSON string.
    if (typeof body === "string") return body.length > 0 ? body : fallback;

    if (body.errors) {
      const messages = Object.values(body.errors).flat();
      if (messages.length > 0) return messages.join(" ");
    }
    return body.title ?? fallback;
  } catch {
    return fallback;
  }
}

export async function login(email: string, password: string): Promise<void> {
  const response = await apiFetch("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({ email, password }),
  });

  if (!response.ok) {
    // 429: the Api's rate limiter, not a wrong password — say so.
    const message =
      response.status === 429
        ? "Too many attempts. Please wait a minute and try again."
        : "Incorrect email or password.";
    throw new ApiError(message, response.status);
  }

  const data = await parseJson<AuthResponseBody>(response);
  setAccessToken(data.accessToken);
}

export async function register(email: string, password: string, displayName: string): Promise<void> {
  const response = await apiFetch("/api/auth/register", {
    method: "POST",
    body: JSON.stringify({ email, password, displayName }),
  });

  if (!response.ok) {
    const message = await extractValidationMessage(response, "Could not create your account.");
    throw new ApiError(message, response.status);
  }

  // Register only creates the account — it does not sign the new user in
  // or return a token, so log in right after with the same credentials.
  // From the caller's side this stays one "create account" action.
  await login(email, password);
}

export async function logout(): Promise<void> {
  try {
    await apiFetch("/api/auth/logout", { method: "POST" });
  } finally {
    // Clear client-side state unconditionally — never leave someone
    // looking signed in after they asked to log out, even if this call failed.
    setAccessToken(null);
  }
}
