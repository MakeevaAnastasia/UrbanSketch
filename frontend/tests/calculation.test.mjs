import {test,afterEach} from 'node:test';
import assert from 'node:assert/strict';
import {formatResultMoney,formatResultPercent} from '../src/financial/formatResult.ts';
import {getCalculation,calculateModel} from '../src/api/calculation.ts';
const original=globalThis.fetch;afterEach(()=>{globalThis.fetch=original;});
const id='a18d9b8a-3d42-4b92-b7e0-095b3e1a0301';
export const resultFixture={status:'complete',costs:{residential:100000000,commercial:16000000,parking:10000000,land:14000000,additional:10000000},revenue:{residential:200000000,commercial:30000000,parking:20000000},totalCosts:150000000,totalRevenue:250000000,profit:100000000,roi:66.6667,salesProfitability:40,missingInputs:[],errors:[],persistenceIssues:[],canPersistToCurrentSchema:true};
for(const [value,expected] of [['150000000','150 000 000 ₽'],['999999999999999999.99','999 999 999 999 999 999,99 ₽'],['-100000000','-100 000 000 ₽'],['0','0 ₽'],[null,'Нет данных'],['0.0001','0 ₽']])
 test('money formatting '+value,()=>assert.equal(formatResultMoney(value),expected));
for(const [value,expected] of [['66.6667','66,67%'],['40','40,00%'],[null,'Не рассчитывается'],['-0.005','-0,01%'],['0','0,00%']])
 test('percent formatting '+value,()=>assert.equal(formatResultPercent(value),expected));
test('GET current preserves exact decimal wire values without Number',async()=>{
 globalThis.fetch=async(url,options)=>{assert.equal(url,`/api/financial-models/${id}/calculation`);assert.equal(options.method,'GET');return new Response(JSON.stringify({state:'current',isCurrent:true,result:resultFixture}).replace('"profit":100000000','"profit":999999999999999999.99'),{headers:{'content-type':'application/json'}});};
 assert.equal((await getCalculation(id)).result.profit,'999999999999999999.9900000000000000000000000000');
});
test('POST has no client sums or body and accepts structured 422',async()=>{
 globalThis.fetch=async(url,options)=>{assert.equal(url,`/api/financial-models/${id}/calculate`);assert.equal(options.method,'POST');assert.equal(options.body,undefined);return Response.json({state:'error',isCurrent:false,result:{...resultFixture,canPersistToCurrentSchema:false,persistenceIssues:[{field:'profit',code:'storageRange',message:'safe'}]}},{status:422});};
 assert.equal((await calculateModel(id)).state,'error');
});
for(const state of ['not_calculated','stale'])test('GET '+state+' never fabricates numbers',async()=>{globalThis.fetch=async()=>Response.json({state,isCurrent:false,result:null});assert.equal((await getCalculation(id)).result,null);});
for(const status of [400,404,409,500])test('HTTP '+status+' is exposed safely',async()=>{globalThis.fetch=async()=>Response.json({detail:'PRIVATE_STACK'},{status});await assert.rejects(()=>calculateModel(id),e=>e.status===status&&!e.message.includes('PRIVATE_STACK'));});
test('incomplete permits known revenue and unknown costs',async()=>{globalThis.fetch=async()=>Response.json({state:'incomplete',isCurrent:false,result:{...resultFixture,status:'incomplete',totalCosts:null,profit:null,roi:null,salesProfitability:null,missingInputs:['constructionResidentialArea'],canPersistToCurrentSchema:false}});const r=await calculateModel(id);assert.equal(r.result.totalCosts,null);assert(r.result.totalRevenue);});
test('inconsistent stale/current payloads are rejected',async()=>{for(const value of [{state:'stale',isCurrent:true,result:resultFixture},{state:'current',isCurrent:true,result:null}]){globalThis.fetch=async()=>Response.json(value);await assert.rejects(()=>getCalculation(id),e=>e.kind==='invalid-response');}});
test('AbortSignal is forwarded and invalid ids never fetch',async()=>{const controller=new AbortController();globalThis.fetch=async(url,options)=>{assert.equal(options.signal,controller.signal);return Response.json({state:'not_calculated',isCurrent:false,result:null});};await getCalculation(id,controller.signal);globalThis.fetch=()=>assert.fail('fetch forbidden');await assert.rejects(()=>getCalculation('bad'));});
