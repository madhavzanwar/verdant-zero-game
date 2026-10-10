const puppeteer = require('puppeteer-core');
const CHROME_PATH = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';

async function measureFps() {
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

  const port = process.env.PORT || 3000;
  await page.goto(`http://localhost:${port}`, { waitUntil: 'networkidle2', timeout: 60000 });
  await new Promise(r => setTimeout(r, 6000));

  // Click PLAY then Level 1
  const rect = await page.evaluate(() => {
    const c = document.querySelector('#unity-canvas');
    const r = c.getBoundingClientRect();
    return { x: r.x, y: r.y, w: r.width, h: r.height };
  });

  await page.mouse.click(rect.x + rect.w * 0.24, rect.y + rect.h * 0.38); // PLAY
  await new Promise(r => setTimeout(r, 1200));
  await page.mouse.click(rect.x + rect.w * 0.5, rect.y + rect.h * 0.36); // Level 1
  await new Promise(r => setTimeout(r, 6000)); // wait for level 1

  // Measure requestAnimationFrame delta times over 3 seconds
  const fpsData = await page.evaluate(async () => {
    return new Promise(resolve => {
      let frames = 0;
      let start = performance.now();
      const frameTimes = [];
      let lastTime = start;

      function count() {
        const now = performance.now();
        frameTimes.push(now - lastTime);
        lastTime = now;
        frames++;
        if (now - start < 3000) {
          requestAnimationFrame(count);
        } else {
          const totalDuration = (now - start) / 1000;
          const avgFps = frames / totalDuration;
          const avgFrameTime = frameTimes.reduce((a, b) => a + b, 0) / frameTimes.length;
          resolve({ frames, avgFps: Math.round(avgFps), avgFrameTime: avgFrameTime.toFixed(2) });
        }
      }
      requestAnimationFrame(count);
    });
  });

  console.log('[FPS Benchmark]', fpsData);
  await browser.close();
}

measureFps().catch(err => {
  console.error('[Benchmark Error]', err);
  process.exit(1);
});
