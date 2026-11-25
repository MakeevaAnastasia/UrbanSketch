import assert from 'node:assert/strict';
import {writeFileSync} from 'node:fs';
import {resolve} from 'node:path';

export async function runResultsChecks(h) {
 const {cdp,evaluate,waitFor,test,select,clickText,load,variants,url,ownedModels,cache}=h;
 const contains=text=>`document.body.innerText.includes(${JSON.stringify(text)})`;
 const metric=(key,value)=>`document.querySelector('[data-result="${key}"]')?.textContent===${JSON.stringify(value)}`;
 let injection;
 async function scenario(mode){
  if(injection)await cdp('Page.removeScriptToEvaluateOnNewDocument',{identifier:injection});
  injection=(await cdp('Page.addScriptToEvaluateOnNewDocument',{source:`(${mockResults.toString()})(${JSON.stringify({mode,a:variants[0].id,b:variants[1].id})});`})).identifier;
  await load('real');await waitFor(`document.getElementById('concept-select')?.value===${JSON.stringify(variants[0].id)}`);
  await clickText('Финансовые параметры');
  await waitFor(mode==='empty'?contains('У этой концепции ещё нет финансовых моделей'):`document.querySelector('[name="landCost"]')?.value==='14000000,00'`);
  await clickText('Результаты расчёта');
 }
 await test('results without model offers edit action and no artificial figures',async()=>{await scenario('empty');await waitFor(contains('Выберите или сохраните финансовую модель'));assert.equal(await evaluate('document.querySelectorAll("[data-result]").length'),0);});
 await test('not_calculated GET does not POST; explicit double click calculates only once',async()=>{
  await scenario('default');await waitFor(contains('Расчёт ещё не выполнен'));assert.equal(await evaluate('window.__results.posts'),0);
  await evaluate(`(()=>{const b=Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Рассчитать');b.click();b.click();})()`);
  await waitFor(contains('Расчёт актуален'));assert.equal(await evaluate('window.__results.posts'),1);
  for(const [key,value] of [['totalCosts','150 000 000 ₽'],['totalRevenue','250 000 000 ₽'],['profit','100 000 000 ₽'],['roi','66,67%'],['salesProfitability','40,00%']])assert(await evaluate(metric(key,value)));
  assert(await evaluate(contains('Детализация затрат')));assert(await evaluate(contains('Детализация выручки')));assert(await evaluate(contains('16 000 000 ₽')));
 });
 await test('stale hides old numbers and offers recalculation',async()=>{await scenario('stale');await waitFor(contains('Необходимо повторить расчёт'));assert.equal(await evaluate('document.querySelectorAll("[data-result]").length'),0);await clickText('Пересчитать');await waitFor(contains('Расчёт актуален'));});
 for(const [mode,key,value] of [['loss','profit','-100 000 000 ₽'],['zero-cost','roi','Не рассчитывается'],['zero-revenue','salesProfitability','Не рассчитывается'],['large','profit','999 999 999 999 999 999,99 ₽']])
  await test('results display '+mode,async()=>{await scenario(mode);await waitFor(metric(key,value));});
 await test('incomplete shows known revenue, missing fields and no false saved/profit',async()=>{await scenario('incomplete');await waitFor(contains('Расчёт ещё не выполнен'));await clickText('Рассчитать');await waitFor(contains('Недостаточно данных'));assert(await evaluate(contains('Строительная жилая площадь')));assert(await evaluate(contains('Частичный результат не сохранён')));assert(await evaluate(metric('totalRevenue','250 000 000 ₽')));assert(await evaluate(metric('profit','Нет данных')));});
 for(const mode of ['400','404','409','422','500','network'])await test('results handles '+mode+' without stale values or server details',async()=>{
  await scenario(mode);await waitFor(mode==='404'||mode==='network'?`document.querySelector('[role="alert"]')`:contains('Расчёт ещё не выполнен'));
  if(mode!=='404'&&mode!=='network')await clickText('Рассчитать');
  await waitFor(`document.querySelector('[role="alert"]')`);assert.equal(await evaluate('document.querySelectorAll("[data-result]").length'),0);assert(!await evaluate(contains('PRIVATE_STACK')));
  if(mode==='409')assert(await evaluate(contains('Данные могли измениться')));
  if(mode==='422')assert(await evaluate(contains('Результат превышает допустимый диапазон')));
 });
 await test('late model response never overwrites new model and concept result',async()=>{
  await scenario('race');await clickText('К финансовым параметрам');await select('financial-model-select','a18d9b8a-3d42-4b92-b7e0-095b3e1a0402');await waitFor(`document.querySelector('[name="landCost"]')?.value==='14000000,00'`);await clickText('Результаты расчёта');await waitFor(contains('Расчёт ещё не выполнен'));await new Promise(r=>setTimeout(r,400));assert.equal(await evaluate('document.querySelectorAll("[data-result]").length'),0);
  await clickText('Исходные данные');await select('concept-select',variants[1].id);await clickText('Финансовые параметры');await waitFor(`document.getElementById('financial-model-select')?.value==='a18d9b8a-3d42-4b92-b7e0-095b3e1a0403'`);await clickText('Результаты расчёта');await waitFor(contains('Расчёт ещё не выполнен'));assert.equal(await evaluate('document.querySelectorAll("[data-result]").length'),0);
 });
 await cdp('Page.removeScriptToEvaluateOnNewDocument',{identifier:injection});injection=undefined;await load('real');
 await test('real PostgreSQL results lifecycle preserves original models and TEP at 390/768/1440',async()=>{
  const before=await Promise.all(variants.flatMap(v=>[`/api/concepts/${v.id}`,`/api/concepts/${v.id}/financial-models`]).map(async p=>await(await fetch(url+p)).text()));
  await waitFor(`document.getElementById('concept-select')?.value===${JSON.stringify(variants[0].id)}`);await clickText('Финансовые параметры');await waitFor(`Array.from(document.querySelectorAll('button')).some(b=>b.textContent==='Создать модель'&&!b.disabled)`);await clickText('Создать модель');
  const fields={constructionResidentialAreaOverride:'1000',constructionCommercialAreaOverride:'200',residentialConstructionCost:'100000',commercialConstructionCost:'80000',parkingCost:'500000',residentialSalePrice:'200000',commercialSalePrice:'150000',parkingSalePrice:'1000000',landCost:'14000000',additionalCosts:'10000000',projectDuration:'3'};
  const fill=async(key,value)=>evaluate(`(()=>{const e=document.querySelector('[name="${key}"]');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,${JSON.stringify(value)});e.dispatchEvent(new Event('input',{bubbles:true}));})()`);
  for(const [key,value] of Object.entries(fields))await fill(key,value);
  await clickText('Сохранить');await waitFor(`${contains('Финансовая модель сохранена')} && Array.from(document.querySelectorAll('button')).some(b=>b.textContent==='Сохранить'&&!b.disabled)`);const id=await evaluate('document.getElementById("financial-model-select").value');assert(ownedModels.has(id));
  await clickText('Результаты расчёта');await waitFor(contains('Расчёт ещё не выполнен'));await clickText('Рассчитать');await waitFor(contains('Расчёт актуален'));
  for(const [key,value] of [['totalCosts','150 000 000 ₽'],['totalRevenue','250 000 000 ₽'],['profit','100 000 000 ₽'],['roi','66,67%'],['salesProfitability','40,00%']])assert(await evaluate(metric(key,value)));
  for(const width of [390,768,1440]){
   await cdp('Emulation.setDeviceMetricsOverride',{width,height:1100,deviceScaleFactor:1,mobile:width===390});assert(await evaluate('document.documentElement.scrollWidth<=innerWidth'));
   writeFileSync(resolve(cache,`results-${width}.png`),Buffer.from((await cdp('Page.captureScreenshot',{format:'png'})).data,'base64'));
  }
  await cdp('Page.reload');await waitFor(`document.getElementById('concept-select')?.value===${JSON.stringify(variants[0].id)}`);await clickText('Финансовые параметры');await waitFor(`!document.getElementById('financial-model-select').disabled`);await select('financial-model-select',id);await waitFor(`document.querySelector('[name="constructionResidentialAreaOverride"]')?.value==='1000,00'`);await clickText('Результаты расчёта');await waitFor(contains('Расчёт актуален'));
  await clickText('К финансовым параметрам');await fill('residentialSalePrice','210000');await clickText('Сохранить');await waitFor(`${contains('Финансовая модель сохранена')} && Array.from(document.querySelectorAll('button')).some(b=>b.textContent==='Сохранить'&&!b.disabled)`);await clickText('Результаты расчёта');await waitFor(contains('Необходимо повторить расчёт'));assert.equal(await evaluate('document.querySelectorAll("[data-result]").length'),0);await clickText('Пересчитать');await waitFor(metric('totalRevenue','260 000 000 ₽'));assert(await evaluate(metric('profit','110 000 000 ₽')));
  await clickText('К финансовым параметрам');await clickText('Удалить модель');await waitFor(contains('Финансовая модель удалена'));assert.equal((await fetch(`${url}/api/financial-models/${id}`)).status,404);
  const after=await Promise.all(variants.flatMap(v=>[`/api/concepts/${v.id}`,`/api/concepts/${v.id}/financial-models`]).map(async p=>await(await fetch(url+p)).text()));assert.deepEqual(after,before);
 });
}

