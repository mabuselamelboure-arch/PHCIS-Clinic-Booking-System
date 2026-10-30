const { test, expect } = require('@playwright/test');
const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');
const evidence = path.resolve(__dirname,process.env.PHCIS_CAPTURE_EVIDENCE ? '../../docs/evidence' : '../../work/evidence');
fs.mkdirSync(evidence,{recursive:true});
function visitDay(days=1) {
  const day = new Date(Date.now()+2*3600000); day.setUTCDate(day.getUTCDate()+days);
  while ([0,6].includes(day.getUTCDay())) day.setUTCDate(day.getUTCDate()+1);
  return day.toISOString().slice(0,10);
}
async function signIn(page) {
  await page.goto('/');
  await page.getByRole('button',{name:'Create an account',exact:true}).click();
  const email = `browser-${crypto.randomUUID()}@example.test`;
  await page.getByLabel('Account email',{exact:true}).fill(email);
  await page.getByLabel('Password',{exact:true}).fill(`Aa!1${crypto.randomUUID()}`);
  await page.getByRole('button',{name:'Create account',exact:true}).click();
  await expect(page.getByRole('heading',{name:'Where and when?'})).toBeVisible();
}
async function selectVisit(page) {
  await page.getByLabel('Appointment date',{exact:true}).fill(visitDay());
  await expect(page.locator('.slot').first()).toBeVisible();
  await page.locator('.slot').first().click();
  return await page.locator('.slot.selected').innerText();
}
async function details(page) {
  await page.getByLabel('Full name',{exact:true}).fill('Alex Example');
  await page.getByLabel('Contact number',{exact:true}).fill('0820000000');
}

test('booking validation, one submission, confirmation focus and refreshed availability', async({page})=>{
  await signIn(page); const time = await selectVisit(page);
  await page.getByRole('button',{name:'Confirm appointment',exact:true}).click();
  await expect(page.locator('div.validation-message').filter({hasText:"Full Name"})).toBeVisible();
  await page.getByLabel('Full name',{exact:true}).fill('Alex Example');
  await page.getByLabel('Contact number',{exact:true}).fill('()---()()');
  await page.getByRole('button',{name:'Confirm appointment',exact:true}).click();
  await expect(page.getByText('Enter a contact number containing 7-15 digits.',{exact:true}).last()).toBeVisible();
  await details(page);
  let submissions=0;
  await page.route('**/api/appointments',async route=>{
    if(route.request().method()==='POST') { submissions++; await new Promise(r=>setTimeout(r,150)); }
    await route.continue();
  });
  await page.getByRole('button',{name:'Confirm appointment',exact:true}).dblclick();
  await expect(page.getByRole('heading',{name:'Appointment confirmed'})).toBeVisible();
  expect(submissions).toBe(1);
  await expect(page.locator('section.confirmation')).toBeFocused();
  await expect(page.locator('.reference')).toContainText('PHC-');
  await page.screenshot({path:path.join(evidence,'booking-desktop.png'),fullPage:true});
  await page.getByRole('button',{name:'Book another appointment'}).click();
  await expect(page.getByRole('button',{name:time,exact:true})).toHaveCount(0);
});

test('two browser tabs compete safely for the same slot',async({browser})=>{
  const context = await browser.newContext({baseURL:process.env.PHCIS_BROWSER_URL || 'http://localhost:5085'});
  const first=await context.newPage(), second=await context.newPage();
  await signIn(first); await signIn(second);
  const time = await selectVisit(first);
  await second.getByLabel('Appointment date',{exact:true}).fill(visitDay());
  await second.getByRole('button',{name:time,exact:true}).click();
  await details(first); await details(second);
  const responses=[first.waitForResponse(r=>r.url().endsWith('/api/appointments') && r.request().method()==='POST'),second.waitForResponse(r=>r.url().endsWith('/api/appointments') && r.request().method()==='POST')];
  await Promise.all([first.getByRole('button',{name:'Confirm appointment',exact:true}).click(),second.getByRole('button',{name:'Confirm appointment',exact:true}).click()]);
  expect((await Promise.all(responses)).map(r=>r.status()).sort()).toEqual([201,409]);
  await context.close();
});

test('rapid clinic and date changes ignore stale availability responses',async({page})=>{
  await signIn(page);
  const oldClinic=await page.getByLabel('Clinic',{exact:true}).inputValue();
  const options=await page.getByLabel('Clinic',{exact:true}).locator('option').evaluateAll(nodes=>nodes.map(n=>n.value));
  let release; const held=new Promise(r=>release=r); let intercepted;
  const caught=new Promise(r=>intercepted=r);
  await page.route('**/slots?*',async route=>{
    if(route.request().url().includes(oldClinic)) {
      intercepted(); await held;
      await route.fulfill({json:[{id:crypto.randomUUID(),clinicId:oldClinic,startUtc:'2030-01-07T14:17:00Z',endUtc:'2030-01-07T14:47:00Z'}]});
    } else await route.continue();
  });
  await page.getByLabel('Appointment date',{exact:true}).fill(visitDay(2));
  await caught;
  await page.getByLabel('Clinic',{exact:true}).selectOption(options.find(x=>x!==oldClinic));
  await expect(page.locator('.slot').first()).toBeVisible();
  release();
  await expect(page.getByRole('button',{name:'16:17',exact:true})).toHaveCount(0);
  await expect(page.getByLabel('Clinic',{exact:true})).not.toHaveValue(oldClinic);
});

