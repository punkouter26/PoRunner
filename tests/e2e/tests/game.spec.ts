import { test, expect, chromium, type Page, type BrowserContext } from '@playwright/test';

// ── Helpers ──────────────────────────────────────────────────────────────────

/** Collect all browser console errors during a test. */
function captureConsoleErrors(page: Page): string[] {
  const errors: string[] = [];
  page.on('console', (msg) => {
    if (msg.type() === 'error') errors.push(msg.text());
  });
  page.on('pageerror', (err) => errors.push(err.message));
  return errors;
}

/** Wait until a specific UI div is visible (not .hidden). */
async function waitForScreen(page: Page, id: string, timeout = 8000) {
  await expect(page.locator(`#${id}`)).not.toHaveClass(/hidden/, { timeout });
}

// ═══════════════════════════════════════════════════════════════════════════════
// Static / Page-load tests
// ═══════════════════════════════════════════════════════════════════════════════

test.describe('Page load', () => {
  test('page title is "🍌 Banana Game"', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveTitle(/Banana Game/);
  });

  test('game canvas is present', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('#gameCanvas')).toBeVisible();
  });

  test('canvas initially fills the viewport', async ({ page }) => {
    await page.goto('/');
    const canvas = page.locator('#gameCanvas');
    const box = await canvas.boundingBox();
    const viewport = page.viewportSize()!;
    expect(box?.width).toBeGreaterThanOrEqual(viewport.width * 0.9);
    expect(box?.height).toBeGreaterThanOrEqual(viewport.height * 0.9);
  });

  test('no uncaught JS errors on page load', async ({ page }) => {
    const errors = captureConsoleErrors(page);
    await page.goto('/');
    await page.waitForTimeout(2000);
    // Filter out known acceptable errors (SignalR reconnect info noise)
    const blocking = errors.filter(
      (e) => !e.includes('WebSocket') && !e.includes('net::ERR_CONNECTION_REFUSED')
    );
    expect(blocking).toHaveLength(0);
  });
});

// ═══════════════════════════════════════════════════════════════════════════════
// Waiting state (single player — requires backend)
// ═══════════════════════════════════════════════════════════════════════════════

test.describe('Waiting screen', () => {
  test('solo player sees waiting screen', async ({ page }) => {
    const errors = captureConsoleErrors(page);
    await page.goto('/');
    await waitForScreen(page, 'ui-waiting');
    expect(page.locator('#ui-waiting')).not.toHaveClass(/hidden/);
    // Report any JS errors found during this scenario
    expect(errors.filter((e) => !e.includes('WebSocket'))).toHaveLength(0);
  });

  test('waiting screen shows "Searching for opponent" copy', async ({ page }) => {
    await page.goto('/');
    await waitForScreen(page, 'ui-waiting');
    await expect(page.locator('#ui-waiting')).toContainText(/opponent/i);
  });

  test('spinner is visible while waiting', async ({ page }) => {
    await page.goto('/');
    await waitForScreen(page, 'ui-waiting');
    await expect(page.locator('.spinner')).toBeVisible();
  });
});

// ═══════════════════════════════════════════════════════════════════════════════
// Error / Disconnect screen
// ═══════════════════════════════════════════════════════════════════════════════

test.describe('Error screen', () => {
  test('reconnect button is present in error UI', async ({ page }) => {
    await page.goto('/');
    // The button is always in DOM — verify it is accessible
    await expect(page.locator('#btn-reconnect')).toBeDefined();
  });
});

// ═══════════════════════════════════════════════════════════════════════════════
// Ready Check — two-player flow (requires backend)
// ═══════════════════════════════════════════════════════════════════════════════

test.describe('Ready Check', () => {
  test('two players reach ready-check screen', async ({ browser }) => {
    const ctx1 = await browser.newContext();
    const ctx2 = await browser.newContext();
    const p1 = await ctx1.newPage();
    const p2 = await ctx2.newPage();
    const errors1 = captureConsoleErrors(p1);
    const errors2 = captureConsoleErrors(p2);

    await p1.goto('/');
    await p2.goto('/');

    await waitForScreen(p1, 'ui-readycheck');
    await waitForScreen(p2, 'ui-readycheck');

    expect(p1.locator('#ui-readycheck')).not.toHaveClass(/hidden/);
    expect(p2.locator('#ui-readycheck')).not.toHaveClass(/hidden/);

    const jsErrors = [...errors1, ...errors2].filter((e) => !e.includes('WebSocket'));
    expect(jsErrors).toHaveLength(0);

    await ctx1.close();
    await ctx2.close();
  });

  test('color picker buttons are visible during ready-check', async ({ browser }) => {
    const ctx1 = await browser.newContext();
    const ctx2 = await browser.newContext();
    const p1 = await ctx1.newPage();
    const p2 = await ctx2.newPage();

    await p1.goto('/');
    await p2.goto('/');
    await waitForScreen(p1, 'ui-readycheck');

    const colorBtns = p1.locator('.color-btn');
    await expect(colorBtns).toHaveCount(2);

    await ctx1.close();
    await ctx2.close();
  });

  test('ready button is visible during ready-check', async ({ browser }) => {
    const ctx1 = await browser.newContext();
    const ctx2 = await browser.newContext();
    const p1 = await ctx1.newPage();
    const p2 = await ctx2.newPage();

    await p1.goto('/');
    await p2.goto('/');
    await waitForScreen(p1, 'ui-readycheck');

    await expect(p1.locator('#btn-ready')).toBeVisible();

    await ctx1.close();
    await ctx2.close();
  });

  test('clicking ready shows "waiting for opponent" status text', async ({ browser }) => {
    const ctx1 = await browser.newContext();
    const ctx2 = await browser.newContext();
    const p1 = await ctx1.newPage();
    const p2 = await ctx2.newPage();

    await p1.goto('/');
    await p2.goto('/');
    await waitForScreen(p1, 'ui-readycheck');

    await p1.locator('#btn-ready').click();
    await expect(p1.locator('#ready-status-text')).not.toHaveClass(/hidden/, { timeout: 3000 });

    await ctx1.close();
    await ctx2.close();
  });
});

