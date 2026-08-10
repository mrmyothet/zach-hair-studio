import { z } from "zod";
import { getCartSessionId } from "./cartSession";

const API_BASE_URL = (
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5236"
).replace(/\/$/, "");

export const CART_UPDATED_EVENT = "zhs:cart-updated";

/** Notify Navbar (and other listeners) that the cart changed. */
export function notifyCartUpdated(): void {
  if (typeof window === "undefined") return;
  window.dispatchEvent(new CustomEvent(CART_UPDATED_EVENT));
}

/** Mirrors CartItemResponseDto — UnitPrice/LineTotal/Subtotal are server-computed. */
export const CartItemSchema = z.object({
  productId: z.number(),
  productName: z.string(),
  productSlug: z.string(),
  imageUrl: z.string().nullable().optional(),
  unitPrice: z.number(),
  quantity: z.number(),
  lineTotal: z.number(),
  stock: z.number(),
});

export const CartSchema = z.object({
  sessionKey: z.string(),
  items: z.array(CartItemSchema),
  subtotal: z.number(),
});

export type CartItem = z.infer<typeof CartItemSchema>;
export type Cart = z.infer<typeof CartSchema>;

/** Write body: productId + quantity only (D-05 — never client-trusted money fields). */
export type CartItemUpsertRequest = {
  productId: number;
  quantity: number;
};

/**
 * Typed error for the cart client. `status` is the HTTP status, or `null` for
 * a network/transport failure. Callers branch on 409 / 400 / null.
 */
export class CartApiError extends Error {
  readonly status: number | null;

  constructor(message: string, status: number | null) {
    super(message);
    this.name = "CartApiError";
    this.status = status;
  }

  get isConflict(): boolean {
    return this.status === 409;
  }

  get isValidation(): boolean {
    return this.status === 400;
  }

  get isNetwork(): boolean {
    return this.status === null;
  }
}

async function extractErrorMessage(res: Response): Promise<string> {
  try {
    const body = await res.json();

    if (body?.errors && typeof body.errors === "object") {
      const messages = Object.values(body.errors as Record<string, string[]>)
        .flat()
        .filter(Boolean);
      if (messages.length > 0) return messages.join(" ");
    }

    if (typeof body?.detail === "string" && body.detail.length > 0) {
      return body.detail;
    }
    if (typeof body?.title === "string") return body.title;
  } catch {
    // Response wasn't JSON — fall through.
  }

  return `Something went wrong (${res.status}). Please try again.`;
}

function sessionHeaders(extra?: HeadersInit): Headers {
  const headers = new Headers(extra);
  headers.set("X-Cart-Session-Id", getCartSessionId());
  return headers;
}

async function parseCart(response: Response): Promise<Cart> {
  try {
    return CartSchema.parse(await response.json());
  } catch {
    throw new CartApiError(
      "The cart service returned an unexpected response.",
      response.status
    );
  }
}

/**
 * GET /api/carts — always fresh (no Next cache). Throws CartApiError on failure;
 * never swallows to an empty list (empty items is a valid success payload).
 */
export async function fetchCart(): Promise<Cart> {
  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}/api/carts`, {
      cache: "no-store",
      headers: sessionHeaders(),
    });
  } catch {
    throw new CartApiError(
      "We couldn't reach the cart service. Please check your connection and try again.",
      null
    );
  }

  if (!response.ok) {
    throw new CartApiError(await extractErrorMessage(response), response.status);
  }

  return parseCart(response);
}

/**
 * PUT /api/carts/items — absolute quantity for productId (clamped server-side to stock).
 */
export async function upsertCartItem(
  request: CartItemUpsertRequest
): Promise<Cart> {
  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}/api/carts/items`, {
      method: "PUT",
      headers: sessionHeaders({ "Content-Type": "application/json" }),
      body: JSON.stringify({
        productId: request.productId,
        quantity: request.quantity,
      }),
    });
  } catch {
    throw new CartApiError(
      "We couldn't reach the cart service. Please check your connection and try again.",
      null
    );
  }

  if (!response.ok) {
    throw new CartApiError(await extractErrorMessage(response), response.status);
  }

  const cart = await parseCart(response);
  notifyCartUpdated();
  return cart;
}

/** DELETE /api/carts/items/{productId} */
export async function removeCartItem(productId: number): Promise<Cart> {
  let response: Response;
  try {
    response = await fetch(
      `${API_BASE_URL}/api/carts/items/${encodeURIComponent(String(productId))}`,
      {
        method: "DELETE",
        headers: sessionHeaders(),
      }
    );
  } catch {
    throw new CartApiError(
      "We couldn't reach the cart service. Please check your connection and try again.",
      null
    );
  }

  if (!response.ok) {
    throw new CartApiError(await extractErrorMessage(response), response.status);
  }

  const cart = await parseCart(response);
  notifyCartUpdated();
  return cart;
}

/** Convenience helper — sets absolute quantity via upsert. */
export async function updateQuantity(
  productId: number,
  quantity: number
): Promise<Cart> {
  return upsertCartItem({ productId, quantity });
}

/** Sum of line quantities — used by the Navbar badge. */
export function cartItemCount(cart: Cart): number {
  return cart.items.reduce((sum, item) => sum + item.quantity, 0);
}

export const CheckoutRequestSchema = z.object({
  email: z.string().email(),
  name: z.string().optional(),
  items: z
    .array(
      z.object({
        productId: z.number().int().positive(),
        quantity: z.number().int().positive(),
      })
    )
    .min(1),
});

export type CheckoutRequest = z.infer<typeof CheckoutRequestSchema>;

export const CheckoutResponseSchema = z.object({
  checkoutUrl: z.string().url(),
  orderId: z.number(),
});

export type CheckoutResponse = z.infer<typeof CheckoutResponseSchema>;

/**
 * POST /api/orders/checkout — requires X-Cart-Session-Id (Plan 03 contract).
 * Optional body sessionKey mirrors the same id; never omit the header.
 */
export async function createCheckout(
  input: CheckoutRequest
): Promise<CheckoutResponse> {
  const request = CheckoutRequestSchema.parse(input);
  const sessionKey = getCartSessionId();

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}/api/orders/checkout`, {
      method: "POST",
      headers: sessionHeaders({ "Content-Type": "application/json" }),
      body: JSON.stringify({
        email: request.email,
        name: request.name || undefined,
        sessionKey,
        items: request.items.map((item) => ({
          productId: item.productId,
          quantity: item.quantity,
        })),
      }),
    });
  } catch {
    throw new CartApiError(
      "We couldn't reach our payment provider. Your cart is still saved — please try again.",
      null
    );
  }

  if (!response.ok) {
    throw new CartApiError(await extractErrorMessage(response), response.status);
  }

  try {
    return CheckoutResponseSchema.parse(await response.json());
  } catch {
    throw new CartApiError(
      "The checkout service returned an unexpected response.",
      response.status
    );
  }
}
