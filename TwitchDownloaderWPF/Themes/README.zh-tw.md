此應用程式支援使用者自訂主題！

要開始使用，請複製任意內建主題檔案，使用你常用的文字編輯器進行修改。

HandyControl 控制元件元素（HandyControl Elements）暫未提供完整主題支援，但包含深色變體，故使用專用布林型鍵值（Boolean Keys）控制。

布林型鍵值主要控制：

- 標題欄主題
- HandyControl 控制元件元素主題樣式
- ……

SolidColorBrush 型鍵值（SolidColorBrush Keys）控制以下顏色屬性：

- 應用程式背景色
- 文字顏色
- 邊框顏色
- ……

內層鍵值（Inner Keys）用於增強雙層遞迴元素（Double-Recursive Elements）的視覺層次。
層級示意圖如下：

```層級示意圖
+---------------------------------------[-][#][x]-+
|             AppBackground (應用背景)             |
| +---------------------------------------------+ |
| |      AppElementBackground (介面元素背景)     | |
| | +-----------------------------------------+ | |
| | | AppInnerElementBackground (內層元素背景) | | |
| | +-----------------------------------------+ | |
| +---------------------------------------------+ |
+-------------------------------------------------+
```  

示例說明：

- `AppElementBackground` 用於框架元素（Frame Elements）
- `AppInnerElementBackground` 用於帶邊框標籤（Bordered Labels）、空白影象背景（Blank Image Backgrounds）等次級元素

若你製作了新主題並希望將其納入官方版本，歡迎提交 GitHub 拉取請求：  
<https://github.com/lay295/TwitchDownloader/pulls>

重要提示：  

1. 程式啟動時將強制覆蓋 `Dark.xaml` 和 `Light.xaml` 檔案。
2. 檔名讀取不區分大小寫，`Dark.xaml` 與 `dark.xaml` 被視為同一檔案。
3. 請修改主題檔案頂部的作者註釋。
