// File: HomePage.tsx
import { Link } from "react-router-dom";
import { useAuth } from "../hooks/useAuth";

const STEPS = [
  {
    title: "Upload",
    body: "Choose a video and give it a title.",
  },
  {
    title: "Process",
    body: "Follow its progress while we prepare playback qualities.",
  },
  {
    title: "Stream",
    body: "Watch at a quality that adapts to your connection.",
  },
];

export function HomePage() {
  const { user } = useAuth();

  return (
    <div className="mx-auto max-w-5xl px-4 py-10 text-center sm:px-6 sm:py-16">
      <div className="rounded-2xl border border-border bg-gradient-to-b from-surface-raised to-surface px-5 py-12 sm:py-20">
      <h1 className="font-display text-4xl font-bold tracking-tight text-ink sm:text-5xl">
        Upload once. <span className="text-accent">Stream anywhere.</span>
      </h1>
      <p className="mx-auto mt-4 max-w-xl text-ink-muted">
        Keep your videos in one library and pick up where you left off on this browser.
      </p>

      <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
        <Link to={user ? "/dashboard" : "/register"} className="rounded-md bg-accent px-4 py-3 text-sm font-semibold text-accent-ink hover:bg-accent-hover">
          {user ? "Open your library" : "Create an account"}
        </Link>
        <Link to={user ? "/upload" : "/login"} className="rounded-md border border-border px-4 py-3 text-sm font-semibold text-ink hover:bg-surface-raised">
          {user ? "Upload a video" : "Log in"}
        </Link>
      </div>
      </div>

      <div className="mt-6 grid grid-cols-1 gap-3 text-left sm:grid-cols-3">
        {STEPS.map((step) => (
          <div key={step.title} className="rounded-lg border border-border bg-surface p-5">
            <h2 className="font-display text-base font-semibold text-ink">{step.title}</h2>
            <p className="mt-1.5 text-sm text-ink-faint">{step.body}</p>
          </div>
        ))}
      </div>
    </div>
  );
}
