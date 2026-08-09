import { z } from "zod";
import { ProductSchema } from "@/lib/products";

const API_BASE_URL = (
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5236"
).replace(/\/$/, "");

const SERVICE_REVALIDATE_SECONDS = 60;

export const ServiceSchema = z.object({
  id: z.number(),
  slug: z.string(),
  name: z.string(),
  shortDescription: z.string(),
  longDescription: z.string(),
  category: z.string(),
  durationMinutes: z.number(),
  price: z.number(),
  imageUrl: z.string().nullable(),
  displayOrder: z.number(),
  recommendedProducts: z.array(ProductSchema).optional(),
});

export const ServiceListSchema = z.array(ServiceSchema);

export type Service = z.infer<typeof ServiceSchema>;

export async function fetchServices(): Promise<Service[]> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/services`, {
      next: { revalidate: SERVICE_REVALIDATE_SECONDS },
    });

    if (!response.ok) {
      throw new Error(`Services request failed with ${response.status}`);
    }

    return ServiceListSchema.parse(await response.json());
  } catch {
    return [];
  }
}

export async function fetchServiceBySlug(
  slug: string
): Promise<Service | null> {
  let response: Response;

  try {
    response = await fetch(
      `${API_BASE_URL}/api/services/${encodeURIComponent(slug)}`,
      { next: { revalidate: SERVICE_REVALIDATE_SECONDS } }
    );
  } catch {
    return null;
  }

  if (response.status === 404 || !response.ok) {
    return null;
  }

  return ServiceSchema.parse(await response.json());
}
