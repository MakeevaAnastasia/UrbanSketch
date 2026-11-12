import { test, afterEach } from 'node:test';
import assert from 'node:assert/strict';
import { normalizeMoney, validateDraft } from '../src/financial/parameters.ts';
import { exactDecimal, JsonNumber, parseExactJson } from '../src/financial/exactJson.ts';
import { isDraftDirty, draftFromModel } from '../src/financial/parameters.ts';
import { getFinancialModels, getFinancialModel, createFinancialModel, updateFinancialModel, deleteFinancialModel, serializeParameters } from '../src/api/financial.ts';

const fetchBefore = globalThis.fetch;
afterEach(() => { globalThis.fetch = fetchBefore; });

const fields = ['residentialConstructionCost','commercialConstructionCost','parkingCost','residentialSalePrice','commercialSalePrice','parkingSalePrice','landCost','additionalCosts'];
const draft = () => ({ ...Object.fromEntries(fields.map(key=>[key,'1000'])), projectDuration:'3' });
for (const [input, expected] of [['0','0.00'],['0,01','0.01'],['1.25','1.25'],['1 000,20','1000.20'],['999999999999999999,99','999999999999999999.99'],['0001,2','1.20']])
  test(`exact money normalization: ${input}`, () => assert.equal(normalizeMoney(input), expected));
for (const input of ['', '-1', '1.001', '1,234', '1000000000000000000', 'NaN', 'Infinity', '1e3', '1a', '1 00', '0.00000000000000000001'])
  test(`invalid money: ${input}`, () => assert.throws(() => normalizeMoney(input)));
test('all missing fields have errors and do not become zero', () => {
  const result = validateDraft({ ...Object.fromEntries(fields.map(key=>[key,''])), projectDuration:'' });
  assert.equal(result.parameters, null); assert.equal(Object.keys(result.errors).length, 9);
});
test('explicit zeroes are valid and money remains text', () => {
  const result = validateDraft({ ...Object.fromEntries(fields.map(key=>[key,'0'])), projectDuration:'1' });
  assert.equal(result.parameters.landCost, '0.00'); assert.equal(result.parameters.projectDuration, 1);
});
for (const value of ['0','-1','1.5','NaN','2147483648']) test(`invalid duration ${value}`, () => assert(validateDraft({...draft(),projectDuration:value}).errors.projectDuration));
test('maximum valid duration and monetary boundary are accepted exactly', () => {
  const result=validateDraft({...draft(),landCost:'999999999999999999.99',projectDuration:'2147483647'});
  assert.equal(result.parameters.landCost,'999999999999999999.99');assert.equal(result.parameters.projectDuration,2147483647);
});
const cid='a18d9b8a-3d42-4b92-b7e0-095b3e1a0101', mid='a18d9b8a-3d42-4b92-b7e0-095b3e1a0301';
const parameters=()=>validateDraft(draft()).parameters;
function rawModel(money='999999999999999999.99') {
  return '{"id":"'+mid+'","conceptVariantId":"'+cid+'",'+fields.map(key=>JSON.stringify(key)+':'+money).join(',')+',"projectDuration":3,"totalCosts":null,"totalRevenue":null,"profit":null,"roi":null,"salesProfitability":null}';
}
const rawResponse=(raw,status=200)=>new Response(raw,{status,headers:{'Content-Type':'application/json'}});
test('response numbers are retained before any Number conversion', () => {
  const tree=parseExactJson('{"n":999999999999999999.99,"text":"999999999999999999.99"}');
  assert(tree.n instanceof JsonNumber);assert.equal(tree.n.raw,'999999999999999999.99');assert.equal(typeof tree.text,'string');
});
for(const raw of ['{"a":1,"a":2}','{"n":01}','[1,]','{"n":NaN}','{"a":1}\u00a0'])
  test('lossless decoder rejects malformed/duplicate JSON '+raw,()=>assert.throws(()=>parseExactJson(raw)));
