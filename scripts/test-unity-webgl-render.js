const puppeteer = require('puppeteer-core');
const path = require('path');
const fs = require('fs');

const CHROME_PATH = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';

async function testRender() {
  console.log('[Test] Launching Chrome to test Unity WebGL execution...');
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

  page.on('console', msg => console.log('[Browser Console]', msg.type(), msg.text()));
  page.on('pageerror', err => console.error('[Browser Error]', err.message));

  console.log('[Test] Navigating to http://localhost:3001 ...');
  await page.goto('http://localhost:3001', { waitUntil: 'networkidle2', timeout: 60000 });

  console.log('[Test] Page loaded. Waiting for Unity WebGL canvas / game to load...');
  // Wait up to 15 seconds for canvas
  await new Promise(r => setTimeout(r, 8000));

  const screenshotPath = path.join(__dirname, '..', 'screenshots', 'actual_unity_webgl_live.png');
  const buffer = await page.screenshot({ fullPage: false });
  fs.writeFileSync(screenshotPath, buffer);
  console.log('[Test] Captured screenshot to:', screenshotPath);

  await browser.close();
}

testRender().catch(err => {
  console.error('[Test Failed]', err);
  process.exit(1);
});
