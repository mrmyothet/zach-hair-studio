"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api/client";
import {
  ApiError,
  extractErrorMessage,
  getSession,
  handleUnauthorized,
  requireAuth,
} from "@/lib/auth";
import { useServices, type ServiceResponseDto } from "@/lib/useServices";
import { DashboardNav } from "@/components/DashboardNav";
import { ServiceForm } from "@/components/ServiceForm";
import { ConfirmDialog, CONFIRM_COPY } from "@/components/ConfirmDialog";
import { API_BASE_URL } from "@/lib/api/client";
import { ImageIcon, PlusIcon } from "@/components/icons";

type FormState =
  | { mode: "closed" }
  | { mode: "create" }
  | { mode: "edit"; service: ServiceResponseDto; isActive: boolean };

/** Small 40x40 avatar-style thumbnail used in the list row's Name cell. */
function RowThumbnail({ imageUrl }: { imageUrl: string | null | undefined }) {
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    setFailed(false);
  }, [imageUrl]);

  if (!imageUrl || failed) {
    return (
      <div className="h-10 w-10 shrink-0 rounded-lg border border-dashed border-border bg-surface flex items-center justify-center">
        <ImageIcon className="h-4 w-4 text-muted" />
      </div>
    );
  }

  return (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src={`${API_BASE_URL}${imageUrl}`}
      alt=""
      className="h-10 w-10 shrink-0 rounded-lg border border-border object-cover"
      onError={() => setFailed(true)}
    />
  );
}

function buildRetirePayload(row: ServiceResponseDto, isActive: boolean) {
  return {
    slug: row.slug ?? "",
    name: row.name ?? "",
    shortDescription: row.shortDescription ?? "",
    longDescription: row.longDescription ?? "",
    category: row.category ?? "",
    durationMinutes: Number(row.durationMinutes ?? 0),
    price: Number(row.price ?? 0),
    imageUrl: row.imageUrl ?? null,
    isActive,
    displayOrder: Number(row.displayOrder ?? 0),
  };
}