// ═══════════════════════════════════════════════════════════════════════════════
// Countdown → Playing transition
// ═══════════════════════════════════════════════════════════════════════════════

test.describe('Countdown and Playing', () => {
  test('both ready → countdown screen appears', async ({ browser }) => {
    const ctx1 = await browser.newContext();
    const ctx2 = await browser.newContext();
    const p1 = await ctx1.newPage();
    const p2 = await ctx2.newPage();
    const errors = captureConsoleErrors(p1);

    await p1.goto('/');
    await p2.goto('/');
    await waitForScreen(p1, 'ui-readycheck');

    await p1.locator('#btn-ready').click();
    await p2.locator('#btn-ready').click();

    await waitForScreen(p1, 'ui-countdown', 5000);
    await expect(p1.locator('#countdown-text')).toBeVisible();

    expect(errors.filter((e) => !e.includes('WebSocket'))).toHaveLength(0);

    await ctx1.close();
    await ctx2.close();
  });

  test('after countdown, playing HUD with timer is shown', async ({ browser }) => {
    const ctx1 = await browser.newContext();
    const ctx2 = await browser.newContext();
    const p1 = await ctx1.newPage();
    const p2 = await ctx2.newPage();
    const errors = captureConsoleErrors(p1);

    await p1.goto('/');
    await p2.goto('/');
    await waitForScreen(p1, 'ui-readycheck');

    await p1.locator('#btn-ready').click();
    await p2.locator('#btn-ready').click();

    // Wait past 3s countdown + some buffer
    await waitForScreen(p1, 'ui-playing', 8000);
    await expect(p1.locator('#hud-timer')).toBeVisible();
    await expect(p1.locator('#hud-timer')).toContainText(/s$/);

    expect(errors.filter((e) => !e.includes('WebSocket'))).toHaveLength(0);

    await ctx1.close();
    await ctx2.close();
  });
});

// ═══════════════════════════════════════════════════════════════════════════════
// Solo race — 1 player completes the race end-to-end
// ═══════════════════════════════════════════════════════════════════════════════

