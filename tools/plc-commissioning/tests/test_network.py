import sys
from pathlib import Path
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'src'))
from network import choose_source


class NetworkTests(unittest.TestCase):
    def adapter(self, address, alias, physical=True):
        return dict(address=address, alias=alias, physical=physical, prefix=24, status='Up')

    def test_prefers_plc_physical_adapter_among_two_same_subnet(self):
        adapters = [self.adapter('192.168.0.20', 'C'), self.adapter('192.168.0.88', 'PLC'),
                    dict(self.adapter('198.18.0.1', 'Meta', False), prefix=0)]
        self.assertEqual(choose_source('192.168.0.10', adapters), '192.168.0.88')

    def test_does_not_select_proxy_or_disconnected_adapter(self):
        with self.assertRaises(ValueError):
            choose_source('192.168.0.10', [dict(self.adapter('198.18.0.1', 'Meta', False), prefix=0),
                                         dict(self.adapter('192.168.0.88', 'PLC'), status='Disconnected')])

    def test_ambiguous_physical_adapters_require_explicit_source(self):
        with self.assertRaises(ValueError):
            choose_source('192.168.0.10', [self.adapter('192.168.0.20', 'C'), self.adapter('192.168.0.88', 'LAN')])
        self.assertIsNone(choose_source('127.0.0.1', []))