export default function ServicesPage() {
  const router = useRouter();
  const [ready, setReady] = useState(false);
  const { services, isLoading, error, mutate } = useServices();
  const [retiredOverrides, setRetiredOverrides] = useState<
    Record<number, ServiceResponseDto>
  >({});
  const [formState, setFormState] = useState<FormState>({ mode: "closed" });
  const [pendingRetire, setPendingRetire] = useState<ServiceResponseDto | null>(
    null
  );
  const [busyId, setBusyId] = useState<number | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  useEffect(() => {
    if (!requireAuth()) return;
    const session = getSession();
    if (!session || session.role !== "Owner") {
      router.replace("/schedule");
      return;
    }
    setReady(true);
  }, [router]);

  if (!ready) {
    return (
      <main className="min-h-screen flex items-center justify-center bg-surface text-muted text-sm">
        Loading…
      </main>
    );
  }

  // GET /api/Services only ever returns Active rows (no API filter param to
  // request retired ones — out of scope for this plan, see 04-02-SUMMARY.md).
  // Retiring/reactivating this session is tracked locally so both actions
  // stay reachable without inventing a new backend surface.
  const retiredList = Object.values(retiredOverrides);
  const rows = [...services, ...retiredList];

  async function handleRetireConfirm() {
    if (!pendingRetire) return;
    const id = Number(pendingRetire.id);
    setBusyId(id);
    setActionError(null);
    try {
      const { response } = await api.PUT("/api/Services/{id}", {
        params: { path: { id } },
        body: buildRetirePayload(pendingRetire, false),
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

      setRetiredOverrides((prev) => ({ ...prev, [id]: pendingRetire }));
      setPendingRetire(null);
      void mutate();
    } catch (err) {
      setActionError(
        err instanceof ApiError ? err.message : "Could not retire service."
      );
    } finally {
      setBusyId(null);
    }
  }

  async function handleReactivate(row: ServiceResponseDto) {
    const id = Number(row.id);
    setBusyId(id);
    setActionError(null);
    try {
      const { response } = await api.PUT("/api/Services/{id}", {
        params: { path: { id } },
        body: buildRetirePayload(row, true),
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

      setRetiredOverrides((prev) => {
        const next = { ...prev };
        delete next[id];
        return next;
      });
      void mutate();
    } catch (err) {
      setActionError(
        err instanceof ApiError ? err.message : "Could not reactivate service."
      );
    } finally {
      setBusyId(null);
    }
  }

  function handleSaved(service: ServiceResponseDto, isActive: boolean) {
    const id = Number(service.id);
    setRetiredOverrides((prev) => {
      if (isActive) {
        if (!(id in prev)) return prev;
        const next = { ...prev };
        delete next[id];
        return next;
      }
      return { ...prev, [id]: service };
    });
    void mutate();
  }

  if (formState.mode !== "closed") {
    return (
      <main className="min-h-screen bg-surface text-ink">
        <DashboardNav />
        <div className="px-4 md:px-6 py-10 flex justify-center">
          <ServiceForm
            mode={formState.mode}
            service={formState.mode === "edit" ? formState.service : undefined}
            initialIsActive={
              formState.mode === "edit" ? formState.isActive : true
            }
            onSaved={handleSaved}
            onCancel={() => setFormState({ mode: "closed" })}
          />
        </div>
      </main>
    );
  }

  const confirmCopy = pendingRetire
    ? CONFIRM_COPY.Retired(pendingRetire.name ?? "this service")
    : null;

  return (
    <main className="min-h-screen bg-surface text-ink">
      <DashboardNav />

      <div className="px-4 md:px-6 py-6 flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-lg font-semibold text-ink">Services</h1>
        <button
          type="button"
          onClick={() => setFormState({ mode: "create" })}
          className="min-h-11 inline-flex items-center gap-2 px-4 rounded-xl bg-gold-dark hover:bg-gold text-white text-sm font-semibold"
        >
          <PlusIcon className="h-4 w-4" />
          Add Service
        </button>
      </div>

      {actionError ? (
        <p
          role="alert"
          className="mx-4 md:mx-6 mb-4 text-sm text-rose-600 bg-rose-600/5 border border-rose-600/20 rounded-xl px-3 py-2"
        >
          {actionError}
        </p>
      ) : null}

      {isLoading && rows.length === 0 ? (
        <div className="px-4 md:px-6 space-y-2">
          {[0, 1, 2].map((i) => (
            <div
              key={i}
              className="h-14 rounded-xl bg-surface-alt animate-pulse"
            />
          ))}
        </div>
      ) : null}

      {error && rows.length === 0 ? (
        <div className="px-4 md:px-6 max-w-lg">
          <h2 className="text-lg font-semibold text-ink">
            Couldn&apos;t Load Services.
          </h2>
          <p className="text-sm text-muted mt-2">
            We couldn&apos;t reach the booking system. Try refreshing, or
            check your connection.
          </p>
          <button
            type="button"
            onClick={() => {
              void mutate();
            }}
            className="mt-4 min-h-11 px-4 rounded-xl bg-gold-dark text-white text-sm font-semibold"
          >
            Refresh
          </button>
        </div>
      ) : null}

      {!isLoading && !error && rows.length === 0 ? (
        <div className="px-4 md:px-6 max-w-lg">
          <h2 className="text-lg font-semibold text-ink">No Services Yet</h2>
          <p className="text-sm text-muted mt-2">
            Add your first service to start taking bookings.
          </p>
        </div>
      ) : null}

      {rows.length > 0 ? (
        <div className="px-4 md:px-6 pb-10 overflow-x-auto">
          <table className="w-full text-sm border-collapse">
            <thead>
              <tr className="text-left text-xs uppercase tracking-wider text-muted border-b border-border">
                <th className="py-3 pr-4 font-normal">Name</th>
                <th className="py-3 pr-4 font-normal">Category</th>
                <th className="py-3 pr-4 font-normal">Duration</th>
                <th className="py-3 pr-4 font-normal">Price</th>
                <th className="py-3 pr-4 font-normal">Status</th>
                <th className="py-3 pr-4 font-normal">Actions</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => {
                const id = Number(row.id);
                const retired = id in retiredOverrides;
                return (
                  <tr
                    key={id}
                    className={
                      retired
                        ? "border-b border-border border-l-4 border-l-border bg-surface-alt/60"
                        : "border-b border-border bg-surface-alt"
                    }
                  >
                    <td className="py-3 pr-4">
                      <div className="flex items-center gap-3 max-w-52">
                        <RowThumbnail imageUrl={row.imageUrl} />
                        <span className="truncate" title={row.name ?? ""}>
                          {row.name}
                        </span>
                      </div>
                    </td>
                    <td
                      className="py-3 pr-4 max-w-32 truncate"
                      title={row.category ?? ""}
                    >
                      {row.category}
                    </td>
                    <td className="py-3 pr-4 whitespace-nowrap">
                      {row.durationMinutes} min
                    </td>
                    <td className="py-3 pr-4 whitespace-nowrap">
                      ${Number(row.price ?? 0).toFixed(2)}
                    </td>
                    <td className="py-3 pr-4">
                      {retired ? (
                        <span className="inline-flex items-center rounded-full bg-surface-alt/60 px-2 py-0.5 text-xs uppercase tracking-wider text-muted">
                          Retired
                        </span>
                      ) : null}
                    </td>
                    <td className="py-3 pr-4">
                      <div className="flex items-center gap-3">
                        <button
                          type="button"
                          onClick={() =>
                            setFormState({
                              mode: "edit",
                              service: row,
                              isActive: !retired,
                            })
                          }
                          className="text-sm text-ink hover:text-gold-dark"
                        >
                          Edit
                        </button>
                        {retired ? (
                          <button
                            type="button"
                            onClick={() => {
                              void handleReactivate(row);
                            }}
                            disabled={busyId === id}
                            className="text-sm text-ink hover:text-gold-dark disabled:opacity-60"
                          >
                            {busyId === id ? "Working…" : "Reactivate"}
                          </button>
                        ) : (
                          <button
                            type="button"
                            onClick={() => setPendingRetire(row)}
                            disabled={busyId === id}
                            className="text-sm text-rose-600 hover:text-rose-700 disabled:opacity-60"
                          >
                            Retire
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      ) : null}

      <ConfirmDialog
        open={Boolean(pendingRetire && confirmCopy)}
        title={confirmCopy?.title ?? ""}
        body={confirmCopy?.body ?? ""}
        confirmLabel={confirmCopy?.confirmLabel ?? "Confirm"}
        onConfirm={() => {
          void handleRetireConfirm();
        }}
        onCancel={() => setPendingRetire(null)}
        busy={pendingRetire ? busyId === Number(pendingRetire.id) : false}
      />
    </main>
  );
}
