/**
 * Self-check for findMatchingService's category + alias matching.
 * Run: node lib/chat.selfcheck.mjs   (exits non-zero on failure)
 *
 * The function under test is pure and dependency-free, so it is inlined here
 * rather than importing the .ts through a build step. Keep in sync with lib/chat.ts.
 */
import assert from "node:assert/strict";

const CATEGORY_ALIASES = {
  cuts: ["cut", "haircut", "hair cut"],
  color: ["dye", "colour"],
  styling: ["style"],
  treatments: ["treatment"],
};

function byDisplayOrder(services) {
  return services.toSorted((a, b) => a.displayOrder - b.displayOrder);
}

function findMatchingService(normalizedInput, services) {
  return byDisplayOrder(services).find((service) => {
    const name = service.name.toLowerCase();
    const slugAsWords = service.slug.replace(/-/g, " ").toLowerCase();
    const category = service.category.toLowerCase();
    if (
      normalizedInput.includes(name) ||
      normalizedInput.includes(slugAsWords) ||
      normalizedInput.includes(category)
    ) {
      return true;
    }
    const aliases = CATEGORY_ALIASES[category] ?? [];
    return aliases.some((alias) => new RegExp(`\\b${alias}\\b`).test(normalizedInput));
  });
}

const FIXTURES = [
  {
    id: 1,
    slug: "precision-cut",
    name: "Precision Cut",
    category: "Cuts",
    displayOrder: 1,
  },
  {
    id: 2,
    slug: "color-and-highlights",
    name: "Color and Highlights",
    category: "Color",
    displayOrder: 2,
  },
];

// Test 1: generic phrasing "hair cut" inside a real sentence matches Cuts.
assert.equal(
  findMatchingService(
    "i want get hair cut today. can i come to there?",
    FIXTURES
  )?.slug,
  "precision-cut"
);

// Test 2: "haircut" (no space) also matches Cuts.
assert.equal(
  findMatchingService("do you have haircut availability", FIXTURES)?.slug,
  "precision-cut"
);

// Test 3: exact name still matches (no regression).
assert.equal(
  findMatchingService("precision cut please", FIXTURES)?.slug,
  "precision-cut"
);

// Test 4: exact slug-as-words and a second fixture category still match
// their own service, unaffected by the Cuts alias.
assert.equal(
  findMatchingService("book color and highlights", FIXTURES)?.slug,
  "color-and-highlights"
);

// Test 5: "cute" must not false-positive match the "cut" alias (word boundary).
assert.equal(
  findMatchingService("that outfit is so cute", FIXTURES),
  undefined
);

// Test 6: no service, no alias -> undefined.
assert.equal(
  findMatchingService("what's the weather like", FIXTURES),
  undefined
);

console.log("chat.selfcheck: all assertions passed");
