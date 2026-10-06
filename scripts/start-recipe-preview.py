"""Start the current recipe editor and actual Host on Windows loopback."""
import argparse
import hashlib
import http.client
import json
import os
from pathlib import Path
import secrets
import shutil
import socket
import subprocess
import sys
import threading
import time
import urllib.request
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer

REPO = Path(__file__).resolve().parent.parent
ROOT = REPO / "artifacts" / "recipe-page-preview"
DATA = ROOT / "data"
HOST_PORT = 5411
PAGE_PORT = 5412
HOST_URL = f"http://127.0.0.1:{HOST_PORT}"
PAGE_URL = f"http://127.0.0.1:{PAGE_PORT}/a.html?role=L3"


def open_page():
    candidates = [
        Path(os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)")) / "Microsoft/Edge/Application/msedge.exe",
        Path(os.environ.get("ProgramFiles", r"C:\Program Files")) / "Microsoft/Edge/Application/msedge.exe",
        Path(os.environ.get("LOCALAPPDATA", "")) / "Microsoft/Edge/Application/msedge.exe",
        Path(os.environ.get("ProgramFiles", r"C:\Program Files")) / "Google/Chrome/Application/chrome.exe",
    ]
    try:
        browser = next((path for path in candidates if path.is_file()), None)
        if browser:
            subprocess.Popen([str(browser), "--new-window", PAGE_URL],
                             stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        else:
            os.startfile(PAGE_URL)
        print("已请求打开浏览器。如果窗口没有弹出，请在远程桌面的浏览器中打开：" + PAGE_URL)
    except OSError:
        print("浏览器未自动打开，请在远程桌面中手动打开：" + PAGE_URL)


def preview_running():
    try:
        with urllib.request.urlopen(PAGE_URL, timeout=2) as response:
            body = response.read()
            return response.status == 200 and b"__GAODE_HOST_CONFIG__" in body and b"recipe-authoring.js" in body
    except OSError:
        return False


def source_digest():
    digest = hashlib.sha256()
    files = [REPO / "global.json", REPO / "frontend/package.json",
             REPO / "frontend/package-lock.json", REPO / "frontend/scripts/build.mjs"]
    for folder in ("frontend/src", "backend/src", "backend/tools/Gaode.StorePrep"):
        files.extend(path for path in (REPO / folder).rglob("*")
                     if path.is_file() and not {"bin", "obj", "node_modules"}.intersection(path.relative_to(REPO).parts))
    files.extend(REPO.glob("Directory.*"))
    files.extend((REPO / "backend").glob("Directory.*"))
    for path in sorted(set(files)):
        if path.is_file():
            digest.update(str(path.relative_to(REPO)).encode())
            digest.update(path.read_bytes())
    return digest.hexdigest()


def run(command, log, cwd=REPO):
    result = subprocess.run(command, cwd=cwd, stdout=log, stderr=log)
    log.flush()
    if result.returncode:
        raise RuntimeError(f"命令失败，退出码 {result.returncode}；请查看 {log.name}")


def port_available(port):
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", port))


class PreviewHandler(SimpleHTTPRequestHandler):
    token = ""

    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(ROOT / "static"), **kwargs)

    def log_message(self, *_):
        # Request bodies, credentials and query strings must not enter console logs.
        pass

    def send_content(self, body, content_type, status=200):
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def proxy(self):
        body = self.rfile.read(int(self.headers.get("Content-Length", "0")))
        excluded = {"host", "connection", "content-length", "transfer-encoding"}
        headers = {key: value for key, value in self.headers.items()
                   if key.lower() not in excluded}
        connection = http.client.HTTPConnection("127.0.0.1", HOST_PORT, timeout=35)
        try:
            connection.request(self.command, self.path, body=body, headers=headers)
            response = connection.getresponse()
            content = response.read()
            self.send_response(response.status)
            for key, value in response.getheaders():
                if key.lower() not in {"connection", "content-length", "transfer-encoding"}:
                    self.send_header(key, value)
            self.send_header("Content-Length", str(len(content)))
            self.end_headers()
            self.wfile.write(content)
        except (OSError, http.client.HTTPException):
            self.send_content(b'{"message":"Preview backend unavailable"}',
                              "application/json", 502)
        finally:
            connection.close()

    def do_GET(self):
        if self.path.startswith("/api/"):
            return self.proxy()
        path = self.path.split("?", 1)[0]
        if path in ("/", "/a.html", "/prototype.html", "/login.html", "/data-view.html"):
            name = "a.html" if path == "/" else path[1:]
            html = (ROOT / "static" / name).read_text(encoding="utf-8")
            # Use real HTTP endpoints through this same-origin preview server.
            # No prepared load request is supplied: this launcher opens the editor.
            config = {"apiBaseUrl": "", "mode": "Test", "testToken": self.token,
                      "resourceVersion": "recipe-page-preview",
                      "prototypeSha256": "3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0"}
            script = "<script>window.__GAODE_HOST_CONFIG__=" + json.dumps(config) + ";</script>"
            html = html.replace("<head>", "<head>" + script, 1)
            return self.send_content(html.encode("utf-8"), "text/html; charset=utf-8")
        return super().do_GET()

    def do_POST(self):
        return self.proxy() if self.path.startswith("/api/") else self.send_error(404)

    do_PUT = do_POST
    do_DELETE = do_POST


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--verify-and-exit", action="store_true")
    parser.add_argument("--no-browser", action="store_true")
    args = parser.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")
    if os.name != "nt":
        raise RuntimeError("请在本机 Windows 远程桌面中运行。")
    if not args.verify_and_exit and preview_running():
        print("配方预览已经运行，直接打开页面，无需再次构建。")
        if not args.no_browser:
            open_page()
        return
    for port in (HOST_PORT, PAGE_PORT):
        try:
            port_available(port)
        except OSError as error:
            raise RuntimeError(f"端口 {port} 已被占用，请先在原预览启动窗口按 Ctrl+C 停止服务。") from error
    dotnet, node, npm = shutil.which("dotnet"), shutil.which("node"), shutil.which("npm.cmd")
    if not all((dotnet, node, npm)):
        raise RuntimeError("需要已安装的 .NET SDK、Node.js 和 npm。")
    ROOT.mkdir(parents=True, exist_ok=True)
    log_root = ROOT / "logs"
    log_root.mkdir(exist_ok=True)
    with (log_root / "build.log").open("w", encoding="utf-8") as build_log:
        signature = source_digest()
        marker = ROOT / "build-source.sha256"
        built = [REPO / "frontend/dist/a.html",
                 REPO / "backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll",
                 REPO / "backend/tools/Gaode.StorePrep/bin/Debug/net10.0/Gaode.StorePrep.dll"]
        reuse = marker.exists() and marker.read_text().strip() == signature and all(path.is_file() for path in built)
        if reuse:
            print("[1/4] 源码未变化，复用已验证的构建结果…", flush=True)
        else:
            print("[1/4] 构建当前前端和后端，可能需要2分钟…", flush=True)
            if not (REPO / "frontend/node_modules/tailwindcss/lib/cli.js").exists():
                print("正在准备前端依赖…", flush=True)
                run([npm, "ci"], build_log, REPO / "frontend")
            print("正在构建前端…", flush=True)
            run([npm, "run", "build"], build_log, REPO / "frontend")
            print("正在构建后端，详细进度见 build.log…", flush=True)
            run([dotnet, "build", "backend/src/Gaode.Host/Gaode.Host.csproj", "-c", "Debug"], build_log)
            print("正在构建数据库准备工具…", flush=True)
            run([dotnet, "build", "backend/tools/Gaode.StorePrep/Gaode.StorePrep.csproj", "-c", "Debug"], build_log)
            marker.write_text(signature, encoding="ascii")
        shutil.copytree(REPO / "frontend/dist", ROOT / "static", dirs_exist_ok=True)
        prep = REPO / "backend/tools/Gaode.StorePrep/bin/Debug/net10.0/Gaode.StorePrep.dll"
        runtime_root, recipes_root = DATA / "runtime", DATA / "recipes"
        print("[2/4] 准备独立预览数据库…", flush=True)
        if not (runtime_root / "station01.test.db").exists():
            run([dotnet, str(prep), str(DATA), str(runtime_root)], build_log)
        if not (recipes_root / "recipes.db").exists():
            run([dotnet, str(prep), "--prepare-recipes", str(DATA), str(recipes_root)], build_log)
            catalog = REPO / "specs/011-plc-interaction-update/examples/joint/catalog.json"
            digest = hashlib.sha256(catalog.read_bytes()).hexdigest().upper()
            run([dotnet, str(prep), "--seed-test-recipes", str(DATA), str(recipes_root),
                 str(catalog), digest], build_log)
    env = {key: value for key, value in os.environ.items()
           if not key.lower().startswith(("gaode__", "recipestore__"))}
    token = secrets.token_urlsafe(32)
    configuration = {
        "ASPNETCORE_ENVIRONMENT": "RecipePreview",
        "Gaode__Mode": "FullSimulation", "Gaode__PlcProvider": "Virtual",
        "Gaode__TestRoot": str(runtime_root), "Gaode__AllowedTestRoot": str(DATA),
        "Gaode__ConfigRoot": str(REPO / "specs/001-station01-public-preparation/examples"),
        "Gaode__SchemaRoot": str(REPO / "specs/001-station01-public-preparation/contracts"),
        "Gaode__PublicId": "s01-public-dev", "Gaode__PublicVersion": "1.0.0",
        "Gaode__BudgetId": "s01-budget-dev", "Gaode__BudgetVersion": "3.0.0",
        "Gaode__SimulationId": "s01-sim-normal", "Gaode__SimulationVersion": "3.0.0",
        "Gaode__Tokens__Operator": secrets.token_urlsafe(32),
        "Gaode__Tokens__SystemAdministrator": token,
        "RecipeStore__DatabasePath": str(recipes_root / "recipes.db"),
        "RecipeStore__ReadWriteTimeoutMs": "30000", "RecipeStore__DbLockTimeoutSeconds": "10",
    }
    env.update(configuration)
    host_dll = REPO / "backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll"
    print("[3/4] 启动后端，等待配方接口就绪…", flush=True)
    server = None
    with (log_root / "host.log").open("w", encoding="utf-8") as host_log:
        host = subprocess.Popen([dotnet, str(host_dll), "--urls", HOST_URL], cwd=REPO,
                                env=env, stdout=host_log, stderr=host_log,
                                creationflags=subprocess.CREATE_NO_WINDOW)
        try:
            deadline = time.monotonic() + 60
            request = urllib.request.Request(HOST_URL + "/api/v1/recipes/catalog",
                                             headers={"Authorization": "Bearer " + token})
            catalog_result = None
            while time.monotonic() < deadline:
                if host.poll() is not None:
                    raise RuntimeError(f"后端启动失败，请查看 {host_log.name}")
                try:
                    with urllib.request.urlopen(request, timeout=2) as response:
                        catalog_result = json.load(response)
                    break
                except OSError:
                    time.sleep(.5)
            if catalog_result is None:
                raise RuntimeError(f"配方接口未就绪，请查看 {host_log.name}")
            access = catalog_result.get("authoringAccess", {})
            if not access.get("canSave") or not access.get("canValidate"):
                raise RuntimeError("当前会话未取得真实配方检查和保存权限。")
            PreviewHandler.token = token
            server = ThreadingHTTPServer(("127.0.0.1", PAGE_PORT), PreviewHandler)
            server.daemon_threads = True
            thread = threading.Thread(target=server.serve_forever, daemon=True)
            thread.start()
            # Check the actual page and the same-origin API used by the browser.
            with urllib.request.urlopen(PAGE_URL, timeout=5) as response:
                html = response.read().decode("utf-8")
                if "recipe-authoring.js" not in html or "__GAODE_HOST_CONFIG__" not in html:
                    raise RuntimeError("配方编辑页面未加载完整。")
            request.full_url = f"http://127.0.0.1:{PAGE_PORT}/api/v1/recipes/catalog"
            with urllib.request.urlopen(request, timeout=5) as response:
                if json.load(response).get("count") != catalog_result.get("count"):
                    raise RuntimeError("前后端配方目录未对齐。")
            if args.verify_and_exit:
                for item in catalog_result.get("items", []):
                    read = urllib.request.Request(
                        f"http://127.0.0.1:{PAGE_PORT}/api/v1/recipes/{item['recipeId']}",
                        headers={"Authorization": "Bearer " + token})
                    with urllib.request.urlopen(read, timeout=5) as response:
                        complete = json.load(response)
                        if not complete.get("definition") or not response.headers.get("ETag"):
                            raise RuntimeError("完整配方或保存所需版本未返回。")
                    validate = urllib.request.Request(
                        f"http://127.0.0.1:{PAGE_PORT}/api/v1/recipes/validate",
                        data=json.dumps({"requestId": secrets.token_hex(16),
                                         "definition": complete["definition"]}).encode(),
                        headers={"Authorization": "Bearer " + token,
                                 "Content-Type": "application/json"}, method="POST")
                    with urllib.request.urlopen(validate, timeout=10) as response:
                        if json.load(response).get("valid") is not True:
                            raise RuntimeError("已有预览配方未通过后端检查。")
            print(f"[4/4] 已就绪，当前测试配方 {catalog_result.get('count', 0)} 份。", flush=True)
            print(f"页面：{PAGE_URL}\n后端：{HOST_URL}\n日志：{log_root}")
            print("点击页面上方“配方配置”，查看、新建或编辑配方。")
            print("修改保存在独立预览库，再次启动仍可读取。关闭服务请在此窗口按 Ctrl+C。")
            if args.verify_and_exit:
                print("启动、配方权限、前端页面及真实API连通检查通过。")
                return
            if not args.no_browser:
                open_page()
            while host.poll() is None:
                time.sleep(1)
            raise RuntimeError(f"后端已退出，请查看 {host_log.name}")
        finally:
            if server is not None:
                server.shutdown()
                server.server_close()
            if host.poll() is None:
                host.terminate()
                try:
                    host.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    host.kill()
                    host.wait(timeout=5)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("预览前后端已关闭，配方数据已保留。")
    except Exception as error:
        print(f"启动失败：{error}", file=sys.stderr)
        sys.exit(1)
