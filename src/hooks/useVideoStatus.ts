// File: useVideoStatus.ts
// Purpose: Polls getVideo() on an interval until status is Ready or
// Failed, so WatchPage and DashboardPage don't each implement their own
// polling loop (ARCHITECTURE.md section 3.1).
// Logic/Approach: A self-rescheduling setTimeout chain rather than
// setInterval — if one poll takes longer than the interval (a slow
// network), setInterval would fire overlapping requests, while this waits
// for each response before scheduling the next. Stops entirely once the
// video reaches a terminal state (types/video.ts's isTerminalStatus) or
// the id changes.
import { useEffect, useState } from "react";
import { getVideo } from "../api/videos";
import { ApiError } from "../api/client";
import { isTerminalStatus, type Video } from "../types/video";

const POLL_INTERVAL_MS = 4000;
const MAX_FAILURES = 6;

interface UseVideoStatusResult {
  video: Video | null;
  error: string | null;
  isLoading: boolean;
  canRetry: boolean;
  retry: () => void;
}

export function useVideoStatus(videoId: string | undefined): UseVideoStatusResult {
  const [video, setVideo] = useState<Video | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [canRetry, setCanRetry] = useState(false);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    if (!videoId) {
      setError("This video does not exist.");
      setIsLoading(false);
      return;
    }

    const controller = new AbortController();
    let timeoutId: ReturnType<typeof setTimeout> | undefined;
    let failures = 0;

    setVideo(null);
    setError(null);
    setCanRetry(false);
    setIsLoading(true);

    const poll = async () => {
      try {
        const data = await getVideo(videoId, controller.signal);
        if (controller.signal.aborted) return;
        failures = 0;

        setVideo(data);
        setIsLoading(false);

        if (!isTerminalStatus(data.status)) {
          timeoutId = setTimeout(poll, POLL_INTERVAL_MS);
        }
      } catch (err) {
        if (controller.signal.aborted) return;
        const transient = !(err instanceof ApiError) || err.status === 429 || err.status >= 500;
        failures += 1;
        if (transient && failures < MAX_FAILURES) {
          timeoutId = setTimeout(poll, Math.min(2000 * 2 ** (failures - 1), 30000));
          return;
        }
        setCanRetry(transient);
        setError(err instanceof ApiError ? err.message : "Something went wrong.");
        setIsLoading(false);
      }
    };

    void poll();

    return () => {
      controller.abort();
      if (timeoutId) clearTimeout(timeoutId);
    };
  }, [videoId, attempt]);

  return { video, error, isLoading, canRetry, retry: () => {
    setError(null);
    setIsLoading(true);
    setAttempt((value) => value + 1);
  } };
}
