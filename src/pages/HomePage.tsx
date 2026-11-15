// File: HomePage.tsx
import { Link } from "react-router-dom";
import { isDemoMode } from "../demo/config";
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

  if (isDemoMode) return <DemoHome signedIn={!!user} />;

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

function DemoHome({ signedIn }: { signedIn: boolean }) {
  return (
    <section className="mx-auto max-w-6xl px-4 py-8 sm:px-6 sm:py-14">
      <div className="grid overflow-hidden rounded-2xl border border-border bg-surface md:grid-cols-[0.95fr_1.05fr]">
        <div className="flex flex-col items-start justify-center px-6 py-9 sm:px-10 sm:py-12 lg:px-12">
          <h1 className="max-w-xl font-display text-4xl font-bold leading-[1.05] tracking-tight text-ink sm:text-5xl lg:text-6xl">
            A library built <span className="text-accent">for video.</span>
          </h1>
          <p className="mt-5 max-w-sm text-base leading-relaxed text-ink-muted">
            Browse five Egyptian video previews, pick up where you left off, or try uploading a clip.
          </p>
          <Link
            to={signedIn ? "/dashboard" : "/login"}
            className="mt-7 inline-flex min-h-11 items-center rounded-md bg-accent px-5 py-3 text-sm font-semibold text-accent-ink hover:bg-accent-hover"
          >
            {signedIn ? "Open the library" : "Try the demo"}
          </Link>
        </div>
        <Link
          to="/watch/campus-characters"
          className="group relative isolate flex min-h-60 items-end overflow-hidden bg-black p-5 sm:min-h-72 sm:p-7 md:min-h-96"
        >
          <img
            src="/demo/v1/campus-characters/poster.jpg"
            alt=""
            fetchPriority="high"
            className="absolute inset-0 -z-20 h-full w-full object-cover transition-transform duration-300 group-hover:scale-[1.035]"
          />
          <div className="absolute inset-0 -z-10 bg-gradient-to-t from-black via-black/25 to-transparent" />
          <div className="flex w-full items-end justify-between gap-4 text-white">
            <div>
              <p className="text-xs font-medium text-white/85">Featured preview</p>
              <h2 lang="ar" dir="auto" className="mt-1 text-xl font-bold sm:text-2xl">شخصيات الجامعة</h2>
            </div>
            <span className="rounded-md border border-white/45 bg-black/50 px-3 py-2 text-sm font-semibold group-hover:bg-black/80">
              Watch
            </span>
          </div>
        </Link>
      </div>
    </section>
  );
}
