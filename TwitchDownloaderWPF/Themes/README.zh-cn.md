此应用程序支持用户自定义主题！

要开始使用，请复制任意内置主题文件，使用你常用的文本编辑器进行修改。

HandyControl 控件元素（HandyControl Elements）暂未提供完整主题支持，但包含深色变体，故使用专用布尔型键值（Boolean Keys）控制。

布尔型键值主要控制：

- 标题栏主题
- HandyControl 控件元素主题样式
- ……

SolidColorBrush 型键值（SolidColorBrush Keys）控制以下颜色属性：

- 应用程序背景色
- 文本颜色
- 边框颜色
- ……

内层键值（Inner Keys）用于增强双层递归元素（Double-Recursive Elements）的视觉层次。
层级示意图如下：

```层级示意图
+---------------------------------------[-][#][x]-+
|             AppBackground (应用背景)             |
| +---------------------------------------------+ |
| |      AppElementBackground (界面元素背景)     | |
| | +-----------------------------------------+ | |
| | | AppInnerElementBackground (内层元素背景) | | |
| | +-----------------------------------------+ | |
| +---------------------------------------------+ |
+-------------------------------------------------+
```  

示例说明：

- `AppElementBackground` 用于框架元素（Frame Elements）
- `AppInnerElementBackground` 用于带边框标签（Bordered Labels）、空白图像背景（Blank Image Backgrounds）等次级元素

若你制作了新主题并希望将其纳入官方版本，欢迎提交 GitHub 拉取请求：  
<https://github.com/lay295/TwitchDownloader/pulls>

重要提示：  

1. 程序启动时将强制覆盖 `Dark.xaml` 和 `Light.xaml` 文件。
2. 文件名读取不区分大小写，`Dark.xaml` 与 `dark.xaml` 被视为同一文件。
3. 请修改主题文件顶部的作者注释。
