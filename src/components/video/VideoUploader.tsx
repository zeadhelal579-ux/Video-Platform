// File: VideoUploader.tsx
// Purpose: Lets the user pick a file and title, and renders upload
// progress from the PUT request's progress events (ARCHITECTURE.md
// section 3.1) via useVideoUpload.
import { useRef, useState, type ChangeEvent, type DragEvent, type FormEvent } from "react";
import { useVideoUpload } from "../../hooks/useVideoUpload";
import { Button } from "../ui/Button";
import { formatFileSize } from "../../utils/formatters";
import { isSupportedVideoFile, SUPPORTED_VIDEO_ACCEPT, UNSUPPORTED_FILE_MESSAGE } from "../../utils/videoFiles";

interface VideoUploaderProps {
  onUploaded: (videoId: string) => void;
}

export function VideoUploader({ onUploaded }: VideoUploaderProps) {
  const [title, setTitle] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [isDragOver, setIsDragOver] = useState(false);
  const [fileError, setFileError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const { phase, progress, error, upload } = useVideoUpload();

  const isBusy = phase === "requesting" || phase === "uploading" || phase === "confirming";

  function handleFileChosen(chosen: File | null) {
    if (chosen && !isSupportedVideoFile(chosen)) {
      setFile(null);
      setFileError(UNSUPPORTED_FILE_MESSAGE);
      if (fileInputRef.current) fileInputRef.current.value = "";
      return;
    }
    setFileError(null);
    setFile(chosen);
    if (chosen && !title) {
      setTitle(chosen.name.replace(/\.[^/.]+$/, "").slice(0, 200));
    }
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    setIsDragOver(false);
    const dropped = event.dataTransfer.files[0];
    if (dropped && !isBusy) handleFileChosen(dropped);
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!file || !title.trim() || isBusy) return;

    try {
      const videoId = await upload(title.trim(), file);
      onUploaded(videoId);
    } catch {
      // useVideoUpload already captured the message in `error` — nothing more to do here.
    }
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-5">
      <div
        onDragOver={(event: DragEvent<HTMLDivElement>) => {
          event.preventDefault();
          if (!isBusy) setIsDragOver(true);
        }}
        onDragLeave={() => setIsDragOver(false)}
        onDrop={handleDrop}
        className={`flex flex-col items-center justify-center gap-3 rounded-lg border-2 border-dashed
          px-6 py-12 text-center transition-colors
          ${isDragOver ? "border-accent bg-accent/5" : "border-border hover:border-ink-faint"}`}
      >
        <input
          ref={fileInputRef}
          type="file"
          accept={SUPPORTED_VIDEO_ACCEPT}
          hidden
          disabled={isBusy}
          onChange={(event: ChangeEvent<HTMLInputElement>) => handleFileChosen(event.target.files?.[0] ?? null)}
        />
        {file ? (
          <>
            <p className="max-w-full break-all font-medium text-ink">{file.name}</p>
            <p className="text-sm text-ink-faint">{formatFileSize(file.size)}</p>
          </>
        ) : (
          <>
            <p className="font-medium text-ink">Drop a video here</p>
            <p className="text-sm text-ink-faint">Playback qualities are prepared after upload.</p>
          </>
        )}
        <Button type="button" variant="secondary" disabled={isBusy} onClick={() => fileInputRef.current?.click()}>
          {file ? "Choose a different video" : "Browse files"}
        </Button>
      </div>
      {fileError && <p role="alert" className="text-sm text-status-failed">{fileError}</p>}

      <label className="flex flex-col gap-1.5">
        <span className="text-sm font-medium text-ink-muted">Title</span>
        <input
          type="text"
          value={title}
          onChange={(event: ChangeEvent<HTMLInputElement>) => setTitle(event.target.value)}
          placeholder="Give your video a title"
          maxLength={200}
          disabled={isBusy}
          required
          className="rounded-md border border-border bg-surface px-3.5 py-2.5 text-ink placeholder:text-ink-faint
            focus:border-accent"
        />
      </label>

      {isBusy && (
        <div className="flex flex-col gap-1.5">
          {phase === "uploading" && (
            <progress aria-label="Video upload progress" value={progress} max={100} className="h-2 w-full accent-accent" />
          )}
          <p role="status" className="text-sm text-ink-muted">{phaseLabel(phase)}</p>
        </div>
      )}

      {error && <p role="alert" className="text-sm text-status-failed">{error}</p>}

      <Button type="submit" isLoading={isBusy} disabled={!file || !title.trim()}>
        {error && !isBusy ? "Try upload again" : "Upload video"}
      </Button>
    </form>
  );
}

function phaseLabel(phase: string): string {
  switch (phase) {
    case "requesting":
      return "Preparing upload…";
    case "uploading":
      return "Uploading video…";
    case "confirming":
      return "Confirming upload…";
    default:
      return "";
  }
}
