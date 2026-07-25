"use client";

import { FormEvent, useState } from "react";
import { api } from "@/lib/api/client";
import { ApiError, extractErrorMessage, handleUnauthorized } from "@/lib/auth";
import { ImageUploadField } from "@/components/ImageUploadField";
import type { ServiceResponseDto } from "@/lib/useServices";

// Mirrors the inputClass/Field styling defined locally in
// dashboard/app/staff/new/page.tsx (not exported there, so kept in sync here
// rather than inventing a second visual style).
const inputClass =
  "w-full bg-surface border border-border hover:border-gold-dark/40 focus:border-gold-dark rounded-xl px-4 py-3 text-ink placeholder:text-muted/60 text-sm outline-none transition-colors";

function Field({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div>
      <label className="text-muted text-xs uppercase tracking-wider block mb-2">
        {label}
      </label>
      {children}
    </div>
  );
}

/** slug isn't a UI-SPEC field — derived from Name so ServiceCreateDto's required slug is satisfied without exposing it. */
function slugify(value: string): string {
  return value
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

type Props = {
  mode: "create" | "edit";
  service?: ServiceResponseDto;
  /**
   * Whether the service being edited is currently Active. The services list
   * supplies this from the server-returned `isActive` status (an Owner-only
   * field on the includeInactive listing); ServiceForm always echoes it back
   * unchanged — only Retire/Reactivate on the list change it. Ignored in
   * create mode (the API always creates Active).
   */
  initialIsActive?: boolean;
  onSaved: (service: ServiceResponseDto, isActive: boolean) => void;
  onCancel: () => void;
};

export function ServiceForm({
  mode,
  service,
  initialIsActive = true,
  onSaved,
  onCancel,
}: Props) {
  const [serviceId, setServiceId] = useState<number | null>(
    mode === "edit" && service?.id != null ? Number(service.id) : null
  );
  const [slug, setSlug] = useState<string | undefined>(service?.slug);
  const [name, setName] = useState(service?.name ?? "");
  const [shortDescription, setShortDescription] = useState(
    service?.shortDescription ?? ""
  );
  const [longDescription, setLongDescription] = useState(
    service?.longDescription ?? ""
  );
  const [category, setCategory] = useState(service?.category ?? "");
  const [durationMinutes, setDurationMinutes] = useState(
    service?.durationMinutes != null ? String(service.durationMinutes) : ""
  );
  const [price, setPrice] = useState(
    service?.price != null ? String(service.price) : ""
  );
  const [imageUrl, setImageUrl] = useState<string | null>(
    service?.imageUrl ?? null
  );
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [removingImage, setRemovingImage] = useState(false);

  const canSave =
    name.trim() !== "" &&
    shortDescription.trim() !== "" &&
    longDescription.trim() !== "" &&
    category.trim() !== "" &&
    durationMinutes.trim() !== "" &&
    price.trim() !== "";

  // Branches on serviceId (has this row been persisted yet?), not the fixed
  // `mode` prop — after a successful create the form stays open (so the
  // Owner can attach an image without leaving the page, per the plan's
  // "upload after create" option) and subsequent Saves must PUT.
  function buildPayload(overrides?: { imageUrl?: string | null }) {
    const nextImageUrl =
      overrides && "imageUrl" in overrides ? overrides.imageUrl ?? null : imageUrl;
    const isUpdate = serviceId != null;
    return {
      slug: slug ?? slugify(name),
      name,
      shortDescription,
      longDescription,
      category,
      durationMinutes: Number(durationMinutes),
      price: Number(price),
      imageUrl: nextImageUrl,
      ...(isUpdate
        ? {
            isActive: initialIsActive,
            displayOrder:
              service?.displayOrder != null ? Number(service.displayOrder) : 0,
          }
        : {}),
    };
  }

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSuccess(null);
    setSubmitting(true);

    try {
      if (serviceId == null) {
        const { data, response } = await api.POST("/api/Services", {
          body: buildPayload(),
        });

        if (response.status === 401) {
          handleUnauthorized(
            "Your session has ended. Log in again to continue."
          );
          return;
        }
        // API returns 201 Created; the typed schema only documents 200, so
        // openapi-fetch can leave `data` untyped for this status — same
        // pattern as staff/new/page.tsx's create flow.
        if (!response.ok || !data) {
          const message = await extractErrorMessage(response.clone());
          throw new ApiError(message, response.status || null);
        }

        setServiceId(Number(data.id));
        setSlug(data.slug);
        setSuccess("Service saved.");
        onSaved(data, true);
      } else {
        const id = serviceId;
        const payload = buildPayload();
        const { response } = await api.PUT("/api/Services/{id}", {
          params: { path: { id } },
          body: payload,
        });

        if (response.status === 401) {
          handleUnauthorized(
            "Your session has ended. Log in again to continue."
          );
          return;
        }
        if (!response.ok) {
          const message = await extractErrorMessage(response.clone());
          throw new ApiError(message, response.status || null);
        }

        // PUT returns 204 No Content — rebuild the dto from the submitted
        // field values rather than re-fetching.
        const updated: ServiceResponseDto = {
          id,
          slug: payload.slug,
          name: payload.name,
          shortDescription: payload.shortDescription,
          longDescription: payload.longDescription,
          category: payload.category,
          durationMinutes: payload.durationMinutes,
          price: payload.price,
          imageUrl: payload.imageUrl,
          displayOrder: payload.displayOrder,
        };
        setSuccess("Service saved.");
        onSaved(updated, initialIsActive);
      }
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else if (err instanceof TypeError) {
        setError(
          "We couldn't reach the booking system. Check your connection and try again."
        );
      } else {
        setError("Could not save service.");
      }
    } finally {
      setSubmitting(false);
    }
  }

  async function handleRemoveImage() {
    if (serviceId == null) {
      // Not-yet-created service — nothing persisted yet, just clear locally.
      setImageUrl(null);
      return;
    }
    setRemovingImage(true);
    setError(null);
    try {
      const { response } = await api.PUT("/api/Services/{id}", {
        params: { path: { id: serviceId } },
        body: buildPayload({ imageUrl: null }),
      });

      if (response.status === 401) {
        handleUnauthorized(
          "Your session has ended. Log in again to continue."
        );
        return;
      }
      if (!response.ok) {
        const message = await extractErrorMessage(response.clone());
        throw new ApiError(message, response.status || null);
      }

      setImageUrl(null);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Could not remove image.");
      }
    } finally {
      setRemovingImage(false);
    }
  }

  return (
    <div className="w-full max-w-lg bg-surface-alt rounded-2xl p-8 border border-border shadow-sm">
      <div className="mb-6 flex items-start justify-between gap-4">
        <h2 className="text-lg font-semibold text-ink">
          {mode === "create" ? "Add Service" : "Edit Service"}
        </h2>
        <button
          type="button"
          onClick={onCancel}
          className="min-h-11 inline-flex items-center text-sm text-muted hover:text-ink"
        >
          Back
        </button>
      </div>

      {error ? (
        <p
          role="alert"
          className="mb-5 text-sm text-rose-600 bg-rose-600/5 border border-rose-600/20 rounded-xl px-3 py-2"
        >
          {error}
        </p>
      ) : null}

      {success ? (
        <p
          role="status"
          className="mb-5 text-sm text-ink bg-surface border border-border rounded-xl px-3 py-2"
        >
          {success}
        </p>
      ) : null}

      <form onSubmit={handleSubmit} className="space-y-5">
        <Field label="Name">
          <input
            type="text"
            required
            value={name}
            onChange={(e) => setName(e.target.value)}
            className={inputClass}
            disabled={submitting}
          />
        </Field>

        <Field label="Short description">
          <input
            type="text"
            required
            value={shortDescription}
            onChange={(e) => setShortDescription(e.target.value)}
            className={inputClass}
            disabled={submitting}
          />
        </Field>

        <Field label="Long description">
          <textarea
            required
            rows={4}
            value={longDescription}
            onChange={(e) => setLongDescription(e.target.value)}
            className={`${inputClass} resize-none overflow-y-auto`}
            disabled={submitting}
          />
        </Field>

        <Field label="Category">
          <input
            type="text"
            required
            value={category}
            onChange={(e) => setCategory(e.target.value)}
            className={inputClass}
            disabled={submitting}
          />
        </Field>

        <Field label="Duration (minutes)">
          <input
            type="number"
            required
            min={1}
            max={480}
            value={durationMinutes}
            onChange={(e) => setDurationMinutes(e.target.value)}
            className={inputClass}
            disabled={submitting}
          />
        </Field>

        <Field label="Price">
          <input
            type="number"
            required
            min={0}
            step="0.01"
            value={price}
            onChange={(e) => setPrice(e.target.value)}
            className={inputClass}
            disabled={submitting}
          />
        </Field>

        <ImageUploadField
          serviceId={serviceId}
          imageUrl={imageUrl}
          disabled={submitting || removingImage}
          onUploaded={(url) => setImageUrl(url)}
          onRemove={() => {
            void handleRemoveImage();
          }}
        />

        <button
          type="submit"
          disabled={submitting || !canSave}
          className="w-full bg-gold-dark hover:bg-gold text-white font-medium text-sm rounded-xl px-4 py-3 transition-colors disabled:opacity-60 disabled:cursor-not-allowed min-h-11"
        >
          {submitting
            ? "Saving…"
            : serviceId == null
              ? "Add Service"
              : "Save Service"}
        </button>
      </form>
    </div>
  );
}
