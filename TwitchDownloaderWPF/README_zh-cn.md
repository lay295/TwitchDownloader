> [!WARNING]
> 此文档可能并非最新版本！
>
> 若过时、有缺漏或错误，以[英语版本](README.md)为准。

# TwitchDownloaderWPF

一款基于 Windows WPF 的桌面应用程序，实现了核心功能并集成了多种提升用户体验的实用功能。

## 目录

- [TwitchDownloaderWPF](#twitchdownloaderwpf)
  - [目录](#目录)
  - [使用方法](#使用方法)
    - [点播下载](#点播下载)
    - [剪辑下载](#剪辑下载)
    - [聊天下载](#聊天下载)
    - [聊天更新器](#聊天更新器)
    - [聊天渲染](#聊天渲染)
      - [常规](#常规)
      - [渲染](#渲染)
      - [比例](#比例)
      - [编码](#编码)
      - [FFmpeg](#ffmpeg)
      - [优化渲染速度](#优化渲染速度)
    - [任务队列](#任务队列)
    - [设置](#设置)
  - [故障排除](#故障排除)
    - [非错误问题](#非错误问题)
    - [常见错误](#常见错误)
    - [罕见错误](#罕见错误)
  - [本地化](#本地化)
  - [主题](#主题)

---

## 使用方法

大部分页面窗口左侧为信息区域，显示点播 / 剪辑的缩略图（若可用）、实况主、创建日期和标题；窗口中部包含人物设置；右侧为事件记录区域。

### 点播下载

从 Twitch 下载点播 / 精选内容。

![图 1.1](Images/vodExample.png)
<br><sup>*图 1.1*</sup>

要开始操作，请输入有效点播 / 精选内容链接 / ID。若内容为私有或订阅限定，则需提供具有访问权限账户的 OAuth 令牌。解锁下载选项后即可自定义任务。

<ins>**Quality（画质）**</ins>：选择下载画质并显示预估文件大小。Twitch 有时会将最高质量称为“Source”（源）而非常规分辨率格式（如图 1.1 所示的 1080p60）。

<ins>**Trim Mode（裁剪模式）**</ins>：设置视频裁剪处理方式。精确裁剪可能在前后几秒出现音视频卡顿；安全裁剪可避免卡顿，但可能使视频稍长。

<ins>**Trim（裁剪）**</ins>：以 [时] [分] [秒] 的格式设置裁剪起止时间。裁剪将减少总下载量。

<ins>**Download Threads（下载线程）**</ins>：启用的并行下载线程数量。

**OAuth**：用于下载订阅限定或私有视频的授权令牌。Twitch 要求此令牌以防止未授权下载付费 / 私有内容。获取 OAuth 令牌教程：[https://youtu.be/1MBsUoFGuls](https://www.youtube.com/watch?v=1MBsUoFGuls)。<ins>**请勿向任何人分享你的 OAuth 令牌！**</ins>

<ins>**Download（下载）**</ins>：开始下载任务。通过下拉菜单选择“*Enqueue（添加至队列）*”可将任务发送至[任务队列](#任务队列)。两种方式均使用当前下载设置。

### 剪辑下载

从 Twitch 下载剪辑。

![图 2.1](Images/clipExample.png)
<br><sup>*图 2.1*</sup>

要开始操作，请输入有效剪辑链接 / ID。解锁下载选项后即可自定义任务。

<ins>**Quality（画质）**</ins>：选择剪辑下载画质。

<ins>**Encode Metadata（编码元数据）**</ins>：使用 FFmpeg 将剪辑元数据（如直播日期和剪辑 ID）编码至最终 MP4 文件。

<ins>**Download（下载）**</ins>：开始下载任务。通过下拉菜单选择“*Enqueue（添加至队列）*”可将任务发送至[任务队列](#任务队列)。两种方式均使用当前下载设置。

### 聊天下载

Downloads the chat of a VOD, highlight, or clip.

![图 3.1](Images/chatdownload1Example.png)
<br><sup>*图 3.1*</sup>

![图 3.2](Images/chatdownload2Example.png)
<br><sup>*图 3.2*</sup>

要开始操作，请输入有效点播 / 精选内容 / 剪辑链接 / ID。解锁下载选项后即可自定义任务。若点播或精选内容为私有或订阅限定，则无法下载聊天。此为 Twitch API 限制，而非 TwitchDownloader。

<ins>**Download Format（下载格式）**</ins>：聊天保存格式。

- `JSON`：输出富文本聊天记录，可用于更新和渲染；
- `Text`：输出原始文本格式，适合观看视频时阅读；
- `HTML`：输出模拟 Twitch 网页界面的本地网页。

<ins>**Compression（压缩）**</ins>（仅 JSON）：使用 GZip 压缩标准减小文件体积（40-90%）。若需手动编辑聊天文件（不使用[聊天更新器](#聊天更新器)功能则不推荐启用）。

<ins>**Timestamp Format（时间戳格式）**</ins>（仅 Text）：文本下载中的时间戳格式。可选 `UTC`、`Relative`（视频相对时间）或 `None`（无）。

<ins>**Trim（裁剪）**</ins>：以 [时] [分] [秒] 的格式设置裁剪起止时间。裁剪将减少总下载量。

<ins>**Embed Images（嵌入图片）**</ins>（仅 JSON 和 HTML）：下载实况主表情和徽章并嵌入聊天文件。文件体积将显著增大。

<ins>**3rd Party Emotes（第三方表情）**</ins>（仅 JSON 和 HTML）：同时下载指定第三方平台的表情并嵌入聊天文件。若实况主未在平台注册则自动跳过。

<ins>**Download Threads（下载线程）**</ins>：启用的并行下载线程数量。部分网络环境下，Twitch 服务器可能限制仅使用 1 个线程。

<ins>**Download（下载）**</ins>：开始下载任务。通过下拉菜单选择“*Enqueue（添加至队列）*”可将任务发送至[任务队列](#任务队列)。两种方式均使用当前下载设置。

### 聊天更新器

更新 JSON 聊天文件中嵌入的表情、徽章、比特及裁剪范围，并 / 或将 JSON 聊天文件转换为另一种格式。

![图 4.1](Images/chatupdateExample.png)
<br><sup>*图 4.1*</sup>

要开始操作，请点击“<ins>**Browse（浏览）**</ins>”按钮并选择先前下载的 JSON 聊天文件。解锁下载选项后即可自定义任务。若源视频仍存在，其信息将加载至信息区域。

<ins>**Download Format（下载格式）**</ins>：聊天保存格式。

- `JSON`：输出富文本聊天记录，可用于更新和渲染；
- `Text`：输出原始文本格式，适合观看视频时阅读；
- `HTML`：输出模拟 Twitch 网页界面的本地网页。

<ins>**Compression（压缩）**</ins>（仅 JSON）：使用 GZip 压缩标准减小文件体积（40-90%）。若需手动编辑聊天文件（不使用[聊天更新器](#聊天更新器)功能则不推荐启用）。

<ins>**Timestamp Format（时间戳格式）**</ins>（仅 Text）：文本下载中的时间戳格式。可选 `UTC`、`Relative`（视频相对时间）或 `None`（无）。

<ins>**Trim（裁剪）**</ins>：以 [时] [分] [秒] 的格式设置裁剪起止时间。扩大范围将尝试获取原下载未包含的聊天；缩小范围不会删除聊天。

<ins>**Embed Missing（嵌入缺失项）**</ins>（仅 JSON 和 HTML）：下载原始 JSON 未包含的表情或徽章。已有项不会被覆盖。

<ins>**Replace Embeds（替换嵌入项）**</ins>（仅 JSON 和 HTML）：丢弃原始 JSON 中所有现存表情和徽章并重新下载。

<ins>**3rd Party Emotes（第三方表情）**</ins>（仅 JSON 和 HTML）：同时下载指定第三方平台表情并嵌入聊天文件。若实况主未在平台注册则自动跳过。

<ins>**Update（更新）**</ins>：开始更新任务。通过下拉菜单选择“*Enqueue（添加至队列）*”可将任务发送至[任务队列](#任务队列)。两种方式均使用当前更新设置。

### 聊天渲染

将聊天 JSON 渲染为视频。

![图 5.1](Images/chatrender1Example.png)
<br><sup>*图 5.1*</sup>

![图 5.2](Images/chatrender2Example.png)
<br><sup>*图 5.2*</sup>

![图 5.3](Images/chatrender3Example.png)
<br><sup>*图 5.3*</sup>

![图 5.4](Images/chatrender4Example.png)
<br><sup>*图 5.4*</sup>

![图 5.5](Images/chatrender5Example.png)
<br><sup>*图 5.5*</sup>

![图 5.6](Images/rangeExample.png)
<br><sup>*图 5.6*</sup>

要开始操作，请点击“<ins>**Browse（浏览）**</ins>”按钮并选择先前下载的 JSON 聊天文件。随后即可通过渲染选项自定义任务。

**渲染**：开始渲染任务。通过下拉菜单选择“*Enqueue（添加至队列）*”可将任务发送至[任务队列](#任务队列)；选择“*Partial Render（部分渲染）*”可渲染聊天片段（见图 5.6）。所有方式均使用当前渲染设置。

#### <ins>常规</ins>

<ins>**Width（宽度）**</ins>：输出视频的宽（必须为偶数）。

<ins>**Height（高度）**</ins>：输出视频的高（必须为偶数）。

<ins>**Font（字体）**</ins>：输出视频所用字体（Twitch 网站使用 *Inter*，本工具内置为 *Inter Embedded*）。

<ins>**Font Size（字体大小）**</ins>：字体大小。

<ins>**Font Color（字体颜色）**</ins>：消息字体颜色。

<ins>**Background Color（背景颜色）**</ins>：输出视频背景颜色。

<ins>**Alt Background Color（交替背景颜色）**</ins>：消息替代背景颜色（需启用“*Alternate Backgrounds〔交替背景〕*”）。

#### <ins>渲染</ins>

<ins>**Outline（边框）**</ins>：为用户名和消息添加细黑边。

<ins>**Timestamps（时间戳）**</ins>：在消息旁显示相对于视频开始的时间。

<ins>**Sub Messages（订阅消息）**</ins>：渲染订阅、续订及礼物消息。禁用后将过滤此类消息。

<ins>**Chat Badges（聊天徽章）**</ins>：在用户名旁显示徽章。

<ins>**Update Rate（更新频率）**</ins>：绘制下批评论时间间隔（秒）。值越低聊天流越易读，但略微增加渲染时间。

<ins>**Dispersion（离散化）**</ins>：2022 年 11 月 Twitch API 变更后，聊天消息仅能按整秒下载。此选项尝试使用元数据还原消息实际发送时间，可能导致评论顺序变化（需将更新频率设置为小于 1.0 以获得有效效果）。

<ins>**Alternate Backgrounds（交替背景）**</ins>：隔行切换消息背景颜色以提高辨识度。

<ins>**Increase Username Visibility（增强用户名可见性）**</ins>：提高用户名与背景的对比度（类似于 Twitch 的“可读颜色”选项）。启用描边时，此选项将增强用户名与描边的对比度。

<ins>**BTTV Emotes（BTTV 表情）**</ins>：启用 BTTV 平台表情渲染。

<ins>**FFZ Emotes（FFZ 表情）**</ins>：启用 FFZ 平台表情渲染。

<ins>**7TV Emotes（7TV 表情）**</ins>：启用 7TV 平台表情渲染。

<ins>**Offline（离线）**</ins>：仅使用 JSON 中嵌入的信息和图片渲染（无网络请求）。

<ins>**User Avatars（用户头像）**</ins>：在渲染中显示用户头像。

<ins>**Chat Badge Filter（聊天勋章过滤器）**</ins>：不渲染指定徽章（例如图 5.2 中永不渲染“*No Autio / No Video〔无音频 / 无视频〕*”徽章）。

<ins>**Ignore Users List（屏蔽用户列表）**</ins>：逗号分隔、不区分大小写的用户列表（渲染时将移除）。例如图 5.2 将移除 Streamlabs、StreamElements 和 Nightbot。

<ins>**Banned Words List（屏蔽词列表）**</ins>：逗号分隔、不区分大小写的禁用词列表（包含这些词的消息将被移除）。例如图 5.2 将移除包含 `" pog "`、`"[pOg+"`、`"/POg9"` 的消息，但保留包含 `" poggers "` 的消息。

<ins>**Emoji Vendor（Emoji 提供者）**</ins>：渲染所用表情符号风格。支持 Twitter 的 *Twemoji*、Google 的 *Noto Color* 及系统默认表情（*None〔无〕*）。

#### <ins>比例</ins>

<ins>**Emote Scale（表情比例）**</ins>：表情缩放比例。

<ins>**Badge Scale（徽章比例）**</ins>：徽章缩放比例。

<ins>**Emoji Scale（Emoji 比例）**</ins>：Emoji 缩放比例。

<ins>**Avatar Scale（头像比例）**</ins>：头像缩放比例。

<ins>**Outline Scale（描边比例）**</ins>：描边粗细比例。

<ins>**Vertical Spacing Scale（垂直间距比例）**</ins>：消息间垂直间距比例。

<ins>**Side Padding Scale（侧边距比例）**</ins>：水平内边距比例。

<ins>**Section Height Scale（区域高度比例）**</ins>：单行文本高度比例。

<ins>**Word Spacing Scale（词间距比例）**</ins>：词语间水平间距比例。

<ins>**Emote Spacing Scale（表情间距比例）**</ins>：表情与表情 / 词语间的间距比例。

<ins>**Highlight Stroke Scale（高亮描边比例）**</ins>：高亮 / 订阅消息侧边栏宽度比例。

<ins>**Highlight Indent Scale（高亮缩进比例）**</ins>：高亮 / 订阅消息缩进比例。

#### <ins>编码</ins>

<ins>**File Format（文件格式）**</ins>：输出视频格式。

<ins>**Codec（编解码器）**</ins>：输出视频所用编解码器。

<ins>**Framerate（帧速率）**</ins>：输出视频帧速率。

<ins>**Generate Mask（生成遮罩）**</ins>：生成包含文本和图像黑白遮罩的副文件。背景颜色 Alpha 通道必须小于 255。

<ins>**Sharpening（锐化）**</ins>：对渲染视频应用锐化滤镜。略微增加渲染时间和文件大小（建议字体大小大于等于 24 时使用）。

#### <ins>FFmpeg</ins>

**警告：修改 FFmpeg 参数可能导致管道错误！**

<ins>**Input Arguments（输入参数）**</ins>：控制 FFmpeg 渲染输入的参数。

<ins>**Output Arguments（输出参数）**</ins>：控制 FFmpeg 编码输出的参数。

<ins>**Reset To Defaults（重置为默认值）**</ins>：重置 FFmpeg 参数至默认状态。

#### <ins>优化渲染速度</ins>

若渲染速度过慢，可尝试以下方式：

| 显著提升 | 中等提升 | 轻微提升 |
|-|-|-|
| 降低渲染宽度 | 禁用 BTTV、FFZ、7TV 表情 | 更新频率小于 1.0 时禁用离散化 |
| 降低渲染高度 | 提高更新频率 | 禁用订阅消息 |
| 降低帧速率 | 切换至系统 Emoji | 禁用描边 |
| 禁用生成遮罩 | | 禁用交替背景           |
| 禁用图像锐化 | | 禁用用户头像 |
| 切换编码器至 H.264 | | |

### 任务队列

创建并管理多个任务。

![图 6.1](Images/taskqueueExample.png)
<br><sup>*图 6.1*</sup>

![图 6.2](Images/massurlExample.png)
<br><sup>*图 6.2*</sup>

![图 6.3](Images/massvodExample.png)
<br><sup>*图 6.3*</sup>

![图 6.4](Images/massclipExample.png)
<br><sup>*图 6.4*</sup>

![图 6.5](Images/enqueueExample.png)
<br><sup>*图 6.5*</sup>

任务队列支持多个任务顺序或并行执行。其他 5 个页面的任务均可通过“*Enqueue（添加至队列）*”按钮发送至任务队列（见图 6.5）。

任务队列页面包含 4 类限制器：

<ins>**VOD Downloads（点播下载）**</ins>：同时进行的点播 / 精选内容下载任务数量。

<ins>**Clip Downloads（剪辑下载）**</ins>：同时进行的剪辑下载任务数量。

<ins>**Chat Downloads（聊天下载）**</ins>：同时进行的聊天下载 / 更新任务数量。

<ins>**Chat Renders（聊天渲染）**</ins>：同时进行的聊天渲染任务数量。

任务队列还支持 3 种批量下载模式：

<ins>**URL List（URL 列表）**</ins>：一个使用相同设置批量处理点播、精选内容、剪辑 URL 的列表（见图 6.2 和 6.5）。

<ins>**Search VODs（搜索点播）**</ins>：一个搜索实况主所有点播并使用相同设置批量处理的窗口（见图 6.3 和 6.5）。

<ins>**Search Clips（搜索剪辑）**</ins>：一个搜索实况主所有剪辑并使用相同设置批量处理的窗口（见图 6.3 和 6.5）。

### 设置

管理应用程序行为。

![图 7.1](Images/settingsExample.png)
<br><sup>*图 7.1*</sup>

<ins>**Cache Folder（缓存文件夹）**</ins>：临时工作文件存储目录（含点播下载、表情、徽章等）。

- <ins>**Clear（清除）**</ins>：删除所有缓存文件（仅推荐异常时使用）。
- <ins>**Browse（浏览）**</ins>：选择新缓存目录（不迁移现有文件）。

<ins>**Hide Donation Button（隐藏捐赠按钮）**</ins>：隐藏捐赠按钮。

<ins>**Time Format（时间格式）**</ins>：控制界面和文件名模板中的时间显示格式。

<ins>**Verbose Errors（详细错误）**</ins>：启用错误时的详细弹窗提示。

<ins>**Theme（主题）**</ins>：应用程序主题（详见[主题](#主题)）。

<ins>**Language（语言）**</ins>：应用程序语言。（详见[本地化](#本地化)）。

<ins>**Maximum Thread Bandwidth（最大线程宽带）**</ins>：单个下载线程最大带宽（单位：KiB/s）。

<ins>**Log Levels（日志级别）**</ins>：启用不同日志级别以便于调试。

<ins>**Download Filename Templates（下载文件名模板）**</ins>：下载文件的默认命名模板。

<ins>**Restore Defaults（重置为默认值）**</ins>：重置所有设置（含各页面记忆设置）。重新启动后生效。

<ins>**Save（保存）**</ins>：保存当前设置并关闭窗口。

<ins>**Cancel（取消）**</ins>：放弃更改并关闭窗口。

## 故障排除

### 非错误问题

以下问题不属于应用程序错误，请附带复现步骤提交 [GitHub 议题](https://github.com/lay295/TwitchDownloader/issues)：

- 视频下载卡在 `99%` 超过 5 分钟
- 聊天渲染状态超过 10 秒未更新
- 渲染后聊天缺失消息
- UI 元素未响应主题切换
- 选项更改（如嵌入表情）未生效

### 常见错误

“常见错误”指任务开始前或启动后立即发生的错误，通常附带友好错误说明（可能含弹窗）。例如：

- 无法获取缩略图
  - 点播已过期或正在直播
- 无法获取视频 / 剪辑信息
  - 点播 / 剪辑无效、被移除、为私有或订阅限定视频未提供有效 OAuth
- 无法解析输入
  - 渲染输入无效（详见日志）

### 罕见错误

“罕见错误”表现为“致命错误”弹窗或不友好的错误信息。请附带复现步骤提交 [GitHub 议题](https://github.com/lay295/TwitchDownloader/issues)。例如：

- Error converting value 'XXX' to type 'XXX'. Path 'XXX', line #, position #.（类型转换错误）
- Cannot access child value on Newtonsoft.Json.Linq.JValue.（JSON 解析异常）
- Response status code does not indicate success：404 (Not Found).（网络响应异常）
- The pipe has been ended.（管道终止）
  - FFmpeg 异常。请重置参数后重试，若仍失败请提交议题。

为便于定位错误，请在[设置](#设置)中启用“详细错误”并截图保存“详细错误输出”弹窗。

## 本地化

本应用程序支持多语言，感谢社区成员的翻译贡献。

如果你对自己的翻译能力有信心，且 TwitchDownloaderWPF 尚未提供你的母语版本，或你的母语版本翻译尚不完整，我们诚邀你加入翻译团队！

如果你需要帮助，可以查阅提交 [53245be1fe55768d525905e81cc2cd0c12469b03](https://github.com/lay295/TwitchDownloader/commit/53245be1fe55768d525905e81cc2cd0c12469b03)、查阅 [AvailableCultures.cs](Services/AvailableCultures.cs)、查阅原始[本地化讨论贴](https://github.com/lay295/TwitchDownloader/issues/445)、或提交[议题](https://github.com/lay295/TwitchDownloader/issues/new/choose)寻求帮助。

不确定的字符串可保留英文原文。

## 主题

> [!WARNING]
> 此部分相较于原文有所修改！
>
> 若过时、有缺漏或错误，以[英语版本](README.md#theming)为准。

此应用程序支持用户自定义主题！

要开始制作，只需复制其中一个内置主题，并用你喜欢的文本编辑器打开它。

HandyControl 元素不提供完整的主题支持，但它们包含一个深色变体，因此专门提供了布尔键。

布尔键控制：

- 标题栏主题
- HandyControl 元素主题
- …

SolidColorBrush 键控制颜色属性：

- 应用程序背景
- 文本颜色
- 边框颜色
- …

Inner 键用于为双递归元素添加视觉深度。以下图表说明了层次结构：

```层次结构图
+----------------------------[-][#][x]-+
|             AppBackground            |
| +----------------------------------+ |
| |       AppElementBackground       | |
| | +------------------------------+ | |
| | |   AppInnerElementBackground  | | |
| | +----------------------------- + | |
| +----------------------------------+ |
+--------------------------------------+
```  

使用示例：

- `AppElementBackground` 用于框架元素
- `AppInnerElementBackground` 用于带边框的标签、空白图像背景等。

如果你制作了一个主题并希望将其包含在官方发行版中，请提交 [GitHub 拉取请求](https://github.com/lay295/TwitchDownloader/pulls)。

重要注意事项：  

1. `Dark.xaml` 和 `Light.xaml` 将在应用程序启动时被覆盖。
2. 文件名不区分大小写（例如 `Dark.xaml` = `dark.xaml`）。
3. 编辑主题文件顶部的作者注释！

有关制作自定义主题的离线说明，请参阅 [Themes/README.txt](Themes/README.txt)，此文件会在每次运行时重新生成。
