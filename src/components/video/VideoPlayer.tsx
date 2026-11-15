// Hosts Video.js and saves the active video's local playback checkpoint.
import { useEffect, useRef, useState } from "react";
import videojs from "video.js";
import "video.js/dist/video-js.css";
import { Button } from "../ui/Button";
import { readProgress, saveProgress } from "../../utils/playbackProgress";

interface VideoPlayerProps {
  src: string;
  title: string;
  mediaType?: string;
  poster?: string;
  userId: string;
  videoId: string;
  storageMode: "real" | "demo";
}

const SAVE_INTERVAL_MS = 5000;
const END_MARGIN_SECONDS = 10;

export function VideoPlayer(props: VideoPlayerProps) {
  const [attempt, setAttempt] = useState(0);
  return <PlaybackSession key={attempt} {...props} onRetry={() => setAttempt((value) => value + 1)} />;
}

function PlaybackSession(props: VideoPlayerProps & { onRetry: () => void }) {
  const [checkpoint] = useState(() => readProgress(props));
  const [startAt, setStartAt] = useState<number | null>(() => checkpoint === null ? 0 : null);

  if (startAt === null && checkpoint !== null) {
    const seconds = Math.floor(checkpoint);
    const timestamp = `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}`;
    return (
      <div className="flex aspect-video flex-col items-center justify-center gap-3 rounded-lg bg-surface p-4 text-center">
        <p>Continue watching?</p>
        <div className="flex flex-wrap justify-center gap-2">
          <Button onClick={() => setStartAt(checkpoint)}>Resume at {timestamp}</Button>
          <Button variant="secondary" onClick={() => {
            saveProgress(props, null);
            setStartAt(0);
          }}>Start over</Button>
        </div>
      </div>
    );
  }

  return <HlsPlayer {...props} startAt={startAt ?? 0} />;
}

function HlsPlayer({
  src, title, mediaType, poster, userId, videoId, storageMode, startAt, onRetry,
}: VideoPlayerProps & { startAt: number; onRetry: () => void }) {
  const container = useRef<HTMLDivElement>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (!container.current) return;
    const key = { userId, videoId, storageMode };
    const element = document.createElement("video-js");
    element.classList.add("vjs-big-play-centered", "vjs-theme-videoplatform");
    element.setAttribute("aria-label", title);
    container.current.appendChild(element);
    const player = videojs(element, {
      controls: startAt === 0,
      responsive: true,
      fluid: true,
      preload: startAt > 0 ? "auto" : "metadata",
      playbackRates: [0.5, 1, 1.25, 1.5, 2],
      poster,
      sources: [{ src, type: mediaType ?? "application/x-mpegURL" }],
    });
    let awaitingSeek = startAt > 0;
    let watched = false;
    let ended = false;
    let lastSavedAt = 0;

    function restorePosition() {
      if (!awaitingSeek) return;
      const duration = player.duration();
      if (typeof duration !== "number" || !Number.isFinite(duration) || duration <= 0) return;
      if (startAt >= duration - END_MARGIN_SECONDS) {
        saveProgress(key, null);
      } else {
        const ranges = player.seekable();
        if (!ranges.length) return;
        for (let index = 0; index < ranges.length; index += 1) {
          if (startAt >= ranges.start(index) && startAt <= ranges.end(index)) {
            player.currentTime(startAt);
            awaitingSeek = false;
            player.controls(true);
            return;
          }
        }
        saveProgress(key, null);
      }
      awaitingSeek = false;
      player.controls(true);
    }

    function checkpoint() {
      if (!watched || ended || awaitingSeek || player.isDisposed()) return;
      const seconds = player.currentTime();
      const duration = player.duration();
      if (typeof seconds !== "number" || !Number.isFinite(seconds)
        || typeof duration !== "number" || !Number.isFinite(duration)) return;
      saveProgress(key, duration - seconds <= END_MARGIN_SECONDS ? null : seconds);
      lastSavedAt = Date.now();
    }

    player.on(["loadedmetadata", "durationchange", "progress", "canplay"], restorePosition);
    const seekTimeout = window.setTimeout(() => {
      if (!awaitingSeek) return;
      awaitingSeek = false;
      player.controls(true);
    }, 12000);
    player.on("playing", () => {
      watched = true;
      ended = false;
    });
    player.on("timeupdate", () => {
      if (!player.paused() && Date.now() - lastSavedAt >= SAVE_INTERVAL_MS) checkpoint();
    });
    player.on(["pause", "seeked"], checkpoint);
    player.on("ended", () => {
      ended = true;
      saveProgress(key, null);
    });
    player.on("error", () => {
      checkpoint();
      setFailed(true);
    });
    window.addEventListener("pagehide", checkpoint);

    return () => {
      window.removeEventListener("pagehide", checkpoint);
      window.clearTimeout(seekTimeout);
      checkpoint();
      if (!player.isDisposed()) player.dispose();
    };
  }, [src, title, mediaType, poster, userId, videoId, storageMode, startAt]);

  return (
    <div className="overflow-hidden rounded-lg bg-black">
      <div data-vjs-player className={failed ? "hidden" : "aspect-video"}>
        <div ref={container} />
      </div>
      {failed && (
        <div className="flex aspect-video flex-col items-center justify-center gap-3 p-4 text-center">
          <p role="alert">Playback is unavailable.</p>
          <Button variant="secondary" onClick={onRetry}>Retry playback</Button>
        </div>
      )}
    </div>
  );
}
