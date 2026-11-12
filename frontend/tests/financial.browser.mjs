import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

export async function runFinancialBrowserChecks(h) {
  const { cdp, evaluate, waitFor, test, select, clickText, load, project, variants, dialogs, answerDialogs, ownedModels, cache } = h;
  const fields=['residentialConstructionCost','commercialConstructionCost','parkingCost','residentialSalePrice','commercialSalePrice','parkingSalePrice','landCost','additionalCosts','projectDuration'];
  const control=['100000','80000','500000','200000','150000','1000000','50000000','20000000','3'];
  const input=key=>`document.querySelector('[name="${key}"]')`;
  async function setField(key,value) {
    await evaluate(`(() => {const e=${input(key)};Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,${JSON.stringify(value)});e.dispatchEvent(new Event('input',{bubbles:true}));})()`);
  }
  const fill=async(values=control)=>{for(let i=0;i<fields.length;i++)await setField(fields[i],values[i]);};
  const contains=text=>`document.body.innerText.includes(${JSON.stringify(text)})`;
  let injection;
  async function scenario(mode) {
    answerDialogs(true);
    if(injection)await cdp('Page.removeScriptToEvaluateOnNewDocument',{identifier:injection});
    injection=(await cdp('Page.addScriptToEvaluateOnNewDocument',{source:`(${mockFinancial.toString()})(${JSON.stringify({mode,conceptA:variants[0].id,conceptB:variants[1].id})});`})).identifier;
    await load('real');
    await waitFor(`document.getElementById('concept-select')?.value === ${JSON.stringify(variants[0].id)}`);
    await clickText('Финансовые параметры');
    await waitFor(contains('Финансовые параметры'));
  }
  await test('financial screen without selected concept offers navigation',async()=>{
    await scenario('empty');await clickText('Исходные данные');await select('project-select','');await clickText('Финансовые параметры');
    await waitFor(contains('Сначала выберите проект и концепцию'));
    assert.equal(await evaluate('document.querySelectorAll("form input").length'),0);
    await clickText('К исходным данным');await waitFor(`document.querySelector('h1').textContent==='Исходные данные'`);
  });
  await test('empty models and a new blank nine-field form',async()=>{
    await scenario('empty');await waitFor(contains('У этой концепции ещё нет финансовых моделей'));
    await clickText('Создать модель');await waitFor('document.querySelectorAll("form input").length===9');
    assert(await evaluate(`Array.from(document.querySelectorAll('form input')).every(e=>e.value==='')`));
    await clickText('Сохранить');await waitFor('document.querySelectorAll("[aria-invalid=true]").length===9');
    assert.equal(await evaluate('window.__financialTest.posts'),0);
  });
  await test('client validation rejects negative/precision and accepts comma without duplicate POST',async()=>{
    await fill();await setField('landCost','-1');await clickText('Сохранить');await waitFor(`document.getElementById('error-landCost')`);
    await setField('landCost','1,001');await clickText('Сохранить');await waitFor(`document.getElementById('error-landCost')`);
    assert.equal(await evaluate('window.__financialTest.posts'),0);
    await setField('landCost','999999999999999999,99');
    await evaluate(`(() => {const f=document.querySelector('form');f.dispatchEvent(new Event('submit',{bubbles:true,cancelable:true}));f.dispatchEvent(new Event('submit',{bubbles:true,cancelable:true}));})()`);
    await waitFor(contains('Финансовая модель сохранена'));
    assert.equal(await evaluate('window.__financialTest.posts'),1);
    assert.equal(await evaluate(`${input('landCost')}.value`),'999999999999999999,99');
    assert(await evaluate(`window.__financialTest.lastPost.includes('"landCost":999999999999999999.99')`));
  });
  await test('saved models reopen, update and use stable IDs rather than ordinal numbers',async()=>{
    const first=await evaluate('document.getElementById("financial-model-select").value');
    await clickText('Создать модель');await fill(control.map((value,index)=>index<8?'0':value));await clickText('Сохранить');await waitFor(contains('Финансовая модель сохранена'));
    const second=await evaluate('document.getElementById("financial-model-select").value');assert.notEqual(first,second);
    await select('financial-model-select',first);await waitFor(`${input('landCost')}?.value==='999999999999999999,99'`);
    await setField('residentialSalePrice','210000');await clickText('Сохранить');await waitFor(contains('Финансовая модель сохранена'));
    assert.equal(await evaluate('window.__financialTest.puts'),1);
    await select('financial-model-select',second);await waitFor(`${input('landCost')}?.value==='0,00'`);
    await select('financial-model-select',first);await waitFor(`${input('residentialSalePrice')}?.value==='210000,00'`);
    assert(!await evaluate(`window.__financialTest.lastPut.includes('conceptVariantId') || window.__financialTest.lastPut.includes('profit')`));
  });
  await test('dirty model switching/new model/navigation require discard confirmation',async()=>{
    await setField('landCost','777');const before=dialogs.length;answerDialogs(false);
    await clickText('Создать модель');assert.equal(dialogs.length,before+1);assert.equal(await evaluate(`${input('landCost')}.value`),'777');
    await clickText('Результаты');assert.equal(await evaluate('document.querySelector("h1").textContent'),'Финансовые параметры');
    const current=await evaluate('document.getElementById("financial-model-select").value');
    const alternate=await evaluate(`Array.from(document.getElementById('financial-model-select').options).find(o=>o.value&&o.value!==${JSON.stringify(current)}).value`);
    await select('financial-model-select',alternate);assert.equal(await evaluate('document.getElementById("financial-model-select").value'),current);
    assert(await evaluate(`(() => {const event=new Event('beforeunload',{cancelable:true});window.dispatchEvent(event);return event.defaultPrevented;})()`));
    answerDialogs(true);await clickText('Результаты');await waitFor(`document.querySelector('h1').textContent==='Результаты'`);
    await clickText('Финансовые параметры');assert.equal(await evaluate(`${input('landCost')}.value`),'999999999999999999,99');
  });
  await test('delete cancellation sends no DELETE; confirmed deletion clears selected ID',async()=>{
    const before=await evaluate('window.__financialTest.deletes');answerDialogs(false);await clickText('Удалить модель');assert.equal(await evaluate('window.__financialTest.deletes'),before);
    answerDialogs(true);await clickText('Удалить модель');await waitFor(contains('Финансовая модель удалена'));
    assert.equal(await evaluate('document.getElementById("financial-model-select").value'),'');assert.equal(await evaluate('window.__financialTest.deletes'),before+1);
  });
  await test('server ValidationProblemDetails maps to the correct field and retains draft',async()=>{
    await scenario('validation');await waitFor(contains('У этой концепции ещё нет финансовых моделей'));await clickText('Создать модель');await fill();await clickText('Сохранить');
    await waitFor(`document.getElementById('error-landCost')?.textContent.includes('Серверная ошибка суммы')`);
    assert.equal(await evaluate(`${input('residentialSalePrice')}.value`),'200000');assert(!await evaluate(contains('Финансовая модель сохранена')));
  });
  await test('failed save/delete never show success or remove models optimistically',async()=>{
    await scenario('save-error');await waitFor(contains('У этой концепции ещё нет финансовых моделей'));await clickText('Создать модель');await fill();await clickText('Сохранить');await waitFor('document.querySelector("[role=alert]")');
    assert(!await evaluate(contains('Финансовая модель сохранена')));assert.equal(await evaluate(`${input('residentialSalePrice')}.value`),'200000');
    await scenario('delete-error');await waitFor(`${input('landCost')}?.value==='1000,00'`);const id=await evaluate('document.getElementById("financial-model-select").value');
    await clickText('Удалить модель');await waitFor('document.querySelector("[role=alert]")');assert.equal(await evaluate('document.getElementById("financial-model-select").value'),id);
  });
  await test('financial network/list errors offer retry and detail 404 does not expose a blank editing form',async()=>{
    await scenario('list-error');await waitFor(contains('Повторить загрузку моделей'));await evaluate('window.__financialTest.recovered=true');await clickText('Повторить загрузку моделей');await waitFor(contains('У этой концепции ещё нет финансовых моделей'));
    await scenario('detail-error');await waitFor(contains('Повторить загрузку модели'));assert.equal(await evaluate('document.querySelectorAll("form input").length'),0);
    await evaluate('window.__financialTest.recovered=true');await clickText('Повторить загрузку модели');await waitFor(`${input('landCost')}?.value==='1000,00'`);
  });
  await test('financial stale model and concept responses never overwrite current selection',async()=>{
    await scenario('race');await waitFor(`${input('landCost')}?.value==='1000,00'`);
    const ids=await evaluate('window.__financialTest.models.map(m=>m.id)');
    await select('financial-model-select',ids[1]);await waitFor(contains('Загрузка финансовой модели'));assert.equal(await evaluate('document.querySelectorAll("form input").length'),0);
    await select('financial-model-select',ids[0]);await waitFor(`${input('landCost')}?.value==='1000,00'`);await new Promise(r=>setTimeout(r,450));assert.equal(await evaluate('document.getElementById("financial-model-select").value'),ids[0]);
    await clickText('Исходные данные');await select('concept-select',variants[1].id);await clickText('Финансовые параметры');await waitFor(contains('У этой концепции ещё нет финансовых моделей'));
    assert.equal(await evaluate('document.querySelectorAll("form input").length'),0);assert.equal(await evaluate('document.getElementById("financial-model-select").value'),'');
  });
  await test('financial mobile form and unchanged results/comparison screens render correctly',async()=>{
    await scenario('empty');await waitFor(contains('У этой концепции ещё нет финансовых моделей'));await clickText('Создать модель');
    await cdp('Emulation.setDeviceMetricsOverride',{width:390,height:844,deviceScaleFactor:1,mobile:true});assert(await evaluate('document.documentElement.scrollWidth<=window.innerWidth'));
    for(const title of ['Результаты','Сравнение концепций','Исходные данные']){await clickText(title);await waitFor(`document.querySelector('h1').textContent===${JSON.stringify(title)}`);}
    await cdp('Emulation.setDeviceMetricsOverride',{width:1440,height:1100,deviceScaleFactor:1,mobile:false});
  });
  // Real CRUD: only server-assigned IDs observed from this browser's POST Location are cleaned up.
  answerDialogs(true);await cdp('Page.removeScriptToEvaluateOnNewDocument',{identifier:injection});injection=undefined;await load('real');
  await test('real PostgreSQL browser create/reload/update/reopen/delete preserves exact money',async()=>{
    await waitFor(`document.getElementById('concept-select')?.value===${JSON.stringify(variants[0].id)}`);await clickText('Финансовые параметры');await waitFor(contains('У этой концепции ещё нет финансовых моделей'));
    await clickText('Создать модель');await fill();await clickText('Сохранить');await waitFor(contains('Финансовая модель сохранена'));
    const id=await evaluate('document.getElementById("financial-model-select").value');assert(ownedModels.has(id),'Server Location must identify our temporary record');
    await cdp('Page.reload');await waitFor(`document.getElementById('concept-select')?.value===${JSON.stringify(variants[0].id)}`);await clickText('Финансовые параметры');await waitFor(`${input('residentialSalePrice')}?.value==='200000,00'`);
    await setField('residentialSalePrice','210000');await clickText('Сохранить');await waitFor(contains('Финансовая модель сохранена'));
    await select('financial-model-select','');await select('financial-model-select',id);await waitFor(`${input('residentialSalePrice')}?.value==='210000,00'`);
    await setField('landCost','999999999999999999,99');await clickText('Сохранить');await waitFor(contains('Финансовая модель сохранена'));
    await select('financial-model-select','');await select('financial-model-select',id);await waitFor(`${input('landCost')}?.value==='999999999999999999,99'`);
    await evaluate('window.scrollTo(0,0)');writeFileSync(resolve(cache,'financial-desktop.png'),Buffer.from((await cdp('Page.captureScreenshot',{format:'png'})).data,'base64'));
    answerDialogs(false);await clickText('Удалить модель');assert.equal(await evaluate('document.getElementById("financial-model-select").value'),id);
    answerDialogs(true);await clickText('Удалить модель');await waitFor(contains('Финансовая модель удалена'));assert.equal(await evaluate('document.getElementById("financial-model-select").value'),'');
    assert.equal((await fetch(`${h.url}/api/financial-models/${id}`)).status,404);
  });
}