test('lossless decoder handles escaped strings, arrays and null',()=>{
  const tree=parseExactJson('{"text":"quote \\\" and \\\\ slash","array":[null,true,false,-1.25e2]}');
  assert.equal(tree.array[0],null);assert.equal(exactDecimal(tree.array[3].raw,20,2,false),'-125.00');
});
test('POST sends exact raw JSON numeric tokens, not numeric strings',async()=>{
  globalThis.fetch=async(url,options)=>{
    assert.equal(url,'/api/financial-models');assert.equal(options.method,'POST');
    assert(options.body.includes('"landCost":999999999999999999.99'));
    assert(!options.body.includes('"landCost":"'));assert(!options.body.includes('"id":'));
    return rawResponse(rawModel(),201);
  };
  const result=await createFinancialModel({...parameters(),landCost:'999999999999999999.99',conceptVariantId:cid});
  assert.equal(result.landCost,'999999999999999999.99');assert.equal(draftFromModel(result).landCost,'999999999999999999,99');
});
test('GET preserves numeric boundary exactly',async()=>{globalThis.fetch=async()=>rawResponse(rawModel());assert.equal((await getFinancialModel(mid)).landCost,'999999999999999999.99');});
test('list uses concept route and preserves multiple model IDs',async()=>{
  globalThis.fetch=async(url)=>{assert.equal(url,`/api/concepts/${cid}/financial-models`);return rawResponse('['+rawModel()+','+rawModel().replace(mid,mid.replace('0301','0302'))+']');};
  assert.equal((await getFinancialModels(cid)).length,2);
});
test('PUT whitelists editable fields and preserves raw precision',async()=>{
  globalThis.fetch=async(url,options)=>{assert.equal(url,`/api/financial-models/${mid}`);assert.equal(options.method,'PUT');assert(!options.body.includes('conceptVariantId'));assert(!options.body.includes('profit'));return rawResponse(rawModel());};
  await updateFinancialModel(mid,{...parameters(),conceptVariantId:cid,profit:'123'});
});
test('DELETE accepts only confirmed 204 without JSON parsing',async()=>{globalThis.fetch=async(url,options)=>{assert.equal(options.method,'DELETE');return new Response(null,{status:204});};await deleteFinancialModel(mid);});
test('ValidationProblemDetails exposes field errors without server details',async()=>{
  globalThis.fetch=async()=>Response.json({status:400,detail:'PRIVATE_DETAIL',errors:{landCost:['Ошибка суммы.']}},{status:400});
  await assert.rejects(()=>createFinancialModel({...parameters(),conceptVariantId:cid}),error=>error.status===400&&error.validationErrors.landCost[0]==='Ошибка суммы.'&&!error.message.includes('PRIVATE_DETAIL'));
});
for(const status of [404,500])test('financial HTTP '+status+' propagates safely',async()=>{
  globalThis.fetch=async()=>Response.json({detail:'PRIVATE_DETAIL'},{status});
  await assert.rejects(()=>getFinancialModel(mid),error=>error.status===status&&!error.message.includes('PRIVATE_DETAIL'));
});
test('quoted numeric values are not accepted as financial wire numbers',async()=>{
  globalThis.fetch=async()=>rawResponse(rawModel().replace('"landCost":999999999999999999.99','"landCost":"999999999999999999.99"'));
  await assert.rejects(()=>getFinancialModel(mid),error=>error.kind==='invalid-response');
});
test('empty financial identifiers never issue requests',async()=>{
  globalThis.fetch=async()=>assert.fail('fetch forbidden');
  for(const call of [getFinancialModels,getFinancialModel,deleteFinancialModel])await assert.rejects(()=>call(''),error=>error.kind==='invalid-request');
});
test('semantic dirty detection distinguishes blank and zero but not comma and point',()=>{
  assert.equal(isDraftDirty(draft(),{...draft(),landCost:'1000,00'}),false);
  assert.equal(isDraftDirty({...draft(),landCost:''},{...draft(),landCost:'0'}),true);
});
