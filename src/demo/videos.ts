// Provides the sample catalog and browser-tab uploads without an API.
import { ApiError } from "../api/client";
import type { ListVideosResult, Video } from "../types/video";

const samples = [
  ["food", "نظام الطيبات", "مقطع من برنامجكم الدارج المحبوب جدًا عن نظام الطيبات."],
  ["university", "الجامعة", "مقطع كوميدي عن الحياة في الجامعة والمواقف اللي بتحصل فيها."],
  ["campus-characters", "شخصيات الجامعة", "مقطع كوميدي عن الشخصيات المختلفة اللي ممكن تقابلها في الجامعة."],
  ["eid", "عيد", "مقطع من سلسلة «ده فيديو بيضحك» عن العيد."],
  ["night-sky", "عينيكي لون السما باليل", "مقطع من سلسلة «ده فيديو بيضحك» بعنوان «عينيكي لون السما باليل»."],
];

const demoVideos: Video[] = samples.map(([id, title, description], index) => {
  const folder = `/demo/v1/${id}`;
  return {
    id, title, description, status: "Ready", tag: "30-second preview",
    masterPlaylistUrl: `${folder}/master.m3u8`,
    thumbnailUrl: `${folder}/poster.jpg`,
    errorMessage: null,
    createdAt: new Date(Date.UTC(2026, 10, 15 - index)).toISOString(),
  };
});

type LocalUpload = { video: Video; playbackUrl: string; readyAt: number };

const uploads = new Map<string, LocalUpload>();
const MAX_UPLOADS = 5;
const MAX_BYTES = 50 * 1024 * 1024;
const demoFormats: Record<string, string> = { mp4: "video/mp4", webm: "video/webm" };

export function addDemoUpload(title: string, file: File, description: string): string {
  const extension = file.name.split(".").pop()?.toLowerCase() ?? "";
  const mediaType = demoFormats[extension];
  if (!mediaType || !Object.hasOwn(demoFormats, extension)) {
    throw new ApiError("Choose an MP4 or WebM file for the demo.", 400);
  }
  if (file.size === 0 || file.size > MAX_BYTES) {
    throw new ApiError("Choose a video between 1 byte and 50 MB.", 400);
  }
  if (uploads.size >= MAX_UPLOADS) {
    throw new ApiError("This tab already has five uploads. Reload to start a new session.", 400);
  }
  const id = crypto.randomUUID();
  const playbackUrl = URL.createObjectURL(file.type === mediaType ? file : new Blob([file], { type: mediaType }));
  uploads.set(id, {
    video: {
      id, title, description, mediaType, status: "Processing", tag: "Local upload",
      masterPlaylistUrl: null, thumbnailUrl: null, errorMessage: null, createdAt: new Date().toISOString(),
    },
    playbackUrl,
    readyAt: Date.now() + 1200,
  });
  return id;
}

function currentVideo(upload: LocalUpload): Video {
  return Date.now() >= upload.readyAt
    ? { ...upload.video, status: "Ready", masterPlaylistUrl: upload.playbackUrl }
    : upload.video;
}

export function getDemoVideo(id: string): Video {
  const upload = uploads.get(id);
  const video = upload ? currentVideo(upload) : demoVideos.find((sample) => sample.id === id);
  if (!video) throw new ApiError("This video does not exist in this demo session.", 404);
  return video;
}

export function listDemoVideos(page: number, pageSize: number, q?: string): ListVideosResult {
  const search = q?.trim().toLowerCase() ?? "";
  const videos = [...Array.from(uploads.values(), currentVideo).reverse(), ...demoVideos];
  const matches = videos.filter((video) => video.title.toLowerCase().includes(search));
  const start = (page - 1) * pageSize;
  return { items: matches.slice(start, start + pageSize), totalCount: matches.length, page, pageSize };
}
