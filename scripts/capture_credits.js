const puppeteer = require('puppeteer-core');
const path = require('path');
const fs = require('fs');

const CHROME_PATH = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';

async function testCredits() {
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

  // Click CREDITS directly from fresh load
  await page.mouse.click(rect.x + rect.w * 0.24, rect.y + rect.h * 0.55);
  await new Promise(r => setTimeout(r, 1500));

  const creditsShot = path.join(__dirname, '..', 'screenshots', '03_credits_english.png');
  fs.writeFileSync(creditsShot, await page.screenshot());
  console.log('[Test] Saved 03_credits_english.png');

  await browser.close();
}

testCredits().catch(err => {
  console.error('[Error]', err);
  process.exit(1);
});
