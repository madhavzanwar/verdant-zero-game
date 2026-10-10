const puppeteer = require('puppeteer-core');
const path = require('path');
const fs = require('fs');

const CHROME_PATH = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';

async function testFlight() {
  console.log('[Test] Launching Chrome to load Level 1 in Unity WebGL...');
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

  await page.goto('http://localhost:3001', { waitUntil: 'networkidle2', timeout: 60000 });
  await new Promise(r => setTimeout(r, 6000));

  const canvas = await page.$('#unity-canvas');
  const box = await canvas.boundingBox();

  // 1. Click JUGAR
  const jugarX = box.x + box.width * 0.24;
  const jugarY = box.y + box.height * 0.39;
  console.log(`[Test] Clicking JUGAR...`);
  await page.mouse.click(jugarX, jugarY);
  await new Promise(r => setTimeout(r, 1200));

  // 2. Click "1 · CENTRO DE VILLALIMPIA" (centered modal, top button)
  const level1X = box.x + box.width * 0.50;
  const level1Y = box.y + box.height * 0.37;
  console.log(`[Test] Clicking Level 1 button...`);
  await page.mouse.click(level1X, level1Y);

  // Wait 7 seconds for Level01 3D scene to load
  await new Promise(r => setTimeout(r, 7000));

  const screenshotPath = path.join(__dirname, '..', 'screenshots', 'actual_unity_level01_flight.png');
  const buffer = await page.screenshot({ fullPage: false });
  fs.writeFileSync(screenshotPath, buffer);
  console.log('[Test] Captured Level 1 gameplay screenshot to:', screenshotPath);

  await browser.close();
}

testFlight().catch(err => {
  console.error('[Test Failed]', err);
  process.exit(1);
});
