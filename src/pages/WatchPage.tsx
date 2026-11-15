// File: WatchPage.tsx
// Purpose: User navigates here with a video ID in the route; polls until
// Ready, then plays it (FLOW.md, Video Streaming Flow, steps 1-4).
import { lazy, Suspense } from "react";
import { Link, useLocation, useParams } from "react-router-dom";
import { useVideoStatus } from "../hooks/useVideoStatus";
import { useAuth } from "../hooks/useAuth";
import { Button } from "../components/ui/Button";
import { ProcessingStatusBadge } from "../components/video/ProcessingStatusBadge";
import { Spinner } from "../components/ui/Spinner";
import { formatDate } from "../utils/formatters";

const VideoPlayer = lazy(() => import("../components/video/VideoPlayer").then(
  (module) => ({ default: module.VideoPlayer }),
));

function PlayerLoading() {
  return (
    <div className="flex aspect-video items-center justify-center rounded-lg bg-surface">
      <Spinner size={28} className="text-ink-faint" />
    </div>
  );
}

export function WatchPage() {
  const { id } = useParams<{ id: string }>();
  return <WatchVideo key={id} id={id} />;
}

function WatchVideo({ id }: { id?: string }) {
  const { user } = useAuth();
  const location = useLocation();
  const librarySearch = (location.state as { librarySearch?: unknown } | null)?.librarySearch;
  const { video, error, isLoading, canRetry, retry } = useVideoStatus(id);

  return (
    <div className="mx-auto max-w-4xl px-4 py-8 sm:px-6 sm:py-10">
      <Link
        to={{ pathname: "/dashboard", search: typeof librarySearch === "string" ? librarySearch : "" }}
        className="mb-4 inline-block text-sm font-medium text-accent hover:text-accent-hover"
      >
        Back to your videos
      </Link>
      {isLoading && <PlayerLoading />}

      {!isLoading && error && (
        <div className="flex aspect-video flex-col items-center justify-center gap-2 rounded-lg bg-surface text-center">
          <p role="alert" className="text-status-failed">{error}</p>
          {canRetry && <Button variant="secondary" onClick={retry}>Try again</Button>}
        </div>
      )}

      {!isLoading && !error && video && (
        <>
          {video.status === "Ready" ? (
            video.masterPlaylistUrl && user ? (
              <Suspense fallback={<PlayerLoading />}>
                <VideoPlayer
                  key={JSON.stringify([user.id, video.id, video.masterPlaylistUrl])}
                  src={video.masterPlaylistUrl} title={video.title}
                  videoId={video.id} userId={user.id} storageMode="real"
                />
              </Suspense>
            ) : (
              <div role="alert" className="grid aspect-video place-items-center rounded-lg bg-surface px-4 text-center">
                Playback isn't available for this video.
              </div>
            )
          ) : video.status === "Failed" ? (
            <div className="flex aspect-video flex-col items-center justify-center gap-2 rounded-lg bg-surface px-6 text-center">
              <p className="font-medium text-status-failed">Processing failed</p>
              {video.errorMessage && <p className="max-w-md text-sm text-ink-faint">{video.errorMessage}</p>}
              <Link to="/upload" className="text-sm font-semibold text-accent">Upload again</Link>
            </div>
          ) : (
            <div role="status" className="flex aspect-video flex-col items-center justify-center gap-3 rounded-lg bg-surface px-4 text-center">
              <Spinner size={28} className="text-ink-faint" />
              <p className="text-sm text-ink-faint">
                {video.status === "Pending"
                  ? "Waiting for the upload to be confirmed…"
                  : video.status === "Queued"
                    ? "Queued for processing…"
                    : "Preparing playback qualities. This can take a few minutes."}
              </p>
            </div>
          )}

          <div className="mt-6 flex flex-col gap-2">
            <h1 className="font-display text-xl font-bold text-ink">{video.title}</h1>
            <div className="flex items-center gap-3">
              <ProcessingStatusBadge status={video.status} />
              <span className="text-xs text-ink-faint">Uploaded {formatDate(video.createdAt)}</span>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
