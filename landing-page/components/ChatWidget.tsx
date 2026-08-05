"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import type { Service } from "@/lib/services";
import type { Stylist } from "@/lib/appointments";
import {
  type ChatMessage,
  STARTER_PROMPTS,
  createMessage,
  sendChatMessage,
} from "@/lib/chat";
import { ChatBubbleIcon, CloseIcon, SendIcon } from "./icons";

const inputClass =
  "w-full bg-charcoal-light border border-white/10 hover:border-gold/30 focus:border-gold rounded-xl px-4 py-2.5 text-white placeholder-gray-600 text-sm outline-none transition-colors";

// Matches assistant-authored `[label](/relative-path)` link markup. The href
// group is anchored to a literal leading slash so only site-relative paths
// can ever become a href — anything else (javascript:, data:, absolute
// cross-origin URLs) stays inert plain text.
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
        className="text-gold underline underline-offset-2"
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
            ? "bg-gold text-charcoal rounded-2xl rounded-br-sm"
            : "bg-charcoal-light border border-white/10 text-gray-200 rounded-2xl rounded-bl-sm"
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
      <div className="bg-charcoal-light border border-white/10 rounded-2xl rounded-bl-sm px-3.5 py-2.5 flex items-center gap-1">
        <span
          className="w-1.5 h-1.5 bg-gold/70 rounded-full animate-bounce"
          style={{ animationDelay: "0ms" }}
        />
        <span
          className="w-1.5 h-1.5 bg-gold/70 rounded-full animate-bounce"
          style={{ animationDelay: "150ms" }}
        />
        <span
          className="w-1.5 h-1.5 bg-gold/70 rounded-full animate-bounce"
          style={{ animationDelay: "300ms" }}
        />
      </div>
    </div>
  );
}

function StarterChips({ onSelect }: { onSelect: (prompt: string) => void }) {
  return (
    <div className="flex flex-wrap gap-2">
      {STARTER_PROMPTS.map((prompt) => (
        <button
          key={prompt}
          type="button"
          onClick={() => onSelect(prompt)}
          className="border border-gold/20 hover:border-gold text-gray-300 hover:text-gold rounded-full px-3 py-1.5 text-xs transition-colors"
        >
          {prompt}
        </button>
      ))}
    </div>
  );
}

type Props = {
  services: Service[];
  stylists?: Stylist[];
};

export default function ChatWidget({ services, stylists = [] }: Props) {
  const [open, setOpen] = useState(false);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState("");
  const [isTyping, setIsTyping] = useState(false);

  const messageListRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const launcherRef = useRef<HTMLButtonElement>(null);

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
      const reply = await sendChatMessage(text, messages, services, stylists);
      setMessages((prev) => [...prev, createMessage("assistant", reply)]);
    } catch {
      setMessages((prev) => [
        ...prev,
        createMessage(
          "assistant",
          "Sorry, something went wrong on my end. You can always book directly: [Book an appointment](/book)"
        ),
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

  function handleInputKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      void handleSend();
    }
  }

  return (
    <>
      <button
        type="button"
        ref={launcherRef}
        onClick={() => setOpen((v) => !v)}
        aria-label={open ? "Close booking assistant" : "Open booking assistant"}
        aria-expanded={open}
        className="fixed bottom-6 right-24 w-14 h-14 bg-gold hover:bg-gold-dark text-charcoal rounded-full shadow-lg flex items-center justify-center transition-all duration-300 z-40"
      >
        {open ? <CloseIcon className="w-6 h-6" /> : <ChatBubbleIcon className="w-6 h-6" />}
      </button>

      {open && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Booking assistant chat"
          className="fixed z-50 inset-x-4 bottom-24 top-24 sm:inset-x-auto sm:top-auto sm:right-6 sm:w-96 sm:h-[32rem] sm:max-h-[70vh] bg-charcoal border border-gold/20 rounded-2xl shadow-2xl flex flex-col overflow-hidden"
        >
          <div className="border-b border-gold/20 px-4 py-3 flex items-center justify-between">
            <span className="font-serif text-gold">Booking Assistant</span>
            <button
              type="button"
              onClick={() => {
                setOpen(false);
                launcherRef.current?.focus();
              }}
              aria-label="Close booking assistant"
              className="text-gray-400 hover:text-gold transition-colors"
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
              <div className="space-y-3">
                <p className="text-gray-400 text-sm">
                  Hi! Ask me about services, pricing, hours, or booking.
                </p>
                <StarterChips onSelect={(prompt) => void handleSend(prompt)} />
              </div>
            )}
            {messages.map((message) => (
              <MessageBubble key={message.id} message={message} />
            ))}
            {isTyping && <TypingIndicator />}
          </div>

          <form
            onSubmit={handleFormSubmit}
            className="border-t border-white/10 p-3 flex items-center gap-2"
          >
            <input
              ref={inputRef}
              type="text"
              value={input}
              onChange={(event) => setInput(event.target.value)}
              onKeyDown={handleInputKeyDown}
              placeholder="Ask about services, pricing, hours..."
              className={inputClass}
            />
            <button
              type="submit"
              aria-label="Send message"
              disabled={isTyping || input.trim().length === 0}
              className="w-10 h-10 bg-gold hover:bg-gold-dark text-charcoal rounded-full flex items-center justify-center flex-shrink-0 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
            >
              <SendIcon className="w-4 h-4" />
            </button>
          </form>
        </div>
      )}
    </>
  );
}
