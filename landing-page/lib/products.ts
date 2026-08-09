import { z } from "zod";

const API_BASE_URL = (
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5236"
).replace(/\/$/, "");

const PRODUCT_REVALIDATE_SECONDS = 60;

export const ProductSchema = z.object({
  id: z.number(),
  slug: z.string(),
  name: z.string(),
  shortDescription: z.string(),
  longDescription: z.string(),
  category: z.string(),
  price: z.number(),
  stock: z.number(),
  imageUrl: z.string().nullable(),
});

export const ProductListSchema = z.array(ProductSchema);

export type Product = z.infer<typeof ProductSchema>;

export async function fetchProducts(): Promise<Product[]> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/products`, {
      next: { revalidate: PRODUCT_REVALIDATE_SECONDS },
    });

    if (!response.ok) {
      throw new Error(`Products request failed with ${response.status}`);
    }

    return ProductListSchema.parse(await response.json());
  } catch {
    return [];
  }
}

export async function fetchProductBySlug(
  slug: string
): Promise<Product | null> {
  let response: Response;

  try {
    response = await fetch(
      `${API_BASE_URL}/api/products/${encodeURIComponent(slug)}`,
      { next: { revalidate: PRODUCT_REVALIDATE_SECONDS } }
    );
  } catch {
    return null;
  }

  if (response.status === 404 || !response.ok) {
    return null;
  }

  return ProductSchema.parse(await response.json());
}
