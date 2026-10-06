"""Finite Modbus evidence check against a separately transcribed oracle.

This is a communication probe: it never declares business completion. Production
definitions/codecs and VirtualPlc are deliberately not imported. Packet framing
is checked before signals, ownership, widths and optional literal write sequences.
"""
import argparse
from datetime import datetime
import hashlib
import json
from pathlib import Path
import struct
import sys


def require(condition, reason):
    if not condition:
        raise ValueError(reason)


def frame(value):
    packet = bytes.fromhex(value)
    require(len(packet) >= 8, "TruncatedMbap")
    transaction, protocol, length = struct.unpack(">HHH", packet[:6])
    require(protocol == 0 and length == len(packet) - 6, "InvalidMbapLengthOrProtocol")
    return transaction, packet[6], packet[7:]


def check(evidence, oracle, expectation=None, all_reads=False):
    require(evidence.get("schemaVersion") == "009-wire-evidence/1", "EvidenceSchema")
    require(bool(evidence.get("runId")) and bool(evidence.get("caseId")), "EvidenceIdentity")
    require(evidence.get("gap") is False, "EvidenceGapOrNotDeclared")
    signals = oracle["signals"]
    occupied = {}
    for signal in signals:
        for offset in range(signal["pduOffset"], signal["pduOffset"] + signal["width"]):
            key = signal["area"], offset
            require(key not in occupied, "OracleOverlap")
            occupied[key] = signal
    reads, writes, write_values = set(), set(), {}
    seen, successes, failures, predispatch = set(), 0, 0, 0
    records = evidence.get("exchanges", [])
    require(bool(records), "MissingExchanges")
    for record in records:
        key = record["connectionId"], record["sequence"], record["channel"]
        require(key not in seen and key[1] > 0 and bool(key[0]), "ExchangeIdentity")
        seen.add(key)
        if record.get("request") is None:
            # Preserve the real failed pre-dispatch attempt. No request/response,
            # transaction or protocol fields may be fabricated for this record.
            require(bool(record.get("error")) and record.get("response") is None,
                    "UnsentAttemptEvidenceInvalid")
            require(all(k in record and record[k] is None for k in
                        ("transactionId", "unit", "function", "offset", "count")),
                    "UnsentAttemptProtocolMetadataInvalid")
            require(bool(record.get("startedUtc")) and bool(record.get("endedUtc")),
                    "UnsentAttemptTimeMissing")
            require(datetime.fromisoformat(record["startedUtc"].replace("Z","+00:00")) <=
                    datetime.fromisoformat(record["endedUtc"].replace("Z","+00:00")),
                    "UnsentAttemptTimeOrder")
            failures += 1
            predispatch += 1
            continue
        transaction, unit, request = frame(record["request"])
        function = request[0]
        require(function in (1, 3, 5, 6, 15, 16), "UnsupportedFunction")
        require(len(request) >= 5, "RequestLength")
        offset, value = struct.unpack(">HH", request[1:5])
        reading = function in (1, 3)
        area = "Coil" if function in (1, 5, 15) else "HoldingRegister"
        count = value if function in (1, 3, 15, 16) else 1
        maximum = {1: 2000, 3: 125, 5: 1, 6: 1, 15: 1968, 16: 123}[function]
        require(0 < count <= maximum, "AccessLength")
        if function in (1, 3, 5, 6):
            require(len(request) == 5, "RequestLength")
        else:
            byte_count = (count + 7) // 8 if function == 15 else count * 2
            require(len(request) == 6 + byte_count and request[5] == byte_count, "WritePayloadLength")
        response_hex = record.get("response")
        if response_hex is None:
            require(bool(record.get("error")), "ResponseAndFailureBothMissing")
            failures += 1
            continue  # no receipt, hence no successful-access coverage
        response_transaction, response_unit, response = frame(response_hex)
        require((transaction, unit) == (response_transaction, response_unit), "ResponseIdentity")
        if response[0] == function | 0x80:
            require(len(response) == 2, "ExceptionResponseLength")
            failures += 1
            continue  # actual protocol refusal is retained, not counted as an accepted write
        require(response[0] == function, "ResponseFunction")
        if reading:
            expected_bytes = (count + 7) // 8 if function == 1 else count * 2
            require(len(response) == expected_bytes + 2 and response[1] == expected_bytes, "ReadResponseLength")
        else:
            require(response == request[:5], "WriteEchoMismatch")
        covered = {}
        for address in range(offset, offset + count):
            require((area, address) in occupied, "UndeclaredAddress")
            signal = occupied[(area, address)]
            require(offset <= signal["pduOffset"] and signal["pduOffset"] + signal["width"] <= offset + count,
                    "PartialFieldAccess")
            covered[signal["id"]] = signal
        for identity, signal in covered.items():
            if reading:
                reads.add(identity)
                continue
            require(signal["writer"] == "PC" and signal["direction"] == "PCToPLC", "WriteOwnership")
            at = signal["pduOffset"] - offset
            if function == 5:
                require(value in (0, 0xFF00), "CoilEncoding")
                words = [int(value == 0xFF00)]
            elif function == 6:
                words = [value]
            elif function == 15:
                words = [(request[6 + at // 8] >> (at % 8)) & 1]
            else:
                words = list(struct.unpack(">" + "H" * signal["width"],
                                           request[6 + at * 2:6 + (at + signal["width"]) * 2]))
            writes.add(identity)
            write_values.setdefault(identity, []).append(words)
        successes += 1
    require(successes > 0, "NoSuccessfulExchange")
    if all_reads:
        require(reads == {s["id"] for s in signals}, "RequiredSignalReadMissing")
    if expectation is not None:
        require(bool(expectation.get("basis")), "IndependentExpectationBasisMissing")
        for identity, values in expectation.get("writeValues", {}).items():
            require(write_values.get(identity) == values, "IndependentWriteSequenceMismatch:" + identity)
    return {"runId": evidence["runId"], "caseId": evidence["caseId"], "status": "Passed",
            "successfulExchanges": successes, "failedExchanges": failures, "preDispatchFailures": predispatch,
            "readSignals": sorted(reads), "writtenSignals": sorted(writes),
            "scope": "CommunicationPacketsOnly;NoBusinessCompletionClaim"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--oracle", type=Path, required=True)
    parser.add_argument("--expectation", type=Path)
    parser.add_argument("--require-all-reads", action="store_true")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = {"status": "Failed"}
    try:
        payload = json.loads(args.evidence.read_text(encoding="utf-8-sig"))
        oracle = json.loads(args.oracle.read_text(encoding="utf-8-sig"))
        expectation = json.loads(args.expectation.read_text(encoding="utf-8-sig")) if args.expectation else None
        result = check(payload, oracle, expectation, args.require_all_reads)
    except (ValueError, KeyError, TypeError, OSError, struct.error) as error:
        result["reason"] = str(error)
    result["inputs"] = {str(p.resolve()): hashlib.sha256(p.read_bytes()).hexdigest()
                        for p in (args.evidence, args.oracle, args.expectation) if p is not None and p.is_file()}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False))
    return 0 if result["status"] == "Passed" else 1


if __name__ == "__main__":
    sys.exit(main())
