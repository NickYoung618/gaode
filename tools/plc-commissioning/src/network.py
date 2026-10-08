"""Select a local physical PLC interface without probing any remote devices."""
import ipaddress
import json
import os
from pathlib import Path
import subprocess


def choose_source(host, adapters):
    target = ipaddress.ip_address(host)
    if target.is_loopback:
        return None
    candidates = []
    for item in adapters:
        if not item.get('physical') or item.get('status') != 'Up':
            continue
        address = item['address']
        subnet = ipaddress.ip_network(f"{address}/{item['prefix']}", strict=False)
        if target in subnet:
            candidates.append(item)
    preferred = [item for item in candidates if item['alias'].upper() == 'PLC']
    if len(preferred) == 1:
        return preferred[0]['address']
    if len(candidates) == 1:
        return candidates[0]['address']
    if len(candidates) > 1:
        raise ValueError('有多个同网段实体网卡，请在高级设置填写本机PLC网卡IP')
    raise ValueError('没有找到与PLC同网段的在线实体网卡，请核对网线和网卡IP；未选择代理虚拟网卡')


def automatic_source(host):
    try:
        target = ipaddress.ip_address(host)
    except ValueError:
        return None
    if os.name != 'nt' or target.is_loopback:
        return None
    command = """
$adapters = Get-NetAdapter
$items = @(foreach ($address in Get-NetIPAddress -AddressFamily IPv4) {
    $adapter = $adapters | Where-Object InterfaceIndex -eq $address.InterfaceIndex | Select-Object -First 1
    if ($adapter) {
        [pscustomobject]@{address=$address.IPAddress;prefix=$address.PrefixLength;alias=$adapter.Name;physical=[bool]$adapter.HardwareInterface;status=[string]$adapter.Status}
    }
})
ConvertTo-Json -InputObject $items -Compress
"""
    executable = Path(os.environ.get('SystemRoot', r'C:\Windows')) / 'System32/WindowsPowerShell/v1.0/powershell.exe'
    output = subprocess.run([str(executable), '-NoProfile', '-NonInteractive', '-Command', command],
                            capture_output=True, check=True, timeout=8,
                            creationflags=subprocess.CREATE_NO_WINDOW)
    # Only alias 'PLC' needs ASCII matching; Windows console encoding varies.
    adapters = json.loads(output.stdout.decode('utf-8', errors='replace').lstrip('\ufeff'))
    return choose_source(host, adapters)