function mockFinancial({mode,conceptA,conceptB}) {
  const original=window.fetch.bind(window);
  const money=['residentialConstructionCost','commercialConstructionCost','parkingCost','residentialSalePrice','commercialSalePrice','parkingSalePrice','landCost','additionalCosts'];
  const make=(id)=>({id,conceptVariantId:conceptA,...Object.fromEntries(money.map(key=>[key,'1000.00'])),projectDuration:3,totalCosts:null,totalRevenue:null,profit:null,roi:null,salesProfitability:null});
  window.__financialTest={mode,recovered:false,models:['race','detail-error','delete-error'].includes(mode)?[make('a18d9b8a-3d42-4b92-b7e0-095b3e1a0301'),make('a18d9b8a-3d42-4b92-b7e0-095b3e1a0302')]:[],posts:0,puts:0,deletes:0,lastPost:'',lastPut:''};
  const raw=model=>'{'+Object.entries(model).map(([key,value])=>JSON.stringify(key)+':'+(money.includes(key)?value:JSON.stringify(value))).join(',')+'}';
  const answer=(model,status=200)=>new Response(Array.isArray(model)?'['+model.map(raw).join(',')+']':raw(model),{status,headers:{'Content-Type':'application/json'}});
  window.fetch=async(input,options={})=>{
    const path=new URL(typeof input==='string'?input:input.url,location.href).pathname, method=options.method||'GET';
    const list=path.match(/^\/api\/concepts\/([^/]+)\/financial-models$/), detail=path.match(/^\/api\/financial-models\/([^/]+)$/);
    if(!list&&!detail&&path!=='/api/financial-models')return original(input,options);
    const state=window.__financialTest;
    if(mode==='list-error'&&list&&!state.recovered)throw new TypeError('private network error');
    if(mode==='detail-error'&&detail&&method==='GET'&&!state.recovered)return Response.json({status:404},{status:404});
    if(list){if(mode==='race'&&list[1]===conceptB)await new Promise(r=>setTimeout(r,200));return answer(state.models.filter(m=>m.conceptVariantId===list[1]));}
    if(method==='GET'){
      const model=state.models.find(m=>m.id===detail[1]);
      if(mode==='race'&&detail[1].endsWith('0302'))await new Promise(r=>setTimeout(r,300));
      return model?answer(model):Response.json({status:404},{status:404});
    }
    if(method==='DELETE'){
      state.deletes++;if(mode==='delete-error')return Response.json({detail:'PRIVATE_DELETE_ERROR'},{status:500});
      state.models=state.models.filter(m=>m.id!==detail[1]);return new Response(null,{status:204});
    }
    if(method==='POST')state.posts++;else state.puts++;
    if(mode==='validation')return Response.json({status:400,errors:{landCost:['Серверная ошибка суммы']}},{status:400});
    if(mode==='save-error')return Response.json({detail:'PRIVATE_SAVE_ERROR'},{status:500});
    const body=options.body;
    if(method==='POST')state.lastPost=body;else state.lastPut=body;
    const values=Object.fromEntries(money.map(key=>[key,body.match(new RegExp('"'+key+'"\\s*:\\s*([\\d.]+)'))[1]]));
    const duration=Number(body.match(/"projectDuration"\s*:\s*(\d+)/)[1]);
    const existing=detail&&state.models.find(m=>m.id===detail[1]);
    const id=existing?.id||'a18d9b8a-3d42-4b92-b7e0-'+String(300+state.posts).padStart(12,'0');
    const cid=method==='POST'?body.match(/"conceptVariantId"\s*:\s*"([^"]+)"/)[1]:existing.conceptVariantId;
    const model={...make(id),...values,projectDuration:duration,conceptVariantId:cid};
    await new Promise(r=>setTimeout(r,100));state.models=[...state.models.filter(m=>m.id!==id),model];return answer(model,method==='POST'?201:200);
  };
}
