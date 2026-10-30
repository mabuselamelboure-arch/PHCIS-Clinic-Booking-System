import { chromium } from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));
const root=path.resolve(here,'../..');
const source=await fs.readFile(path.join(root,'docs/Design.md'),'utf8');
const diagrams=[...source.matchAll(/```mermaid\s*([\s\S]*?)```/g)].map(x=>x[1]);
const names=['architecture','erd','booking-sequence'];
if(diagrams.length!==names.length) throw new Error('Expected three documented diagrams.');
const output=path.join(root,'docs/evidence'); await fs.mkdir(output,{recursive:true});
const browser=await chromium.launch(); const page=await browser.newPage({viewport:{width:1600,height:1200}});
await page.setContent('<html><body style="margin:24px;background:white"></body></html>');
await page.addScriptTag({path:path.join(here,'node_modules/mermaid/dist/mermaid.min.js')});
await page.evaluate(()=>mermaid.initialize({startOnLoad:false,securityLevel:'strict',theme:'neutral',flowchart:{htmlLabels:false}}));
for(let i=0;i<diagrams.length;i++) {
  const svg=await page.evaluate(async({code,id})=>(await mermaid.render(id,code)).svg,{code:diagrams[i],id:`diagram${i}`});
  await fs.writeFile(path.join(output,`${names[i]}.svg`),svg);
  await page.evaluate(svg=>document.body.innerHTML=svg,svg);
  await page.locator('svg').screenshot({path:path.join(output,`${names[i]}.png`)});
  console.log(`Rendered ${names[i]}`);
}
await browser.close();
