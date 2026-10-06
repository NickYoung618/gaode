# Windows 安装包边界

安装包必须包含 `frontend/dist/` 下的 `login.html`、`a.html`、`data-view.html`、`assets/`、`vendor/` 和 `prototype-hash.txt`，并在安装/首次启动时检查系统 WebView2 Runtime。缺 Runtime、缺静态资源或后端不可用必须分别报告，不得显示业务成功。

当前仓库未引入第三方安装器；安装器选型和具体构建命令由本目录的 T036 锁定。安装包不得携带 PLC 地址、相机 SDK、算法端口或数据库连接串。
