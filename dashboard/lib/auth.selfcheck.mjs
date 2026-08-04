/**
 * Self-check for extractErrorMessage's body-shape handling.
 * Run: node lib/auth.selfcheck.mjs   (exits non-zero on failure)
 *
 * The function under test is pure and dependency-free, so it is inlined here
 * rather than importing the .ts through a build step. Keep in sync with lib/auth.ts.
 */
import assert from "node:assert/strict";

function extractErrorMessage(body, status) {
  if (body && typeof body === "object") {
    const problem = body;
    if (problem.errors && typeof problem.errors === "object") {
      const messages = Object.values(problem.errors).flat().filter(Boolean);
      if (messages.length > 0) return messages.join(" ");
    }
    if (typeof problem.detail === "string" && problem.detail.length > 0) {
      return problem.detail;
    }
    if (typeof problem.title === "string" && problem.title.length > 0) {
      return problem.title;
    }
  }
  if (typeof body === "string" && body.trim().length > 0) return body;
  return `Something went wrong (${status}). Please try again.`;
}

// ModelState — the shape that was being swallowed (G-04-7). Both real causes:
assert.equal(
  extractErrorMessage(
    { errors: { "": ["Username 'a@b.com' is already taken."] } }, 400
  ),
  "Username 'a@b.com' is already taken."
);
assert.equal(
  extractErrorMessage(
    { errors: { "": ["Passwords must have at least one digit ('0'-'9').",
                     "Passwords must have at least one uppercase ('A'-'Z')."] } }, 400
  ),
  "Passwords must have at least one digit ('0'-'9'). " +
  "Passwords must have at least one uppercase ('A'-'Z')."
);

// Multiple keys flatten across fields.
assert.equal(
  extractErrorMessage({ errors: { Email: ["Bad email."], Password: ["Too short."] } }, 400),
  "Bad email. Too short."
);

// ProblemDetails precedence: errors > detail > title.
assert.equal(extractErrorMessage({ detail: "Boom.", title: "Error" }, 500), "Boom.");
assert.equal(extractErrorMessage({ title: "Conflict" }, 409), "Conflict");

// Degenerate bodies fall back to the status line rather than rendering "" or "undefined".
assert.match(extractErrorMessage({ errors: {} }, 400), /^Something went wrong \(400\)/);
assert.match(extractErrorMessage({ detail: "" }, 500), /^Something went wrong \(500\)/);
assert.match(extractErrorMessage(null, 502), /^Something went wrong \(502\)/);
assert.match(extractErrorMessage(undefined, 503), /^Something went wrong \(503\)/);
assert.match(extractErrorMessage("   ", 500), /^Something went wrong \(500\)/);

// Non-JSON error bodies (proxy HTML / plain text) pass through.
assert.equal(extractErrorMessage("Bad Gateway", 502), "Bad Gateway");

console.log("auth.selfcheck: all assertions passed");
