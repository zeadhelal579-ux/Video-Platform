// Keeps the public demo account signed in across reloads in the same tab.
import { useState, type ReactNode } from "react";
import { ApiError } from "../api/client";
import { AuthContext } from "../context/AuthContext";
import { demoPassword, demoUser } from "./config";

const SESSION_KEY = "vp.demo.session.v1";

function readSession(): boolean {
  try {
    return sessionStorage.getItem(SESSION_KEY) === "signed-in";
  } catch {
    return false;
  }
}

export function DemoAuthProvider({ children }: { children: ReactNode }) {
  const [signedIn, setSignedIn] = useState(readSession);

  function updateSession(active: boolean) {
    setSignedIn(active);
    try {
      if (active) sessionStorage.setItem(SESSION_KEY, "signed-in");
      else sessionStorage.removeItem(SESSION_KEY);
    } catch {
      // In-memory login also works when browser storage is blocked.
    }
  }

  const value = {
    user: signedIn ? demoUser : null,
    isInitializing: false,
    async login(email: string, password: string) {
      if (email.trim().toLowerCase() !== demoUser.email || password !== demoPassword) {
        throw new ApiError("Use the prefilled demo email and password.", 401);
      }
      updateSession(true);
    },
    async logout() {
      updateSession(false);
    },
    async register() {
      throw new ApiError("Use the public demo account to sign in.", 400);
    },
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
