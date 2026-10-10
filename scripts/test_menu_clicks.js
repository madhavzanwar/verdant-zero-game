const puppeteer = require('puppeteer-core');
const path = require('path');
const fs = require('fs');

const CHROME_PATH = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';

async function testScreenshots() {
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

  // Save main menu
  const menuShot = path.join(__dirname, '..', 'screenshots', '01_main_menu_english.png');
  fs.writeFileSync(menuShot, await page.screenshot());
  console.log('[Test] Saved 01_main_menu_english.png');

  // Let's click on canvas where OPTIONS button is
  // Canvas is 960x600 centered at viewport 1280x800
  // Canvas left: (1280 - 960)/2 = 160. top: (800 - 600)/2 = 100 (or container position)
  const rect = await page.evaluate(() => {
    const c = document.querySelector('#unity-canvas');
    const r = c.getBoundingClientRect();
    return { x: r.x, y: r.y, w: r.width, h: r.height };
  });
  console.log('[Test] Canvas rect:', rect);

  // Click OPTIONS: roughly x = rect.x + rect.w * 0.24, y = rect.y + rect.h * 0.47
  await page.mouse.click(rect.x + rect.w * 0.24, rect.y + rect.h * 0.47);
  await new Promise(r => setTimeout(r, 1500));
  const optionsShot = path.join(__dirname, '..', 'screenshots', '02_options_english.png');
  fs.writeFileSync(optionsShot, await page.screenshot());
  console.log('[Test] Saved 02_options_english.png');

  // Press Escape to go back
  await page.keyboard.press('Escape');
  await new Promise(r => setTimeout(r, 1000));

  // Click CREDITS: roughly x = rect.x + rect.w * 0.24, y = rect.y + rect.h * 0.55
  await page.mouse.click(rect.x + rect.w * 0.24, rect.y + rect.h * 0.55);
  await new Promise(r => setTimeout(r, 1500));
  const creditsShot = path.join(__dirname, '..', 'screenshots', '03_credits_english.png');
  fs.writeFileSync(creditsShot, await page.screenshot());
  console.log('[Test] Saved 03_credits_english.png');

  // Press Escape to go back
  await page.keyboard.press('Escape');
  await new Promise(r => setTimeout(r, 1000));

  // Click PLAY: roughly x = rect.x + rect.w * 0.24, y = rect.y + rect.h * 0.38
  await page.mouse.click(rect.x + rect.w * 0.24, rect.y + rect.h * 0.38);
  await new Promise(r => setTimeout(r, 1500));
  const levelsShot = path.join(__dirname, '..', 'screenshots', '04_levels_english.png');
  fs.writeFileSync(levelsShot, await page.screenshot());
  console.log('[Test] Saved 04_levels_english.png');

  await browser.close();
  console.log('[Test] All UI screenshots captured successfully!');
}

testScreenshots().catch(err => {
  console.error('[Error]', err);
  process.exit(1);
});
