"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import {
  type ChatMessage,
  type ChatSession,
  STARTER_PROMPTS,
  createMessage,
  sendChatMessage,
} from "@/lib/adminChat";
import { ApiError } from "@/lib/auth";
import { ChatBubbleIcon, CloseIcon, SendIcon } from "@/components/icons";

// Matches assistant-authored `[label](/relative-path)` link markup. The href
// group is anchored to a literal leading slash so only site-relative paths can
// ever become a href — anything else (javascript:, data:, absolute cross-origin
// URLs) stays inert plain text.
const LINK_PATTERN = /\[([^\]]+)\]\((\/[^)\s]*)\)/g;

function renderMessageText(text: string): React.ReactNode[] {
  const nodes: React.ReactNode[] = [];
  let lastIndex = 0;
  let segmentIndex = 0;
  let match: RegExpExecArray | null;

  LINK_PATTERN.lastIndex = 0;
  while ((match = LINK_PATTERN.exec(text)) !== null) {
    if (match.index > lastIndex) {
      nodes.push(
        <span key={`text-${segmentIndex}`}>{text.slice(lastIndex, match.index)}</span>
      );
      segmentIndex += 1;
    }

    const [, label, href] = match;
    nodes.push(
      <Link
        key={`link-${segmentIndex}`}
        href={href}
        className="text-gold-dark underline underline-offset-2"
      >
        {label}
      </Link>
    );
    segmentIndex += 1;
    lastIndex = LINK_PATTERN.lastIndex;
  }

  if (lastIndex < text.length) {
    nodes.push(<span key={`text-${segmentIndex}`}>{text.slice(lastIndex)}</span>);
  }

  return nodes;
}

function MessageBubble({ message }: { message: ChatMessage }) {
  const isUser = message.role === "user";
  return (
    <div className={`flex ${isUser ? "justify-end" : "justify-start"}`}>
      <div
        className={`max-w-[85%] px-3.5 py-2.5 text-sm leading-relaxed whitespace-pre-line ${
          isUser
            ? "bg-gold text-ink rounded-2xl rounded-br-sm"
            : "bg-surface-alt border border-border text-ink rounded-2xl rounded-bl-sm"
        }`}
      >
        {renderMessageText(message.text)}
      </div>
    </div>
  );
}

function TypingIndicator() {
  return (
    <div className="flex justify-start">
      <div className="bg-surface-alt border border-border rounded-2xl rounded-bl-sm px-3.5 py-2.5 flex items-center gap-1">
        {[0, 150, 300].map((delay) => (
          <span
            key={delay}
            className="w-1.5 h-1.5 bg-gold-dark/70 rounded-full animate-bounce"
            style={{ animationDelay: `${delay}ms` }}
          />
        ))}
      </div>
    </div>
  );
}

/**
 * Staff-side assistant. Answers from live salon data via the authenticated API
 * client, so it only ever shows what the signed-in user could already see.
 * Mounted once in DashboardNav — present on every authenticated page.
 */
