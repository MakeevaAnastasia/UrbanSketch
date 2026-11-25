import { useCallback, useEffect, useRef, useState } from 'react';
import { calculateModel, getCalculation } from '../api/calculation.ts';
import type { CalculationResponse } from '../api/calculation.ts';
import { ApiError, isAbortError } from '../api/http.ts';

interface State { key: string | null; loading: boolean; calculating: boolean; data: CalculationResponse | null; error: string }
const empty = (key: string | null): State => ({key,loading:false,calculating:false,data:null,error:''});
export function useCalculation(id: string | null) {
  const [state,setState] = useState<State>(()=>empty(id));
  const owner = useRef(id); owner.current=id;
  const serial = useRef(0), controller = useRef<AbortController | null>(null), busy = useRef(false);
  const request = useCallback(async (key: string, post: boolean) => {
    if (post && busy.current) return;
    controller.current?.abort(); const abort = new AbortController(); controller.current=abort;
    const version=++serial.current; busy.current=post;
    setState({...empty(key),loading:!post,calculating:post});
    try {
      const data=await (post ? calculateModel(key,abort.signal) : getCalculation(key,abort.signal));
      if(owner.current===key && version===serial.current && !abort.signal.aborted) setState({...empty(key),data});
    } catch(error: unknown) {
      if(owner.current!==key || version!==serial.current || abort.signal.aborted || isAbortError(error)) return;
      const message=error instanceof ApiError && error.status===409 ? 'Данные могли измениться. Обновите результат и повторите расчёт.'
        : error instanceof ApiError && error.status===404 ? 'Финансовая модель не найдена. Вернитесь к финансовым параметрам и обновите список.'
        : error instanceof ApiError ? error.message : 'Не удалось загрузить расчёт. Повторите попытку.';
      setState({...empty(key),error:message});
    } finally { if(version===serial.current) busy.current=false; }
  },[]);
  useEffect(()=>{
    busy.current=false;
    if(id) void request(id,false); else setState(empty(null));
    return ()=>{serial.current++;controller.current?.abort();busy.current=false;};
  },[id,request]);
  return {...(state.key===id?state:empty(id)), reload:()=>{if(id&&!busy.current) void request(id,false);}, calculate:()=>{if(id) void request(id,true);}};
}
