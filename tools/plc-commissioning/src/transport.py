"""Standard-library Modbus TCP. No device discovery or implicit writes."""
import asyncio
import math
import struct
import time
from network import automatic_source


class ModbusError(Exception):
    def __init__(self, message, code=None):
        super().__init__(message)
        self.code = code


ERRORS = {1: '不支持的功能码', 2: '非法地址', 3: '非法数据值',
          4: 'PLC 执行失败', 5: '应答处理中', 6: 'PLC 忙', 10: '网关不可达', 11: '网关目标无响应'}
PERMUTATIONS = {'Abcd': (0, 1, 2, 3), 'Badc': (1, 0, 3, 2),
                'Cdab': (2, 3, 0, 1), 'Dcba': (3, 2, 1, 0)}


def pdu_address(point, cfg):
    pc = point['direction'] == 'PC->PLC'
    return cfg['pcBase' if pc else 'plcBase'] + (point['mb'] - (2000 if pc else 6000)) // 2


def byte_shift(point, cfg):
    low = (point['mb'] % 2 == 0) == (cfg['boolOrder'] == 'EvenLow')
    return 0 if low else 8


def real_interpretations(registers):
    raw = struct.pack('>HH', *registers)
    result = {}
    for order, indexes in PERMUTATIONS.items():
        value = struct.unpack('>f', bytes(raw[i] for i in indexes))[0]
        result[order] = value if math.isfinite(value) else None
    return result


def real_order(point, cfg):
    key = "realWriteOrder" if point["direction"] == "PC->PLC" else "realReadOrder"
    return cfg.get(key, cfg["realOrder"])


def decode(point, registers, cfg):
    if point['type'] == 'BOOL':
        value = (registers[0] >> byte_shift(point, cfg)) & 255
        return value, 'Good' if value in (0, 1) else 'InvalidBoolByte'
    if point['type'] == 'INT':
        return struct.unpack('>h', struct.pack('>H', registers[0]))[0], 'Good'
    value = real_interpretations(registers)[real_order(point, cfg)]
    return value, 'Good' if value is not None else 'InvalidReal'


def encode(point, value, cfg):
    if point['type'] == 'BOOL':
        if type(value) is not int or value not in (0, 1):
            raise ValueError('BOOL 请填写整数 0 或 1')
        return [value]
    if point['type'] == 'INT':
        if type(value) is not int or not -32768 <= value <= 32767:
            raise ValueError('INT 请填写 -32768..32767 的整数')
        return [value & 65535]
    if type(value) not in (int, float) or not math.isfinite(value):
        raise ValueError('REAL 请填写有限数值')
    try:
        raw = struct.pack('>f', value)
    except (OverflowError, struct.error):
        raise ValueError('REAL 超出 Float32 范围') from None
    if not math.isfinite(struct.unpack('>f', raw)[0]):
        raise ValueError('REAL 超出 Float32 范围')
    indexes = PERMUTATIONS[real_order(point, cfg)]
    return list(struct.unpack('>HH', bytes(raw[i] for i in indexes)))


