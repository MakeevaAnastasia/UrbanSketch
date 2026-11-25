import { test, afterEach } from 'node:test';
import assert from 'node:assert/strict';
import { emptyDraft, validateDraft, draftFromModel, isDraftDirty, normalizeArea, areaSourceText } from '../src/financial/parameters.ts';
import { serializeParameters, getFinancialModel } from '../src/api/financial.ts';
import { getConceptDetails } from '../src/api/client.ts';
import { formatTep } from '../src/formatTep.ts';
const originalFetch=globalThis.fetch;
afterEach(()=>{globalThis.fetch=originalFetch;});
const r='constructionResidentialAreaOverride', c='constructionCommercialAreaOverride';
const draft=()=>({...emptyDraft(),residentialConstructionCost:'100000',commercialConstructionCost:'80000',parkingCost:'500000',residentialSalePrice:'200000',commercialSalePrice:'150000',parkingSalePrice:'1000000',landCost:'14000000',additionalCosts:'10000000',projectDuration:'3'});
for(const [value,expected] of [['',null],['  ',null],['0','0.00'],['1000,25','1000.25'],['200.20','200.20'],['9999999999999999.99','9999999999999999.99']])
 test('normalize optional exact area '+JSON.stringify(value),()=>assert.equal(normalizeArea(value),expected));
for(const value of ['-1','1.001','10000000000000000','NaN','Infinity','1e3','1a','0.000000000000000000000001'])
 test('reject area without rounding '+value,()=>assert.throws(()=>normalizeArea(value)));
test('blank area fields validate as null while nine parameters remain required',()=>{
 const result=validateDraft(draft());assert.equal(result.parameters[r],null);assert.equal(result.parameters[c],null);
 assert.equal(validateDraft({...draft(),landCost:''}).parameters,null);
});
test('area errors remain attached to each field',()=>{
 const result=validateDraft({...draft(),[r]:'-1',[c]:'1.001'});assert(result.errors[r]);assert(result.errors[c]);
});
test('wire numbers are exact and clear is explicit null on PUT and POST',()=>{
 const data=validateDraft({...draft(),[r]:'9999999999999999,99'}).parameters;
 for(const cid of [undefined,'a18d9b8a-3d42-4b92-b7e0-095b3e1a0101']) {
  const raw=serializeParameters(data,cid);assert(raw.includes(`"${r}":9999999999999999.99`));assert(raw.includes(`"${c}":null`));
 }
});
test('dirty tracks only-area edits, clear and zero but normalizes comma/point',()=>{
 const base=draft();assert(isDraftDirty({...base,[r]:'0'},base));assert(isDraftDirty({...base,[c]:'240'},base));
 assert(!isDraftDirty({...base,[r]:'1,20'},{...base,[r]:'1.2'}));
 assert(isDraftDirty({...base,[r]:''},{...base,[r]:'0'}));
});
test('model GET retains exact maximum and draft distinguishes null and zero',async()=>{
 const data=validateDraft(draft()).parameters;
 const raw=serializeParameters(data).slice(0,-1)+',"id":"a18d9b8a-3d42-4b92-b7e0-095b3e1a0301","conceptVariantId":"a18d9b8a-3d42-4b92-b7e0-095b3e1a0101","totalCosts":null,"totalRevenue":null,"profit":null,"roi":null,"salesProfitability":null}';
 globalThis.fetch=async()=>new Response(raw.replace(`"${r}":null`,`"${r}":9999999999999999.99`).replace(`"${c}":null`,`"${c}":0`),{headers:{'content-type':'application/json'}});
 const model=await getFinancialModel('a18d9b8a-3d42-4b92-b7e0-095b3e1a0301');
 assert.equal(model[r],'9999999999999999.99');assert.equal(draftFromModel(model)[c],'0,00');
 assert.equal(draftFromModel({...model,[r]:null})[r],'');
 assert.equal(draftFromModel(model).landCost,'14000000,00');
});
test('area source priority retains zero and never uses saleable areas',()=>{
 assert.equal(areaSourceText('0','500.00'),'Используется пользовательское значение');
 assert.equal(areaSourceText('', '9999999999999999.99'),'Используется исходная площадь концепции: 9999999999999999,99 м²');
 assert.equal(areaSourceText('',null),'Строительная площадь не задана');
});
test('source construction area is decoded exactly from original numeric token',async()=>{
 const raw='{"id":"a18d9b8a-3d42-4b92-b7e0-095b3e1a0101","projectId":"a18d9b8a-3d42-4b92-b7e0-095b3e1a0100","name":"A","residentialArea":1000,"commercialArea":200,"parkingSpaces":20,"constructionResidentialArea":9999999999999999.99,"constructionCommercialArea":0,"residentsCount":null,"buildingsCount":null,"maxFloors":null,"socialAreas":null,"preschoolPlaces":null,"schoolPlaces":null}';
 globalThis.fetch=async()=>new Response(raw,{headers:{'content-type':'application/json'}});
 const detail=await getConceptDetails('a18d9b8a-3d42-4b92-b7e0-095b3e1a0101');
 assert.equal(detail.constructionResidentialArea,'9999999999999999.99');assert.equal(detail.constructionCommercialArea,'0.00');
});
test('exact source formatting preserves integer zeroes and fractional precision',()=>{
 assert.equal(formatTep('1000','м²'),'1000 м²');assert.equal(formatTep('0.00','м²'),'0 м²');
 assert.equal(formatTep('9999999999999999.99','м²'),'9999999999999999,99 м²');
});