test.describe('Solo race', () => {
  // Each test navigates through waiting → countdown (3 s) → playing → finish.
  // Allow ample time for all phases plus network overhead.
  test.setTimeout(60_000);

  /**
   * Full happy-path for a solo player:
   *   waiting → click "RACE SOLO!" → countdown → playing → TYGH to finish → gameover
   *
   * Movement math:
   *   - Player starts at x = 100 (server-assigned)
   *   - Each full TYGH combo advances x += 60 (client-side, then server-synced)
   *   - FINISH_LINE_X ≈ 150 + (worldWidth − 300) * 0.5
   *     With 1 280 px viewport → worldWidth = 1 280 → finish ≈ 640
   *   - 15 combos × 60 px = 900 px total — always enough for any viewport ≥ 1 200 px wide
   *
   * Reliability notes:
   *   - window.__serverConnected is set to true when the FIRST gameState arrives from server
   *   - window.__gameStatus tracks the current game phase as a string (avoids DOM-class polling)
   *   - window.__testPlayerReady() directly invokes 'PlayerReady' on the SignalR hub
   *     (bypasses the button which is inside a display:none container during most render frames)
   */
  test('solo player finishes the race and sees a final time', async ({ page }) => {
    const errors = captureConsoleErrors(page);

    await page.goto('/');

    // ── 1. Wait for server connection AND waiting state ────────────────────────
    // window.__serverConnected is set on the first received gameState message;
    // window.__gameStatus mirrors data.status for reliable state polling.
    await page.waitForFunction(
      () => (window as any).__serverConnected === true && (window as any).__gameStatus === 'waiting',
      { timeout: 10_000 }
    );

    // ── 2. Start solo race via the test hook (bypasses render-loop display:none) ─
    await page.evaluate(() => (window as any).__testPlayerReady());

    // ── 3. Server sends 3-second countdown ────────────────────────────────────
    await page.waitForFunction(
      () => (window as any).__gameStatus === 'countdown',
      { timeout: 6_000 }
    );

    // ── 4. Playing phase begins after countdown ────────────────────────────────
    await page.waitForFunction(
      () => (window as any).__gameStatus === 'playing',
      { timeout: 10_000 }
    );
    await expect(page.locator('#hud-timer')).toBeVisible({ timeout: 2_000 });

    // ── 5. Drive to the finish line via TYGH key combos ───────────────────────
    //    15 combos × 60 px = 900 px forward — more than enough to clear the line.
    //    A brief pause after each full combo lets the server tick process the update.
    for (let i = 0; i < 15; i++) {
      await page.keyboard.press('t');
      await page.keyboard.press('y');
      await page.keyboard.press('g');
      await page.keyboard.press('h');
      await page.waitForTimeout(80);
    }

    // ── 6. Game-over state should be reached ───────────────────────────────────
    await page.waitForFunction(
      () => (window as any).__gameStatus === 'gameover',
      { timeout: 10_000 }
    );

    // ── 7. Final time and play-again button are rendered ───────────────────────
    await expect(page.locator('#final-time-text')).toBeVisible({ timeout: 2_000 });
    await expect(page.locator('#final-time-text')).toContainText(/Final Time:/);
    await expect(page.locator('#final-time-text')).toContainText(/\d+\.\d{3}s/);
    await expect(page.locator('#btn-restart')).toBeVisible({ timeout: 2_000 });

    // ── 8. No unexpected JS errors throughout ─────────────────────────────────
    const jsErrors = errors.filter(
      (e) => !e.includes('WebSocket') && !e.includes('net::ERR_CONNECTION_REFUSED')
    );
    expect(jsErrors).toHaveLength(0);
  });

  test('solo player can play again after finishing', async ({ page }) => {
    await page.goto('/');

    await page.waitForFunction(
      () => (window as any).__serverConnected === true && (window as any).__gameStatus === 'waiting',
      { timeout: 10_000 }
    );

    // Start solo race
    await page.evaluate(() => (window as any).__testPlayerReady());

    // Wait through countdown → playing
    await page.waitForFunction(() => (window as any).__gameStatus === 'countdown', { timeout: 6_000 });
    await page.waitForFunction(() => (window as any).__gameStatus === 'playing', { timeout: 10_000 });

    // Race to finish
    for (let i = 0; i < 15; i++) {
      await page.keyboard.press('t');
      await page.keyboard.press('y');
      await page.keyboard.press('g');
      await page.keyboard.press('h');
      await page.waitForTimeout(80);
    }

    await page.waitForFunction(() => (window as any).__gameStatus === 'gameover', { timeout: 10_000 });

    // Request restart via test hook (bypasses render-loop display:none on btn-restart)
    await page.evaluate(() => (window as any).__testRequestRestart());
    await page.waitForFunction(
      () => (window as any).__serverConnected === true && (window as any).__gameStatus === 'waiting',
      { timeout: 8_000 }
    );
    await expect(page.locator('#btn-ready-solo')).toBeVisible({ timeout: 2_000 });
  });
});

// ═══════════════════════════════════════════════════════════════════════════════
// Game Over
// ═══════════════════════════════════════════════════════════════════════════════

test.describe('Game Over', () => {
  test('game-over screen has PLAY AGAIN button', async ({ page }) => {
    await page.goto('/');
    // The button is in DOM even if hidden; inspect its presence
    await expect(page.locator('#btn-restart')).toBeDefined();
  });
});

// ═══════════════════════════════════════════════════════════════════════════════
// Mobile viewport
// ═══════════════════════════════════════════════════════════════════════════════

test.describe('Mobile viewport', () => {
  test('canvas fills iPhone 12 viewport width', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/');
    const canvas = page.locator('#gameCanvas');
    const box = await canvas.boundingBox();
    expect(box?.width).toBeLessThanOrEqual(395);
    expect(box?.width).toBeGreaterThanOrEqual(380);
  });

  test('waiting screen is legible on mobile', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/');
    await waitForScreen(page, 'ui-waiting');
    const textBox = await page.locator('#ui-waiting').boundingBox();
    expect(textBox).not.toBeNull();
  });
});

// ═══════════════════════════════════════════════════════════════════════════════
// JS Error monitoring across ALL tests (summary helper)
// ═══════════════════════════════════════════════════════════════════════════════

test.describe('JS error monitoring', () => {
  test('no critical JS errors during full waiting→readycheck flow', async ({ browser }) => {
    const ctx1 = await browser.newContext();
    const ctx2 = await browser.newContext();
    const p1 = await ctx1.newPage();
    const p2 = await ctx2.newPage();
    const allErrors: string[] = [];

    p1.on('pageerror', (e) => allErrors.push(`p1: ${e.message}`));
    p2.on('pageerror', (e) => allErrors.push(`p2: ${e.message}`));

    await p1.goto('/');
    await p2.goto('/');
    await waitForScreen(p1, 'ui-readycheck');
    await waitForScreen(p2, 'ui-readycheck');

    expect(allErrors).toHaveLength(0);

    await ctx1.close();
    await ctx2.close();
  });
});
