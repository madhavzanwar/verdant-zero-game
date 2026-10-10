const puppeteer = require('puppeteer-core');
const path = require('path');
const fs = require('fs');

const CHROME_PATH = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';

async function testGameplay() {
  const browser = await puppeteer.launch({
    executablePath: CHROME_PATH,
    headless: 'new',
    args: [
      '--no-sandbox',
      '--disable-setuid-sandbox',
      '--disable-web-security',
      '--use-gl=angle',
      '--use-angle=default',
      '--enable-webgl',
      '--window-size=1280,800'
    ]
  });

  const page = await browser.newPage();
  await page.setViewport({ width: 1280, height: 800 });

  console.log('[Test] Navigating to http://localhost:3001 ...');
  await page.goto('http://localhost:3001', { waitUntil: 'networkidle2', timeout: 60000 });
  await new Promise(r => setTimeout(r, 8000));

  const rect = await page.evaluate(() => {
    const c = document.querySelector('#unity-canvas');
    const r = c.getBoundingClientRect();
    return { x: r.x, y: r.y, w: r.width, h: r.height };
  });

  // Click PLAY
  console.log('[Test] Clicking PLAY...');
  await page.mouse.click(rect.x + rect.w * 0.24, rect.y + rect.h * 0.38);
  await new Promise(r => setTimeout(r, 1500));

  // Click 1 · DOWNTOWN DISTRICT (roughly center-ish, y = 0.36)
  console.log('[Test] Clicking 1 · DOWNTOWN DISTRICT...');
  await page.mouse.click(rect.x + rect.w * 0.5, rect.y + rect.h * 0.36);

  // Wait 10 seconds for Level 1 scene to load and render
  console.log('[Test] Waiting for Level 1 scene to load...');
  await new Promise(r => setTimeout(r, 12000));

  const gameplayShot = path.join(__dirname, '..', 'screenshots', '05_gameplay_hud_english.png');
  fs.writeFileSync(gameplayShot, await page.screenshot());
  console.log('[Test] Saved 05_gameplay_hud_english.png');

  await browser.close();
}

testGameplay().catch(err => {
  console.error('[Error]', err);
  process.exit(1);
});