function mockResults({mode,a,b}){
 const original=window.fetch.bind(window);const id='a18d9b8a-3d42-4b92-b7e0-095b3e1a0401';
 const make=(key,cid)=>({id:key,conceptVariantId:cid,residentialConstructionCost:100000,commercialConstructionCost:80000,parkingCost:500000,residentialSalePrice:200000,commercialSalePrice:150000,parkingSalePrice:1000000,landCost:14000000,additionalCosts:10000000,projectDuration:3,constructionResidentialAreaOverride:1000,constructionCommercialAreaOverride:200,totalCosts:null,totalRevenue:null,profit:null,roi:null,salesProfitability:null});
 const models=mode==='empty'?[]:[make(id,a),make(id.replace('0401','0402'),a),make(id.replace('0401','0403'),b)];
 const result={status:'complete',costs:{residential:100000000,commercial:16000000,parking:10000000,land:14000000,additional:10000000},revenue:{residential:200000000,commercial:30000000,parking:20000000},totalCosts:150000000,totalRevenue:250000000,profit:100000000,roi:66.6667,salesProfitability:40,missingInputs:[],errors:[],persistenceIssues:[],canPersistToCurrentSchema:true};
 window.__results={posts:0};
 window.fetch=async(input,options={})=>{
  const path=new URL(typeof input==='string'?input:input.url,location.href).pathname;
  const list=path.match(/^\/api\/concepts\/([^/]+)\/financial-models$/);if(list)return Response.json(models.filter(m=>m.conceptVariantId===list[1]));
  const match=path.match(/^\/api\/financial-models\/([^/]+)(?:\/(calculation|calculate))?$/);if(!match)return original(input,options);
  if(!match[2])return Response.json(models.find(m=>m.id===match[1]));
  const post=match[2]==='calculate';if(post){window.__results.posts++;await new Promise(r=>setTimeout(r,100));}
  if(mode==='network')throw new TypeError('PRIVATE_STACK');
  if(mode==='404'||(post&&['400','409','500'].includes(mode)))return Response.json({detail:'PRIVATE_STACK'},{status:Number(mode)});
  if(post&&mode==='422')return Response.json({state:'error',isCurrent:false,result:{...result,canPersistToCurrentSchema:false,persistenceIssues:[{field:'profit',code:'storageRange',message:'PRIVATE_STACK'}]}},{status:422});
  if(post&&mode==='incomplete')return Response.json({state:'incomplete',isCurrent:false,result:{...result,status:'incomplete',costs:{...result.costs,residential:null},totalCosts:null,profit:null,roi:null,salesProfitability:null,missingInputs:['constructionResidentialArea'],canPersistToCurrentSchema:false}});
  if(mode==='race'&&match[1]===id)await new Promise(r=>setTimeout(r,300));
  const current=post||['loss','zero-cost','zero-revenue','large','race'].includes(mode)&&match[1]===id;
  if(current){const r={...result};if(mode==='loss'){r.profit=-100000000;r.roi=-40;r.salesProfitability=-40;}if(mode==='zero-cost'){r.totalCosts=0;r.roi=null;}if(mode==='zero-revenue'){r.totalRevenue=0;r.salesProfitability=null;}const raw=JSON.stringify({state:'current',isCurrent:true,result:r});return new Response(mode==='large'?raw.replace('"profit":100000000','"profit":999999999999999999.99'):raw,{headers:{'content-type':'application/json'}});}
  return Response.json({state:mode==='stale'?'stale':'not_calculated',isCurrent:false,result:null});
 };
}
