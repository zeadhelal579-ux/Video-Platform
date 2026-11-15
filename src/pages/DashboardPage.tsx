// File: DashboardPage.tsx
// Purpose: DashboardPage -> api/videos.listVideos() (ARCHITECTURE.md
// section 6, "List my videos" row) — paginated, filtered to the
// signed-in user's own videos server-side (ListVideosHandler.cs).
import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { isDemoMode } from "../demo/config";
import { listVideos } from "../api/videos";
import { ApiError } from "../api/client";
import { VideoCard } from "../components/video/VideoCard";
import { Button } from "../components/ui/Button";
import { Spinner } from "../components/ui/Spinner";
import type { Video } from "../types/video";

const PAGE_SIZE = 20;
const MAX_PAGE = Math.floor(2147483647 / PAGE_SIZE) + 1;
const SEARCH_LIMIT = 100;
const REFRESH_MS = 5000;
const UPLOAD_LINK = "rounded-md bg-accent px-4 py-2.5 text-sm font-semibold text-accent-ink hover:bg-accent-hover";

export function DashboardPage() {
  const [params, setParams] = useSearchParams();
  const q = (params.get("q") ?? "").trim();
  const rawPage = params.get("page") ?? "1";
  const requestedPage = Number(rawPage);
  const page = /^\d+$/.test(rawPage) && requestedPage >= 1 && requestedPage <= MAX_PAGE
    ? requestedPage : 1;

  function navigate(search: string, nextPage = 1, replace = false) {
    const next = new URLSearchParams();
    if (search) next.set("q", search);
    if (nextPage > 1) next.set("page", String(nextPage));
    setParams(next, { replace });
  }

  return (
    <div className="mx-auto max-w-6xl px-4 py-8 sm:px-6 sm:py-10">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="font-display text-3xl font-bold text-ink sm:text-4xl">{isDemoMode ? "Demo library" : "Your videos"}</h1>
          {isDemoMode && <p className="mt-2 text-sm text-ink-muted">Browse the collection or try uploading a clip of your own.</p>}
        </div>
        <Link to="/upload" className={UPLOAD_LINK}>Upload video</Link>
      </div>
      <form
        key={q}
        role="search"
        className="mt-6 flex flex-wrap items-center gap-2"
        onSubmit={(event) => {
          event.preventDefault();
          const draft = String(new FormData(event.currentTarget).get("q") ?? "").trim();
          navigate(draft);
        }}
      >
        <input
          aria-label="Search videos by title"
          name="q"
          type="search"
          defaultValue={q}
          maxLength={SEARCH_LIMIT}
          placeholder="Search by title"
          className="min-w-0 flex-1 rounded-md border border-border bg-surface px-3 py-2 text-ink"
        />
        <Button type="submit">Search</Button>
        {!!q && <Button type="button" variant="ghost" onClick={() => navigate("")}>Clear</Button>}
      </form>
      <LibraryResults key={JSON.stringify([q, page])} q={q} page={page} navigate={navigate} librarySearch={params.toString()} />
    </div>
  );
}

interface LibraryResultsProps {
  q: string;
  page: number;
  librarySearch: string;
  navigate: (search: string, page?: number, replace?: boolean) => void;
}

function LibraryResults({ q, page, navigate, librarySearch }: LibraryResultsProps) {
  const [videos, setVideos] = useState<Video[] | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout> | undefined;

    async function load(background = false) {
      try {
        const result = await listVideos(page, PAGE_SIZE, q, controller.signal);
        if (controller.signal.aborted) return;
        setVideos(result.items);
        setTotalCount(result.totalCount);
        if (result.items.some((video) => video.status === "Queued" || video.status === "Processing")) {
          timer = setTimeout(() => void load(true), REFRESH_MS);
        }
      } catch (err) {
        if (controller.signal.aborted) return;
        if (background && (!(err instanceof ApiError) || err.status === 429 || err.status >= 500)) {
          timer = setTimeout(() => void load(true), REFRESH_MS);
          return;
        }
        setError(err instanceof ApiError ? err.message : "Could not load your videos.");
      }
    }

    void load();
    return () => {
      controller.abort();
      clearTimeout(timer);
    };
  }, [q, page, attempt]);

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  useEffect(() => {
    if (videos && !error && page > totalPages) navigate(q, totalPages, true);
  }, [videos, error, page, totalPages, q, navigate]);

  return (
    <>
      {error && (
        <div className="mt-8 flex items-center gap-3">
          <p role="alert" className="text-sm text-status-failed">{error}</p>
          <Button variant="secondary" onClick={() => {
            setError(null);
            setVideos(null);
            setAttempt((value) => value + 1);
          }}>Try again</Button>
        </div>
      )}

      {!error && videos === null && (
        <div className="flex min-h-[30vh] items-center justify-center">
          <Spinner size={28} className="text-ink-faint" />
        </div>
      )}

      {!error && videos !== null && videos.length === 0 && page <= totalPages && (
        <div className="mt-16 flex flex-col items-center gap-3 text-center">
          <p className="text-ink-muted">{q ? "No videos match this search." : "You haven't uploaded anything yet."}</p>
          {q ? (
            <Button variant="secondary" onClick={() => navigate("")}>Clear search</Button>
          ) : (
            <Link to="/upload" className={UPLOAD_LINK}>Upload your first video</Link>
          )}
        </div>
      )}

      {!error && videos !== null && videos.length > 0 && (
        <>
          <div className="mt-8 grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {videos.map((video) => (
              <VideoCard key={video.id} video={video} librarySearch={librarySearch} />
            ))}
          </div>

          {totalPages > 1 && (
            <div className="mt-8 flex items-center justify-center gap-3">
              <Button variant="secondary" disabled={page <= 1} onClick={() => navigate(q, page - 1)}>
                Previous
              </Button>
              <span className="text-sm text-ink-faint">
                Page {page} of {totalPages}
              </span>
              <Button variant="secondary" disabled={page >= totalPages} onClick={() => navigate(q, page + 1)}>
                Next
              </Button>
            </div>
          )}
        </>
      )}
    </>
  );
}