class Transport:
    """Caller serializes complete read-modify-write sequences, not just packets."""
    def __init__(self, cfg, event):
        self.cfg, self.event = cfg, event
        self.reader = self.writer = None
        self.tid = 0
        self.last_response = None
        self.timeouts = 0
        self.context = {}
        self.local_endpoint = None
        self.remote_endpoint = None

    async def connect(self):
        source = self.cfg.get('sourceAddress') or await asyncio.to_thread(automatic_source, self.cfg['host'])
        self.reader, self.writer = await asyncio.wait_for(
            asyncio.open_connection(self.cfg['host'], self.cfg['port'],
                local_addr=(source,0) if source else None), self.cfg['timeout'])
        self.local_endpoint = self.writer.get_extra_info('sockname')
        self.remote_endpoint = self.writer.get_extra_info('peername')
        self.event('NETWORK_PATH', '实际TCP源地址与PLC端点', **self.context, requestedSource=self.cfg.get('sourceAddress') or 'auto-physical', localEndpoint=self.local_endpoint, remoteEndpoint=self.remote_endpoint)

    async def close(self):
        writer, self.writer, self.reader = self.writer, None, None
        if writer:
            writer.close()
            try:
                await writer.wait_closed()
            except OSError:
                pass

    async def request(self, pdu):
        if self.writer is None:
            raise ConnectionError('TCP 尚未连接')
        self.tid = (self.tid + 1) & 65535
        tx = struct.pack('>HHHB', self.tid, 0, len(pdu) + 1, self.cfg['unitId']) + pdu
        started = time.monotonic()
        fields = dict(self.context, transactionId=self.tid, function=pdu[0],
                      address=int.from_bytes(pdu[1:3], 'big') if len(pdu) >= 3 else None,
                      quantity=int.from_bytes(pdu[3:5], 'big') if pdu[0] in (3,4,16) else None)
        written_registers = list(struct.unpack('>'+'H'*((len(pdu)-6)//2),pdu[6:])) if pdu[0]==16 else [int.from_bytes(pdu[3:5],'big')] if pdu[0]==6 else None
        self.event('TX', '发送 Modbus 请求', **fields, hex=tx.hex(' ').upper(),rawRegisters=written_registers,
                   maskRegisters=list(struct.unpack('>HH',pdu[3:])) if pdu[0]==22 else None)
        try:
            self.writer.write(tx)
            await asyncio.wait_for(self.writer.drain(), self.cfg['timeout'])
            header = await asyncio.wait_for(self.reader.readexactly(7), self.cfg['timeout'])
            tid, protocol, length, unit = struct.unpack('>HHHB', header)
            if (tid, protocol, unit) != (self.tid, 0, self.cfg['unitId']) or not 2 <= length <= 254:
                self.event('RX_INVALID', 'Modbus 头不匹配', **fields, hex=header.hex(' ').upper())
                await self.close()
                raise ModbusError('Modbus 响应头不匹配（事务号/协议号/站号/长度）', 'MBAP_MISMATCH')
            body = await asyncio.wait_for(self.reader.readexactly(length - 1), self.cfg['timeout'])
            elapsed = round((time.monotonic() - started) * 1000, 2)
            raw_registers=list(struct.unpack('>'+'H'*((len(body)-2)//2),body[2:])) if body[0] in (3,4) and len(body)>=2 and (len(body)-2)%2==0 else None
            self.event('RX', '收到 Modbus 应答', **fields, hex=(header + body).hex(' ').upper(), elapsedMs=elapsed,rawRegisters=raw_registers)
            if body[0] == pdu[0] | 128:
                if len(body) != 2:
                    raise ModbusError('异常应答长度错误', 'BAD_EXCEPTION_LENGTH')
                raise ModbusError(f'Modbus 异常 0x{body[1]:02X}：{ERRORS.get(body[1], "未知异常")}', body[1])
            if body[0] != pdu[0]:
                raise ModbusError('应答功能码不匹配', 'FUNCTION_MISMATCH')
            return body
        except asyncio.TimeoutError:
            self.timeouts += 1
            self.event('TIMEOUT', 'Modbus 超时，未自动重发写入', **fields, elapsedMs=round((time.monotonic()-started)*1000,2), errorCode='TIMEOUT')
            await self.close()
            raise ModbusError('Modbus 请求超时（TIMEOUT）', 'TIMEOUT') from None
        except (OSError, asyncio.IncompleteReadError) as error:
            await self.close()
            raise ModbusError(f'TCP 中断：{error}', getattr(error, 'winerror', None) or 'TCP_CLOSED') from None

    async def read(self, address, count, fc=3):
        if fc not in (3, 4) or not 1 <= count <= 125 or not 0 <= address <= 65535-count+1:
            raise ValueError('读取地址/数量/功能码无效')
        body = await self.request(struct.pack('>BHH', fc, address, count))
        if len(body) != count*2+2 or body[1] != count*2:
            raise ModbusError('寄存器应答长度不符', 'READ_LENGTH')
        self.last_response = time.time()
        return list(struct.unpack('>'+'H'*count, body[2:]))

    async def write(self, point, value):
        if point['direction'] != 'PC->PLC':
            raise ValueError('PLC 反馈点不允许写入')
        values = encode(point, value, self.cfg)
        address = pdu_address(point, self.cfg)
        if point['type'] == 'BOOL':
            shift = byte_shift(point, self.cfg)
            if self.cfg['byteWrite'] == 'FC22':
                tx = struct.pack('>BHHH', 22, address, 65535 ^ (255 << shift), value << shift)
                body = await self.request(tx)
                if body != tx:
                    raise ModbusError('FC22 回执不匹配', 'WRITE_ECHO')
                sent = None
            else:
                prior = (await self.read(address, 1, 3))[0]
                updated = (prior & (65535 ^ (255 << shift))) | (value << shift)
                self.event('BYTE_MERGE', '整字节合并，保留相邻 BYTE', **dict(self.context,mb=point['mb']),
                           address=address, before=prior, after=updated, shift=shift,
                           adjacentBefore=(prior >> (8-shift)) & 255)
                tx = struct.pack('>BHH', 6, address, updated)
                if await self.request(tx) != tx:
                    raise ModbusError('FC06 回执不匹配', 'WRITE_ECHO')
                sent = [updated]
        else:
            raw = struct.pack('>'+'H'*len(values), *values)
            tx = struct.pack('>BHHB', 16, address, len(values), len(raw)) + raw
            if await self.request(tx) != tx[:5]:
                raise ModbusError('FC16 回执不匹配', 'WRITE_ECHO')
            sent = values
        return sent
