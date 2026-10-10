const puppeteer = require('puppeteer-core');
const path = require('path');
const fs = require('fs');

const CHROME_PATH = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const URL = 'http://localhost:3001';
const SCREENSHOTS_DIR = path.join(__dirname, '..', 'screenshots');

if (!fs.existsSync(SCREENSHOTS_DIR)) {
  fs.mkdirSync(SCREENSHOTS_DIR, { recursive: true });
}

function delay(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

async function capture() {
  console.log('[Screenshots] Launching Chrome via Puppeteer-core...');
  const browser = await puppeteer.launch({
    executablePath: CHROME_PATH,
    headless: 'new',
    args: [
      '--no-sandbox',
      '--disable-setuid-sandbox',
      '--disable-gpu',
      '--window-size=1920,1080',
      '--autoplay-policy=no-user-gesture-required'
    ],
    defaultViewport: {
      width: 1920,
      height: 1080,
      deviceScaleFactor: 1
    }
  });

  const page = await browser.newPage();
  console.log(`[Screenshots] Navigating to ${URL}...`);
  await page.goto(URL, { waitUntil: 'networkidle0', timeout: 30000 });
  await delay(1800);

  // 1. Main Menu
  console.log('[Screenshots] Capturing 01_main_menu.png...');
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '01_main_menu.png'), type: 'png' });

  // 2. Level Select Overlay
  console.log('[Screenshots] Capturing 02_level_select.png...');
  await page.evaluate(() => {
    if (window.game) window.game.openOverlay(window.game.dom.overlayLevels);
  });
  await delay(600);
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '02_level_select.png'), type: 'png' });

  // 3. Options Overlay
  console.log('[Screenshots] Capturing 03_options_modal.png...');
  await page.evaluate(() => {
    if (window.game) window.game.openOverlay(window.game.dom.overlayOptions);
  });
  await delay(600);
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '03_options_modal.png'), type: 'png' });

  // 4. Credits Overlay
  console.log('[Screenshots] Capturing 04_credits_modal.png...');
  await page.evaluate(() => {
    if (window.game) window.game.openOverlay(window.game.dom.overlayCredits);
  });
  await delay(600);
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '04_credits_modal.png'), type: 'png' });

  // 5. Tutorial Onboarding
  console.log('[Screenshots] Capturing 05_tutorial_onboarding.png...');
  await page.evaluate(() => {
    if (window.game) window.game.startTutorial();
  });
  await delay(600);
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '05_tutorial_onboarding.png'), type: 'png' });

  // 6. Gameplay (3rd Person View)
  console.log('[Screenshots] Starting mission & capturing 06_gameplay_3rd_person.png...');
  await page.evaluate(() => {
    if (window.game) {
      window.game.startGame();
      window.game.drone.position.set(0, 22, -22);
      window.game.droneAltitude = 22;
      window.game.droneYaw = 0; // facing forward down the central avenue
      window.game.smog = 0.58;
      window.game.score = 2400;
      window.game.treesPlanted = 4;
      window.game.cloudsPurified = 6;
      window.game.timeRemaining = 104;
      // Ensure camera smooth updates
      window.game.updateCamera();
    }
  });
  await delay(1200);
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '06_gameplay_3rd_person.png'), type: 'png' });

  // 7. Purifier Beam in Action
  console.log('[Screenshots] Capturing 07_purifier_beam_action.png...');
  await page.evaluate(() => {
    if (window.game) {
      // Spawn a target smog cloud directly in front of the drone
      const testCloud = window.game.smogClouds[0];
      if (testCloud) {
        testCloud.group.position.set(0, 22, 10);
        window.game.activeTargetCloud = testCloud;
      }
      window.game.mouse.beam = true;
      window.game.updateBeam(true);
      window.game.showVoiceToast("Purifier Beam engaged! Neutralizing smog cloud.", 4000);
    }
  });
  await delay(800);
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '07_purifier_beam_action.png'), type: 'png' });
  await page.evaluate(() => {
    if (window.game) window.game.mouse.beam = false;
  });

  // 8. Instant Seed Planting & Blooming Tree
  console.log('[Screenshots] Capturing 08_seed_planting_blooming.png...');
  await page.evaluate(() => {
    if (window.game) {
      window.game.drone.position.set(0, 16, -12);
      window.game.droneAltitude = 16;
      if (window.game.plantingSpots.length > 0) {
        window.game.triggerBloom(window.game.plantingSpots[0]);
      }
      window.game.showVoiceToast("Instant Seed planted! Tree is purifying the air.", 4000);
    }
  });
  await delay(900);
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '08_seed_planting_blooming.png'), type: 'png' });

  // 9. First-Person Cockpit (FPV View)
  console.log('[Screenshots] Capturing 09_gameplay_fpv_cockpit.png...');
  await page.evaluate(() => {
    if (window.game) {
      window.game.drone.position.set(0, 20, -10);
      window.game.droneAltitude = 20;
      window.game.toggleCameraMode();
      window.game.updateCamera();
    }
  });
  await delay(700);
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '09_gameplay_fpv_cockpit.png'), type: 'png' });


  // 10. Pause Menu
  console.log('[Screenshots] Capturing 10_pause_menu.png...');
  await page.evaluate(() => {
    if (window.game) window.game.togglePause();
  });
  await delay(600);
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '10_pause_menu.png'), type: 'png' });

  // 11. Clean Air Victory Screen
  console.log('[Screenshots] Capturing 11_clean_air_victory.png...');
  await page.evaluate(() => {
    if (window.game) {
      window.game.score = 21450;
      window.game.smogRemoved = 0.94;
      window.game.cloudsPurified = 24;
      window.game.treesPlanted = 16;
      window.game.timeRemaining = 48;
      window.game.endGame('VICTORY');
    }
  });
  await delay(800);
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '11_clean_air_victory.png'), type: 'png' });

  console.log('[Screenshots] All 11 screenshots successfully captured!');
  await browser.close();
}

capture().catch(err => {
  console.error('[Screenshots] Capture failed:', err);
  process.exit(1);
});
