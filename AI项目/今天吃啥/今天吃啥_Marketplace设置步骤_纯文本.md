设置步骤

前置条件（5分钟）

你需要两样东西：Dify账号用来运行Workflow，注册地址 https://dify.ai。飞书账号用来做数据库存储，注册地址 https://www.feishu.cn。


第一步：创建飞书多维表格（3分钟）

1. 打开 https://fvs5liujf2.feishu.cn/base/ 登录飞书
2. 右上角点"+ 新建"，选"多维表格"，名字填"吃饭记录"
3. 加四列：吃了什么（多行文本）、主料（文本）、花了多少钱（数字）、用户（文本）
4. 复制地址栏链接，提取 app_token 和 table_id

你的链接形如 https://xxx.feishu.cn/base/ABC123?table=tblXYZ789
那么 app_token 就是 ABC123，table_id 就是 tblXYZ789。记下来，后面要用。


第二步：创建飞书应用（3分钟）

1. 打开 https://open.feishu.cn/app 扫码登录
2. 右上角点"创建企业自建应用"，名字填"今天吃啥 Agent"
3. 左侧点"权限管理"，搜索 bitable，勾选 bitable:app
4. 左侧点"版本管理与发布"，点"创建版本"，填 v1.0，点"发布"
5. 左侧点"凭证与基础信息"，复制 App ID 和 App Secret。记下来，后面要用。


第三步：给应用授权表格（1分钟）

1. 打开你的飞书表格
2. 右上角点"…"（更多），点"权限设置"，点"关联应用"
3. 搜索"今天吃啥 Agent"，添加，权限给"可编辑"


第四步：导入Dify Workflow

1. 在Dify Marketplace找到本模板，点"导入"
2. 打开Workflow，修改以下节点：

节点2（HTTP拿Token）：Body里的app_id改成你的，app_secret改成你的。

节点3（HTTP查历史）：URL里的 Ysreb14G6aarTSsNVxkcaCVYnwd 改成你的app_token，tbloSgrPT93gaMpt 改成你的table_id。

节点5（HTTP写入）：URL和Body同上，改成你自己的app_token和table_id。

3. 右上角点"发布"


第五步：测试

打开Bot，输入：中午吃啥，自己做，20块，1人

应该收到一份推荐，包含菜名、价格、营养说明和已存档提示。

打开飞书表格刷新，看到一行新记录，完成。


进阶（可选）

加代码节点自动刷新Token：在节点1后面加一个"代码执行"节点，自动提取tenant_access_token，下游节点引用变量，Token永不过期。

接微信：搭配开源项目 dify-on-wechat，微信直接对话Agent。
