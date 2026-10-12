# 项目官网

已选定 **01 原生相伴**。首页是 `docs/index.html`，样式与脚本在 `docs/site/`，原生状态条沿用 `docs/images/skins/`。页面使用相对资源路径，不需要构建。

本地查看：在仓库根目录运行 `python -m http.server 8757 --bind 127.0.0.1 --directory docs`，然后打开 http://127.0.0.1:8757/ 。深浅皮肤、完整/精简模式和安装提示复制都可直接操作。

官网地址：[Codex Status](https://mogoostudio.github.io/mogoo-codex-status/) 。发布来源为 `master` 分支的 `/docs` 目录，采用 Deploy from a branch；推送该目录的变更后，GitHub Pages 自动更新。详见 GitHub 官方 [配置发布来源](https://docs.github.com/en/pages/getting-started-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site)。`docs/.nojekyll` 已包含在页面文件中。

页面中的数值引用原生截图预览样本，不读取网页访客的 Codex 账号。工作场景由 HTML 编排，顶部状态条为实际原生控件截图。设计记录和此前的多方案保留在本地 `docs/designs/github-page/`，不随官网发布；最终首页以 `docs/index.html` 为准。
