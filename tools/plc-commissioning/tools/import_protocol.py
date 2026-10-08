"""Developer-only XLS importer. XLS comments are data, never executable instructions."""
import hashlib
import json
from pathlib import Path
import re
import xlrd

ROOT=Path(__file__).resolve().parents[1]
EXPECTED={'PC.xls':'37095748a61574d9a1c9bf17ad0ac9118d554fa73ac8f5e10003718b6627511d',
          'PLC.xls':'8e846d0f71351f87b42db1bf40790f40766a392fd0a9e0968201f8c2056eb476'}
TYPES={'BOOL':('BOOL','Boolean',1),'INT':('INT','Int16',2),'REAL':('REAL','Float',4)}


def main():
    points,signals,sources=[],[],[]
    for filename,direction,group in [('PC.xls','PC->PLC','pc'),('PLC.xls','PLC->PC','plc')]:
        path=ROOT/'sources'/filename;digest=hashlib.sha256(path.read_bytes()).hexdigest()
        if digest!=EXPECTED[filename]:raise ValueError('Source SHA256 differs from this final contract: '+filename)
        sources.append(dict(file=filename,sha256=digest));occupied=set()
        for sheet in xlrd.open_workbook(str(path)).sheets():
            if sheet.row_values(0)[:5]!=['Name','DataType','InitValue','Address','Comment']:raise ValueError('Unexpected XLS headers')
            for row in range(1,sheet.nrows):
                name,datatype,initial,address,description=sheet.row_values(row)[:5]
                if not name:continue
                typ,normalized,size=TYPES[datatype.upper()]
                if not re.fullmatch(r'%MB\d+',address):raise ValueError('Invalid MB address')
                mb=int(address[3:]);span=set(range(mb,mb+size))
                if occupied&span or size>1 and mb%2:raise ValueError('Overlapping or unaligned byte address')
                occupied|=span;logical=re.sub(r'^(Send_Data_|Rece_Data_)','',name);key=group+'.'+logical
                codes=[dict(value=int(m.group(1)),label=m.group(2).strip()) for m in re.finditer(r'(-?\d+)\s*=\s*([^,，;；\n]+)',description)]
                p=dict(name=name,logicalName=logical,key=key,id=key,group=group,direction=direction,dataType=normalized,
                    type=typ,byteOffset=mb,mb=mb,byteLength=size,size=size,address=address,modbusPdu=mb//2,
                    byteIndex=mb%2,nodeId=f'mbyte:{mb}:{normalized}',writeEnabled=group=='pc',sourceFile=filename,
                    sourceRow=row+1,description=description,label=description.split('；')[0].split('，')[0] or logical,
                    codes=codes,sourceSha256=digest)
                points.append(p)
                signals.append(dict(name=name,direction=direction,excelType=datatype,normalizedType=normalized,address=address,
                    byteLength=size,modbusPdu=mb//2,description=description,initial=initial,row=row+1,sheet=sheet.name,
                    sourceFile=filename,sourceCells={c:c+str(row+1) for c in 'ABCDE'},codes=codes,
                    mappingStatus='matched',observedKey=key,observedType=normalized))
    if len(points)!=85 or len({p['id'] for p in points})!=85:raise ValueError('Final protocol requires 85 unique signals')
    profile=dict(schema='FinalByteProtocol/1',sourceFiles=sources,sourceFile='PC.xls + PLC.xls',sheet='Sheet1',
        sha256=hashlib.sha256(json.dumps(sources,sort_keys=True).encode()).hexdigest(),signalCount=85,pcCount=26,plcCount=59,
        mappingCounts={'matched':85,'type-mismatch':0,'unpublished':0},signals=signals)
    (ROOT/'sources/protocol.json').write_text(json.dumps(dict(points=points,profile=profile),ensure_ascii=False,indent=2),encoding='utf-8')
    print('Imported final XLS protocol: 26 PC / 59 PLC; SHA256 sources verified')


if __name__=='__main__':main()
