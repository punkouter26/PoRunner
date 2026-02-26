const { chromium } = require('playwright');
const path = require('path');

(async () => {
    // Make sure we have the correct output directory
    const artifactsDir = 'C:\\Users\\punko\\Downloads\\test1\\docs';

    const browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const page = await context.newPage();

    try {
        console.log('Navigating to local vite dev server...');
        await page.goto('http://localhost:5173/', { waitUntil: 'networkidle' });

        // Wait a bit for the scene to render if it's a game or threejs
        await page.waitForTimeout(2000);
        await page.screenshot({ path: path.join(artifactsDir, 'screenshot_1_home.png') });
        console.log('Saved screenshot 1');

        // Let's try to interact or wait more for different states
        // In the absence of a known UI, we'll try to find buttons and click them
        // For now let's space screenshots out over time to capture animations
        await page.waitForTimeout(2000);
        await page.screenshot({ path: path.join(artifactsDir, 'screenshot_2_state.png') });
        console.log('Saved screenshot 2');

        await page.waitForTimeout(2000);
        await page.screenshot({ path: path.join(artifactsDir, 'screenshot_3_state.png') });
        console.log('Saved screenshot 3');

        // Let's press "Enter" or similar globally if it's a start screen
        await page.keyboard.press('Enter');
        await page.waitForTimeout(2000);
        await page.screenshot({ path: path.join(artifactsDir, 'screenshot_4_after_enter.png') });
        console.log('Saved screenshot 4');

        // Let's click in the middle of the screen
        await page.mouse.click(720, 450);
        await page.waitForTimeout(2000);
        await page.screenshot({ path: path.join(artifactsDir, 'screenshot_5_after_click.png') });
        console.log('Saved screenshot 5');

    } catch (e) {
        console.error('Error during screenshot generation', e);
    } finally {
        await browser.close();
    }
})();