test('API unavailability is recoverable',async({page})=>{
  await page.route('**/api/clinics',route=>route.abort());
  await signInWithoutBookingWait(page);
  await expect(page.getByText('Unable to reach the booking service. Please try again.',{exact:true})).toBeVisible();
  await page.unroute('**/api/clinics');
  await page.getByRole('button',{name:'Retry',exact:true}).click();
  await expect(page.getByRole('heading',{name:'Where and when?'})).toBeVisible();
});
async function signInWithoutBookingWait(page) {
  await page.goto('/'); await page.getByRole('button',{name:'Create an account',exact:true}).click();
  await page.getByLabel('Account email',{exact:true}).fill(`browser-${crypto.randomUUID()}@example.test`);
  await page.getByLabel('Password',{exact:true}).fill(`Aa!1${crypto.randomUUID()}`);
  await page.getByRole('button',{name:'Create account',exact:true}).click();
}

test('mobile layout and keyboard field navigation work',async({page})=>{
  await page.setViewportSize({width:390,height:844});
  await signIn(page); await selectVisit(page);
  const name=page.getByLabel('Full name',{exact:true}); await name.focus(); await page.keyboard.press('Tab');
  await expect(page.getByLabel('Email address',{exact:true})).toBeFocused();
  expect(await page.evaluate(()=>document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({path:path.join(evidence,'booking-mobile.png'),fullPage:true});
});

test('Blazor executes with no console errors and component assets load',async({page})=>{
  const errors=[]; const assets=[];
  page.on('pageerror',error=>errors.push(error.message));
  page.on('console',message=>{if(message.type()==='error') errors.push(message.text());});
  page.on('response',response=>{if(response.url().includes('/_content/MudBlazor/')) assets.push(response.status());});
  await signIn(page);
  expect(assets.length).toBeGreaterThanOrEqual(2); expect(assets.every(x=>x===200)).toBe(true); expect(errors).toEqual([]);
});

test('expired authentication returns to sign-in instead of losing a booking silently',async({page})=>{
  await signIn(page); await selectVisit(page); await details(page);
  await page.route('**/api/appointments',route=>route.fulfill({status:401}));
  await page.getByRole('button',{name:'Confirm appointment',exact:true}).click();
  await expect(page.getByRole('heading',{name:'Sign in to book',exact:true})).toBeVisible();
});

test('dates without slots have an explicit empty state',async({page})=>{
  await signIn(page);
  const day=new Date(Date.now()+2*3600000); do {day.setUTCDate(day.getUTCDate()+1);} while(day.getUTCDay()!==6);
  await page.getByLabel('Appointment date',{exact:true}).fill(day.toISOString().slice(0,10));
  await expect(page.getByText('No appointments available on this date.',{exact:false})).toBeVisible();
  await expect(page.getByRole('button',{name:'Confirm appointment',exact:true})).toBeDisabled();
});


test('account validation corrections preserve a single registration and login click',async({page})=>{
  const email=`account-${crypto.randomUUID()}@example.test`;
  const password=`Aa!1${crypto.randomBytes(30).toString('base64url')}`;
  let registrations=0,logins=0;
  page.on('request',r=>{if(r.method()==='POST'&&r.url().endsWith('/api/auth/register'))registrations++;
    if(r.method()==='POST'&&r.url().endsWith('/api/auth/login'))logins++;});
  await page.goto('/');
  await page.getByRole('button',{name:'Create an account',exact:true}).click();
  await page.getByRole('button',{name:'Create account',exact:true}).click();
  await expect(page.getByText('Enter your account email.',{exact:true})).toBeVisible();
  await expect(page.getByText('Enter a password.',{exact:true})).toBeVisible();
  await page.getByLabel('Account email',{exact:true}).fill(email);
  await page.getByLabel('Password',{exact:true}).fill(password);
  await page.getByRole('button',{name:'Create account',exact:true}).click();
  await expect(page.getByRole('heading',{name:'Where and when?',exact:true})).toBeVisible();
  expect(registrations).toBe(1);
  await page.getByRole('button',{name:'Sign out',exact:true}).click();
  await page.getByRole('button',{name:'Sign in',exact:true}).click();
  await expect(page.getByText('Enter a password.',{exact:true})).toBeVisible();
  await page.getByLabel('Account email',{exact:true}).fill(email);
  await page.getByLabel('Password',{exact:true}).fill(password);
  await page.getByRole('button',{name:'Sign in',exact:true}).click();
  await expect(page.getByRole('heading',{name:'Where and when?',exact:true})).toBeVisible();
  expect(logins).toBe(1);
});
