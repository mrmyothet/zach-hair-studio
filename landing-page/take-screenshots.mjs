import { chromium } from 'playwright';
import { mkdir } from 'fs/promises';

const SCREENSHOTS_DIR = './screenshots';
const URL = 'http://localhost:3000';
const WIDTH = 1280;
const HEIGHT = 800;

async function takeScreenshots() {
  await mkdir(SCREENSHOTS_DIR, { recursive: true });

  const browser = await chromium.launch({
    executablePath: '/home/myothet/.cache/ms-playwright/chromium-1228/chrome-linux64/chrome',
    args: ['--no-sandbox']
  });

  const context = await browser.newContext({
    viewport: { width: WIDTH, height: HEIGHT }
  });

  const page = await context.newPage();

  for (let i = 1; i <= 5; i++) {
    console.log(`Taking screenshot ${i}...`);
    await page.goto(URL, { waitUntil: 'networkidle' });

    // Scroll to different positions for variety
    const scrollPositions = [0, 500, 1000, 1500, 2000];
    await page.evaluate((pos) => window.scrollTo(0, pos), scrollPositions[i - 1]);

    // Wait for any animations/lazy loading
    await page.waitForTimeout(1000);

    const filename = `${SCREENSHOTS_DIR}/screenshot-${i}.png`;
    await page.screenshot({ path: filename, fullPage: false });
    console.log(`✓ Saved: ${filename}`);
  }

  await browser.close();
  console.log('All screenshots taken successfully!');
}

takeScreenshots().catch(console.error);
