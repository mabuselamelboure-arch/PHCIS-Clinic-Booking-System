const { defineConfig } = require('@playwright/test');
const path = require('node:path');
const fs = require('node:fs');
const root = path.resolve(__dirname, '../..');
fs.mkdirSync(path.join(root, 'work'), {recursive:true});
const external = process.env.PHCIS_BROWSER_URL;
module.exports = defineConfig({
  testDir: '.', testMatch: 'booking.spec.cjs', workers: 1, retries: 0, timeout: 45000,
  expect: { timeout: 15000 }, reporter: [['list'], ['json', { outputFile: '../../work/browser-results.json' }]],
  use: { baseURL: external || 'http://localhost:5085', browserName: 'chromium', viewport: {width:1280,height:1000},
    trace: 'off', screenshot: 'only-on-failure' },
  webServer: external ? undefined : {
    command: 'dotnet run --project src/PHCIS.Api --no-launch-profile --no-build -c Release -- --demo-local',
    cwd: root, url: 'http://localhost:5085/health', reuseExistingServer: false, timeout: 60000,
    env: { ASPNETCORE_ENVIRONMENT:'Development', ASPNETCORE_URLS:'http://localhost:5085',
      DemoDatabasePath: path.join(root,'work',`browser-${process.pid}.db`) }
  }
});
