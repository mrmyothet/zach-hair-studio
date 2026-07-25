"use client";

import { useEffect, useRef, useState } from "react";
import { API_BASE_URL } from "@/lib/api/client";
import { getToken, handleUnauthorized } from "@/lib/auth";
import { ImageIcon, TrashIcon } from "@/components/icons";

const ALLOWED_TYPES = ["image/jpeg", "image/png", "image/webp"];
const MAX_SIZE_BYTES = 5 * 1024 * 1024; // 5MB
const UPLOAD_ERROR = "Couldn't upload image. Use a JPG, PNG, or WebP under 5MB.";

type Props = {
  /** Null until the service has been saved once (POST /api/Services returns the id). */
  serviceId: number | null;
  imageUrl: string | null;
  disabled?: boolean;
  onUploaded: (imageUrl: string) => void;
  onRemove: () => void;
};

export function ImageUploadField({
  serviceId,
  imageUrl,
  disabled = false,
  onUploaded,
  onRemove,
}: Props) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [imgFailed, setImgFailed] = useState(false);

  useEffect(() => {
    setImgFailed(false);
  }, [imageUrl]);

  function pickFile() {
    inputRef.current?.click();
  }

  async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file || serviceId == null) return;

    setError(null);

    if (!ALLOWED_TYPES.includes(file.type) || file.size > MAX_SIZE_BYTES) {
      setError(UPLOAD_ERROR);
      return;
    }

    setUploading(true);
    try {
      const formData = new FormData();
      formData.append("Image", file);

      const token = getToken();
      const res = await fetch(
        `${API_BASE_URL}/api/Services/${serviceId}/image`,
        {
          method: "POST",
          headers: token ? { Authorization: `Bearer ${token}` } : undefined,
          body: formData,
        }
      );

      if (res.status === 401) {
        handleUnauthorized("Your session has ended. Log in again to continue.");
        return;
      }

      if (!res.ok) {
        setError(UPLOAD_ERROR);
        return;
      }

      const data = (await res.json()) as { imageUrl?: string | null };
      if (data.imageUrl) {
        onUploaded(data.imageUrl);
      }
    } catch {
      setError(
        "We couldn't reach the booking system. Check your connection and try again."
      );
    } finally {
      setUploading(false);
    }
  }

  const showThumbnail = Boolean(imageUrl) && !imgFailed;
  const fieldDisabled = disabled || uploading;

  return (
    <div>
      <label className="text-muted text-xs uppercase tracking-wider block mb-2">
        Image
      </label>
      <div className="flex items-start gap-4">
        <div className="relative h-40 w-40 shrink-0 rounded-xl border-2 border-dashed border-border overflow-hidden flex items-center justify-center bg-surface">
          {showThumbnail ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={`${API_BASE_URL}${imageUrl}`}
              alt=""
              className="h-full w-full object-cover"
              onError={() => setImgFailed(true)}
            />
          ) : (
            <div className="flex flex-col items-center gap-2 px-3 text-center">
              <ImageIcon className="h-6 w-6 text-muted" />
              <span className="text-xs text-muted">No image yet</span>
            </div>
          )}

          {uploading ? (
            <div
              role="status"
              aria-label="Uploading image"
              className="absolute inset-0 bg-surface/80 flex items-center justify-center"
            >
              <span className="h-6 w-6 rounded-full border-2 border-gold-dark border-t-transparent animate-spin" />
            </div>
          ) : null}
        </div>

        <div className="flex flex-col gap-2">
          <input
            ref={inputRef}
            type="file"
            accept="image/jpeg,image/png,image/webp"
            className="sr-only"
            onChange={(e) => {
              void handleFileChange(e);
            }}
          />

          {serviceId == null ? (
            <p className="text-xs text-muted max-w-40">
              Save the service to add an image.
            </p>
          ) : (
            <>
              <button
                type="button"
                onClick={pickFile}
                disabled={fieldDisabled}
                className="min-h-11 px-3 rounded-xl border border-border text-sm text-ink hover:border-gold-dark/40 disabled:opacity-60 disabled:cursor-not-allowed"
              >
                {imageUrl ? "Replace" : "Upload Image"}
              </button>
              {imageUrl ? (
                <button
                  type="button"
                  onClick={onRemove}
                  disabled={fieldDisabled}
                  className="min-h-11 inline-flex items-center gap-1.5 px-3 rounded-xl border border-border text-sm text-ink hover:border-rose-600/40 disabled:opacity-60 disabled:cursor-not-allowed"
                >
                  <TrashIcon className="h-4 w-4" />
                  Remove
                </button>
              ) : null}
            </>
          )}
        </div>
      </div>

      {error ? (
        <p
          role="alert"
          className="mt-2 text-sm text-rose-600 bg-rose-600/5 border border-rose-600/20 rounded-xl px-3 py-2"
        >
          {error}
        </p>
      ) : null}
    </div>
  );
}
