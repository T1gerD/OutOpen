// 项目关闭了隐式 using：WPF 与 WinForms 同时启用时，System.Drawing / System.Windows.Forms
// 会让 Brush、Application、UserControl 等类型产生歧义。这里只引入两边都安全的基础命名空间。
global using System;
global using System.Collections.Generic;
global using System.Collections.ObjectModel;
global using System.IO;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
