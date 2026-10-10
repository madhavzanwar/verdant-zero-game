const puppeteer = require('puppeteer-core');
const path = require('path');
const fs = require('fs');

const CHROME_PATH = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';

async function testGameplay() {
  console.log('[Test] Launching Chrome to test Unity WebGL gameplay...');
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

  // Wait 6 seconds for Unity WebGL to fully load
  await new Promise(r => setTimeout(r, 6000));

  // Find canvas coordinates
  const canvas = await page.$('#unity-canvas');
  const box = await canvas.boundingBox();
  console.log('[Test] Canvas bounding box:', box);

  // Click on "JUGAR" button on the canvas
  // On 960x600 canvas centered in 1280x800:
  // JUGAR button is on the left side: ~24% from left of canvas, ~39% from top of canvas
  const clickX = box.x + box.width * 0.24;
  const clickY = box.y + box.height * 0.39;
  console.log(`[Test] Clicking JUGAR at (${clickX}, ${clickY})...`);
  await page.mouse.click(clickX, clickY);

  // Wait 4 seconds for scene transition / Level01 load
  await new Promise(r => setTimeout(r, 4000));

  const screenshotPath = path.join(__dirname, '..', 'screenshots', 'actual_unity_gameplay_live.png');
  const buffer = await page.screenshot({ fullPage: false });
  fs.writeFileSync(screenshotPath, buffer);
  console.log('[Test] Captured gameplay screenshot to:', screenshotPath);

  await browser.close();
}

testGameplay().catch(err => {
  console.error('[Test Failed]', err);
  process.exit(1);
});
