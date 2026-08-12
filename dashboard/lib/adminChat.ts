import { api } from "@/lib/api/client";
import { ApiError, extractErrorMessage, handleUnauthorized } from "@/lib/auth";

export type ChatRole = "user" | "assistant";

export type ChatMessage = {
  id: string;
  role: ChatRole;
  text: string;
};

export const STARTER_PROMPTS: readonly string[] = [
  "Who's booked today?",
  "What's on tomorrow?",
  "List services and prices",
  "Any haircut openings tomorrow?",
];

let messageIdCounter = 0;

export function createMessage(role: ChatRole, text: string): ChatMessage {
  messageIdCounter += 1;
  return { id: `msg-${messageIdCounter}`, role, text };
}

export async function sendChatMessage(messages: ChatMessage[]): Promise<string> {
  const result = await api.POST("/api/chat", {
    body: {
      messages: messages.map(({ role, text }) => ({ role, content: text })),
    },
  });

  if (result.response.status === 401) {
    handleUnauthorized("Your session has ended. Log in again to continue.");
    throw new ApiError("Unauthorized", 401);
  }
  if (!result.response.ok || result.error) {
    throw new ApiError(
      extractErrorMessage(result.error, result.response.status),
      result.response.status || null
    );
  }

  const reply = result.data?.reply?.trim();
  if (!reply) {
    throw new ApiError("The salon assistant returned an empty response.", null);
  }
  return reply;
}
