import {test} from 'node:test';
import assert from 'node:assert/strict';
import http from 'node:http';
import {once} from 'node:events';
import {forwardPageRequest} from '../014-page-proxy.mjs';

test('observer delivers a live SSE notification before upstream completion and releases its owned upstream on page close',async()=>{
 let upstreamClosed=false;
 const upstream=http.createServer((req,res)=>{res.writeHead(200,{'content-type':'text/event-stream'});res.write('data: actual-component-notification\n\n');res.on('close',()=>{upstreamClosed=true;});});
 upstream.listen(0,'127.0.0.1');await once(upstream,'listening');
 const proxy=http.createServer((req,res)=>forwardPageRequest(req,res,new URL('http://127.0.0.1:'+upstream.address().port),()=>assert.fail('SSE must not become an API JSON result')).catch(error=>res.destroy(error)));
 proxy.listen(0,'127.0.0.1');await once(proxy,'listening');
 try{
  const request=http.get('http://127.0.0.1:'+proxy.address().port+'/hubs/station01');
  const [response]=await Promise.race([once(request,'response'),new Promise((_,reject)=>{const timer=setTimeout(()=>reject(Error('Live response was buffered')),1500);timer.unref();})]);
  const [chunk]=await once(response,'data');assert.match(chunk.toString(),/actual-component-notification/);
  response.destroy();request.destroy();
  const end=Date.now()+1500;while(!upstreamClosed&&Date.now()<end)await new Promise(resolve=>setTimeout(resolve,10));
  assert.equal(upstreamClosed,true,'closing the page must abort its live upstream fetch');
 }finally{proxy.closeAllConnections();upstream.closeAllConnections();await Promise.all([new Promise(resolve=>proxy.close(resolve)),new Promise(resolve=>upstream.close(resolve))]);}
});
