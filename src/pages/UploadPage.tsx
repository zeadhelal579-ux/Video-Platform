// File: UploadPage.tsx
import { useNavigate } from "react-router-dom";
import { VideoUploader } from "../components/video/VideoUploader";

export function UploadPage() {
  const navigate = useNavigate();

  return (
    <div className="mx-auto max-w-xl px-4 py-8 sm:px-6 sm:py-10">
      <h1 className="font-display text-2xl font-bold text-ink">Upload a video</h1>
      <p className="mt-1.5 text-sm text-ink-faint">
        After upload, you can follow processing from the watch page.
      </p>

      <div className="mt-8">
        <VideoUploader onUploaded={(videoId) => navigate(`/watch/${videoId}`)} />
      </div>
    </div>
  );
}
