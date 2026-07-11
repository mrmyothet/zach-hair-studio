import createClient, { type Middleware } from "openapi-fetch";
import type { paths } from "./schema";
import { attachToken, handleUnauthorized } from "@/lib/auth";

const API_BASE_URL = (
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5236"
).replace(/\/$/, "");

const SKIP_AUTH_REDIRECT = "X-Skip-Auth-Redirect";

const authMiddleware: Middleware = {
  async onRequest({ request }) {
    attachToken(request.headers);
    return request;
  },
  async onResponse({ request, response }) {
    // Login (and similar) pass X-Skip-Auth-Redirect so a 401 stays on-page
    // with an inline error instead of bouncing through handleUnauthorized.
    if (
      response.status === 401 &&
      !request.headers.has(SKIP_AUTH_REDIRECT)
    ) {
      handleUnauthorized("Your session has ended. Log in again to continue.");
    }
    return response;
  },
};

export const api = createClient<paths>({ baseUrl: API_BASE_URL });
api.use(authMiddleware);

export { API_BASE_URL };
