import json
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT=Path(__file__).resolve().parents[1]


class LauncherTests(unittest.TestCase):
    def test_numeric_latest_ready_release_skips_incomplete_and_false_markers(self):
        with tempfile.TemporaryDirectory() as temp:
            release=Path(temp)/'release';release.mkdir()
            for version,ready in [('1.9.0',True),('1.10.0',True),('99.0.0',False),('100.0.0',None)]:
                root=release/f'Gaode-PlcCommissioning-{version}-win-x64';(root/'runtime').mkdir(parents=True)
                (root/'runtime/python.exe').touch();(root/'Start-PLC.ps1').touch()
                if ready is not None:(root/'release.ready.json').write_text(json.dumps(dict(version=version,ready=ready)),encoding='utf-8')
            text=(ROOT/'tools/Workspace-Start-PLC.ps1').read_text(encoding='utf-8-sig')
            text=text.split("Write-Host ('使用最新联调包：'")[0]+"Write-Output $plcPackageRoot\n"
            text=text.replace('D:\\Gaode-PlcCommissioning-20261006\\release',str(release))
            script=Path(temp)/'select.ps1';script.write_text(text,encoding='utf-8-sig')
            result=subprocess.run(['C:/Windows/System32/WindowsPowerShell/v1.0/powershell.exe','-NoProfile','-File',str(script),'-NoBrowser'],capture_output=True,text=True)
            self.assertEqual(result.returncode,0,result.stderr)
            self.assertTrue(result.stdout.strip().endswith('Gaode-PlcCommissioning-1.10.0-win-x64'),result.stdout)


if __name__=='__main__':unittest.main()
