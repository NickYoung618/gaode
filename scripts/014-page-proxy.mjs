import {Readable} from 'node:stream';

// Read-only page observer transport, not a workflow/PLC driver. In particular an
// SSE response must reach the page before its long-lived upstream response ends.
export async function forwardPageRequest(req,res,upstream,onApiRecord){
 if(req.method!=='GET'&&!req.url.startsWith('/hubs/')){res.writeHead(405).end();return;}
 const cancellation=new AbortController();
 const closed=()=>cancellation.abort();res.once('close',closed);
 try{
  const chunks=[];for await(const chunk of req)chunks.push(chunk);
  const body=Buffer.concat(chunks);
  const response=await fetch(new URL(req.url,upstream),{method:req.method,
   headers:{authorization:req.headers.authorization||'',...(req.headers['content-type']?{'content-type':req.headers['content-type']}:{})},
   body:body.length?body:undefined,signal:cancellation.signal});
  const contentType=response.headers.get('content-type')||'application/octet-stream';
  res.writeHead(response.status,{'content-type':contentType,...(response.headers.has('etag')?{etag:response.headers.get('etag')}:{})});
  if(contentType.startsWith('text/event-stream')){
   res.flushHeaders();
   const stream=Readable.fromWeb(response.body);
   stream.on('error',error=>{if(!cancellation.signal.aborted)res.destroy(error);});
   stream.pipe(res);
   // Ownership lasts until the actual response/client closes; caller shutdown
   // aborts the upstream request by closing its owned page connections.
   return;
  }
  const bytes=Buffer.from(await response.arrayBuffer());
  if(req.url.startsWith('/api/')){
   let value;try{value=JSON.parse(bytes);}catch{value=bytes.toString();}
   onApiRecord?.({observedAtUtc:new Date().toISOString(),method:req.method,path:req.url,status:response.status,etag:response.headers.get('etag'),response:value});
  }
  res.end(bytes);
 }catch(error){if(!cancellation.signal.aborted)throw error;}
}
