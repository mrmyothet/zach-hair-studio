import type { Service } from "@/lib/services";
import { formatDuration } from "@/lib/formatDuration";

/**
 * Chat message model shared by the mock engine below and the ChatWidget UI.
 */
export type ChatRole = "user" | "assistant";

export type ChatMessage = {
  id: string;
  role: ChatRole;
  text: string;
};

/**
 * Booking-assistant openers shown as chips while the conversation is empty.
 */
export const STARTER_PROMPTS: readonly string[] = [
  "What services do you offer?",
  "How much does a haircut cost?",
  "What are your opening hours?",
  "I'd like to book an appointment",
];

// Module-scoped counter so ids are deterministic (no crypto.randomUUID / Date.now,
// which would risk SSR/hydration id mismatches on a client-only chat surface).
let messageIdCounter = 0;

export function createMessage(role: ChatRole, text: string): ChatMessage {
  messageIdCounter += 1;
  return { id: `msg-${messageIdCounter}`, role, text };
}

const MOCK_REPLY_DELAY_MS = 600;

// Mirrors the salon hours copy shown in Contact.tsx.
const SALON_HOURS = "Open Daily: 9:00 AM – 7:30 PM";

const priceFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});

const BOOK_LINK = "[Book an appointment](/book)";

function byDisplayOrder(services: Service[]): Service[] {
  return services.toSorted((a, b) => a.displayOrder - b.displayOrder);
}

function findMatchingService(
  normalizedInput: string,
  services: Service[]
): Service | undefined {
  return byDisplayOrder(services).find((service) => {
    const name = service.name.toLowerCase();
    const slugAsWords = service.slug.replace(/-/g, " ").toLowerCase();
    return normalizedInput.includes(name) || normalizedInput.includes(slugAsWords);
  });
}

/**
 * Single seam for a real backend: this is the ONLY function a future chat API
 * needs to replace. Swap the body below for a `fetch` to the real endpoint —
 * the signature and return type (Promise<string>) stay identical, so no
 * ChatWidget code needs to change. `history` is accepted now (for a future
 * request payload) but unused by this mock.
 */
export async function sendChatMessage(
  userText: string,
  history: ChatMessage[],
  services: Service[]
): Promise<string> {
  await new Promise((resolve) => setTimeout(resolve, MOCK_REPLY_DELAY_MS));

  const normalizedInput = userText.toLowerCase().trim();

  // a. Service name match
  const matchedService = findMatchingService(normalizedInput, services);
  if (matchedService) {
    return (
      `${matchedService.name} is ${priceFormatter.format(matchedService.price)} ` +
      `and takes about ${formatDuration(matchedService.durationMinutes)}. ` +
      `${matchedService.shortDescription} ` +
      `[Book ${matchedService.name}](/book?service=${matchedService.slug})`
    );
  }

  // b. book / appointment / schedule
  if (/\b(book|appointment|schedule)\b/.test(normalizedInput)) {
    return `Let's get you booked in! Head over to our booking page to pick a time that works for you. ${BOOK_LINK}`;
  }

  // c. price / cost / how much
  if (/\b(price|cost|how much)\b/.test(normalizedInput)) {
    const cheapest = byDisplayOrder(services).slice(0, 3);
    if (cheapest.length === 0) {
      return `Pricing varies by service — head to our booking page to see current rates. ${BOOK_LINK}`;
    }
    const lines = cheapest
      .map((service) => `${service.name} — ${priceFormatter.format(service.price)}`)
      .join(", ");
    return `Here are a few of our services and prices: ${lines}. ${BOOK_LINK}`;
  }

  // d. hour / open / close
  if (/\b(hours?|open|close)\b/.test(normalizedInput)) {
    return `${SALON_HOURS}. ${BOOK_LINK}`;
  }

  // e. service / offer / do you do
  if (/\b(service|offer|do you do)\b/.test(normalizedInput)) {
    const allServices = byDisplayOrder(services);
    if (allServices.length === 0) {
      return `We offer a full range of hair styling and coloring services — head to our booking page to see them. ${BOOK_LINK}`;
    }
    const names = allServices.map((service) => service.name).join(", ");
    return `We offer: ${names}. ${BOOK_LINK}`;
  }

  // f. fallback
  return (
    "I'm your booking assistant — I can help with services, pricing, hours, " +
    `or getting you booked in. What would you like to know? ${BOOK_LINK}`
  );
}
