"""Explicit loopback-only PLC simulator, never binds to a LAN interface."""
import argparse
import asyncio
import json
from pathlib import Path
import struct
import sys
import time
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'src'))
from app import AXES
from transport import encode,decode,pdu_address,byte_shift


class Simulator:
    def __init__(self,root,cfg=None):
        root=Path(root)
        self.cfg=cfg or json.loads((root/'config/default.json').read_text(encoding='utf-8'))
        self.points=json.loads((root/'sources/protocol.json').read_text(encoding='utf-8'))['points']
        self.by_mb={p['mb']:p for p in self.points}
        self.registers={i:0 for i in range(65536)}
        self.requests=[]
        self.stale_axes=set()
        self.motion={}
        self.completed_axes=set()
        self.clear_delay=0
        self.hold_clear=set()
        self.clear_due={}

        self.fail_function=None
        self.bad_tid=False
        self.silent=False
        self.server=None
        self.writers=set()
        for axis,(_,target,start,feedback,actual) in AXES.items():
            self.set(feedback,0)
        self.set(6015,1)
        self.set(6016,1)

    def set(self,mb,value):
        p=self.by_mb[mb];addr=pdu_address(p,self.cfg)
        vals=encode(p,value,self.cfg)
        if p['type']=='BOOL':
            shift=byte_shift(p,self.cfg)
            self.registers[addr]=(self.registers[addr] & (65535 ^ (255<<shift))) | (value<<shift)
        else:
            for i,v in enumerate(vals):self.registers[addr+i]=v

    def get(self,mb):
        p=self.by_mb[mb];addr=pdu_address(p,self.cfg)
        return decode(p,[self.registers[addr+i] for i in range(2 if p['type']=='REAL' else 1)],self.cfg)[0]

    def update(self):
        self.set(6038,int(time.monotonic()*5)%2)
        for axis,(started,baseline,target) in list(self.motion.items()):
            _,_,_,feedback,actual=AXES[axis]
            elapsed=time.monotonic()-started
            if elapsed<.12:
                self.set(feedback,0)
                self.set(actual,baseline+(target-baseline)*min(1,elapsed/.25))
            elif elapsed<.8:
                self.set(feedback,0)
                self.set(actual,baseline+(target-baseline)*.75)
            else:
                self.set(actual,target)
                self.set(feedback,1)
                del self.motion[axis]
                self.completed_axes.add(axis)

        for axis in self.completed_axes:
            _,_,start,feedback,_=AXES[axis]
            if self.get(start)==0 and axis not in self.motion:
                self.clear_due.setdefault(axis,time.monotonic()+self.clear_delay)
                if axis not in self.hold_clear and time.monotonic()>=self.clear_due[axis]:self.set(feedback,0)

    def start_edges(self,before):
        for axis,(_,target,start,feedback,actual) in AXES.items():
            if before[axis]==0 and self.get(start)==1 and axis not in self.stale_axes:
                self.completed_axes.discard(axis);self.clear_due.pop(axis,None)
                self.motion[axis]=(time.monotonic(),self.get(actual),self.get(target))
                self.set(feedback,0)

    async def serve(self,reader,writer):
        self.writers.add(writer)
        try:
            while True:
                header=await reader.readexactly(7)
                tid,protocol,length,unit=struct.unpack('>HHHB',header)
                pdu=await reader.readexactly(length-1)
                self.requests.append(dict(at=time.time(),tx=(header+pdu).hex(),function=pdu[0]))
                self.update()
                if self.silent:continue
                fc=pdu[0]
                before={a:self.get(v[2]) for a,v in AXES.items()}
                if fc==self.fail_function:
                    reply=bytes([fc|128,2])
                elif fc in (3,4):
                    addr,count=struct.unpack('>HH',pdu[1:])
                    reply=bytes([fc,count*2])+struct.pack('>'+'H'*count,*[self.registers[addr+i] for i in range(count)])
                elif fc==6:
                    addr,value=struct.unpack('>HH',pdu[1:]);self.registers[addr]=value
                    self.start_edges(before);reply=pdu
                elif fc==16:
                    addr,count,size=struct.unpack('>HHB',pdu[1:6])
                    for i,value in enumerate(struct.unpack('>'+'H'*count,pdu[6:])):self.registers[addr+i]=value
                    self.start_edges(before);reply=pdu[:5]
                elif fc==22:
                    addr,mask,value=struct.unpack('>HHH',pdu[1:]);self.registers[addr]=(self.registers[addr]&mask)|(value&(65535^mask))
                    self.start_edges(before);reply=pdu
                else:reply=bytes([fc|128,1])
                packet=struct.pack('>HHHB',(tid+1)&65535 if self.bad_tid else tid,protocol,len(reply)+1,unit)+reply
                self.requests[-1]['rx']=packet.hex()
                writer.write(packet);await writer.drain()
        except (asyncio.IncompleteReadError,ConnectionResetError):pass
        finally:
            self.writers.discard(writer)
            writer.close()

    async def start(self,port=0):
        self.server=await asyncio.start_server(self.serve,'127.0.0.1',port)
        return self.server.sockets[0].getsockname()[1]

    async def close(self):
        self.server.close();await self.server.wait_closed()
        for writer in list(self.writers):writer.close()


async def main():
    parser=argparse.ArgumentParser();parser.add_argument('--port',type=int,default=15020)
    args=parser.parse_args();sim=Simulator(Path(__file__).resolve().parents[1]);await sim.start(args.port)
    print(f'LOOPBACK simulator 127.0.0.1:{args.port}',flush=True)
    async with sim.server:await sim.server.serve_forever()

if __name__=='__main__':asyncio.run(main())