export function AdminChatWidget() {
  const [open, setOpen] = useState(false);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState("");
  const [isTyping, setIsTyping] = useState(false);

  const messageListRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const launcherRef = useRef<HTMLButtonElement>(null);
  // Slot-filling state across turns (e.g. "which service?" -> "Scalp Treatment").
  // A ref, not state: it's never rendered, only read/written around the send call.
  const sessionRef = useRef<ChatSession>({});

  useEffect(() => {
    if (!open) return;
    const el = messageListRef.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [messages, isTyping, open]);

  useEffect(() => {
    if (open) inputRef.current?.focus();
  }, [open]);

  useEffect(() => {
    if (!open) return;

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setOpen(false);
        launcherRef.current?.focus();
      }
    }

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [open]);

  async function handleSend(overrideText?: string) {
    const text = (overrideText ?? input).trim();
    if (!text || isTyping) return;

    setMessages((prev) => [...prev, createMessage("user", text)]);
    setInput("");
    setIsTyping(true);

    try {
      const { reply, session } = await sendChatMessage(text, sessionRef.current);
      sessionRef.current = session;
      setMessages((prev) => [...prev, createMessage("assistant", reply)]);
    } catch (error) {
      // A 401 has already redirected to /login via handleUnauthorized; anything
      // else surfaces the server's reason rather than a generic apology.
      if (error instanceof ApiError && error.isUnauthorized) return;
      // A failed turn shouldn't leave the assistant stuck "awaiting" an answer
      // that was never captured.
      sessionRef.current = {};
      const detail =
        error instanceof ApiError
          ? error.message
          : "Something went wrong reading the schedule.";
      setMessages((prev) => [
        ...prev,
        createMessage("assistant", `${detail} [Open the schedule](/schedule)`),
      ]);
    } finally {
      setIsTyping(false);
      inputRef.current?.focus();
    }
  }

  function handleFormSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void handleSend();
  }

  return (
    <>
      <button
        type="button"
        ref={launcherRef}
        onClick={() => setOpen((v) => !v)}
        aria-label={open ? "Close salon assistant" : "Open salon assistant"}
        aria-expanded={open}
        className="fixed bottom-6 right-6 w-14 h-14 bg-gold hover:bg-gold-dark text-ink rounded-full shadow-lg flex items-center justify-center transition-colors z-40"
      >
        {open ? <CloseIcon className="w-6 h-6" /> : <ChatBubbleIcon className="w-6 h-6" />}
      </button>

      {open && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Salon assistant chat"
          className="fixed z-50 inset-x-4 bottom-16 top-16 sm:inset-x-auto sm:top-auto sm:right-6 sm:w-[28rem] sm:h-[40rem] sm:max-h-[85vh] bg-surface border border-border rounded-2xl shadow-2xl flex flex-col overflow-hidden"
        >
          <div className="border-b border-border px-4 py-3 flex items-center justify-between">
            <span className="font-serif text-lg text-ink">Salon Assistant</span>
            <button
              type="button"
              onClick={() => {
                setOpen(false);
                launcherRef.current?.focus();
              }}
              aria-label="Close salon assistant"
              className="text-muted hover:text-gold-dark transition-colors"
            >
              <CloseIcon className="w-5 h-5" />
            </button>
          </div>

          <div
            ref={messageListRef}
            aria-live="polite"
            className="flex-1 overflow-y-auto px-4 py-4 space-y-3"
          >
            {messages.length === 0 && (
              <p className="text-muted text-sm">
                Ask about the day&apos;s bookings, the service menu, or open slots.
              </p>
            )}
            {messages.map((message) => (
              <MessageBubble key={message.id} message={message} />
            ))}
            {isTyping && <TypingIndicator />}
          </div>

          <div className="border-t border-border px-4 py-2.5 flex flex-wrap gap-2">
            {STARTER_PROMPTS.map((prompt) => (
              <button
                key={prompt}
                type="button"
                onClick={() => void handleSend(prompt)}
                disabled={isTyping}
                className="border border-border hover:border-gold-dark text-ink hover:text-gold-dark rounded-full px-3 py-2 min-h-11 text-xs transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
              >
                {prompt}
              </button>
            ))}
          </div>

          <form
            onSubmit={handleFormSubmit}
            className="border-t border-border p-3 flex items-center gap-2"
          >
            <input
              ref={inputRef}
              type="text"
              value={input}
              onChange={(event) => setInput(event.target.value)}
              placeholder="Who's booked tomorrow?"
              aria-label="Message the salon assistant"
              className="w-full bg-surface-alt border border-border hover:border-gold-dark/40 focus:border-gold-dark rounded-xl px-4 py-2.5 text-ink placeholder-muted text-sm outline-none transition-colors"
            />
            <button
              type="submit"
              aria-label="Send message"
              disabled={isTyping || input.trim().length === 0}
              className="w-10 h-10 bg-gold hover:bg-gold-dark text-ink rounded-full flex items-center justify-center flex-shrink-0 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
            >
              <SendIcon className="w-4 h-4" />
            </button>
          </form>
        </div>
      )}
    </>
  );
}
