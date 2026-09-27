# Blender 工业设备贴图管线

本轮制作 **3 个独立设备**：压缩机 `compressor`、双柱分离器 `separator`、高压储罐 `tank`。它们是可摆放的前景建筑，不是预渲染基地或整张关卡背景。几何与材质均由项目内脚本创建，未使用《缺氧》的游戏素材。

## 已交付内容

- `Tools/Blender/build_industrial_assets.py`：可重复执行的建模与渲染脚本。
- `ArtSource/Blender/{compressor,separator,tank}.blend`：可直接打开修改的模型、相机、灯光、材质。
- `Assets/DeepPressure/Art/Industrial/{名称}_Color.png`：无固定方向照明的 sRGB 基础色、透明背景。
- `Assets/DeepPressure/Art/Industrial/{名称}_Normal.png`：面向 Sprite 的切线基法线，线性数据、透明背景。
- `Assets/DeepPressure/Art/Industrial/{名称}_AO.png`：独立的灰度遮蔽数据，白色代表未遮蔽。
- `{名称}_Preview.png`：带区域灯与去噪的材质预览。游戏的实时光照贴图应使用 Color，而不是 Preview。
- `{名称}_Layout.json`：固定画幅、归一化 Sprite pivot、视觉管口坐标。游戏占格与网络端口仍由建筑定义管理。

所有贴图都是 512 × 512 RGBA PNG。当前机器的 Blender 为 `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`，实测版本为 **5.2.1 LTS**。渲染使用 Cycles CPU，固定 4 线程，48 samples；仅 Preview 开启去噪，避免法线通道被颜色去噪误处理。

## 一键重建

在项目目录运行 PowerShell：

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' `
  --background --threads 4 `
  --python 'Tools\Blender\build_industrial_assets.py' -- `
  --asset all --size 512 --samples 48
```

只更新一个设备时，把 `--asset all` 改成 `--asset compressor`、`separator` 或 `tank`。使用 `--passes Normal` 可只重渲染法线；使用 `--output 'ArtSource\Blender\Rendered'` 可指定临时输出目录。本轮因并行沙箱对新建目录的写权限差异，实际先渲染到了此临时目录，再统一复制到 Unity 的 Industrial 目录。

脚本是程序化源文件：修改脚本后重建会覆盖 `.blend` 与同名输出。如果直接在 Blender 内进行了手工建模，应另存手工版本，或把修改同步回脚本。

## 坐标、比例与摆放

- Blender **X 向右、Z 向上、Y 为设备深度**，模型世界原点位于脚底中心。
- 三个资产共用固定正交相机、固定 framing 与 **5.5 Blender 单位**的垂直画幅。因此同一像素长度在三个设备中对应相同物理长度，不会把矮压缩机自动放大为高储罐。
- 相机轻微俯视，让顶面、倒角和管件厚度可见；没有透视缩放。
- 512px 的画幅相当于约 **93.09 px / Blender 单位**。若 Unity 1 单位 = 1 Blender 单位，Sprite 的 PPU 使用此数值。
- 导入 pivot 使用各自 `Layout.json` 的 `unitySpritePivotNormalized`。这是世界原点投影，约为 `(0.5, 0.112)`；不是 PNG 矩形的几何底边。
- 所有端口有 Blender Empty anchor，布局 JSON 记录其图像投影坐标，便于对齐连接管线的视觉端头。
- **视觉端头不决定网络连通性**：占格、输入/输出所在格、朝向、气体类型和容量应放在建筑定义中。更换美术时不应改坏管网拓扑。

## 法线与 AO 如何使用

脚本将几何表面法线从 WORLD 转为相机基，再编码到 `[0,1]`。Cycles 的 shader CAMERA 变换中 Z 朝屏幕内部，因此脚本显式反转 Z，最终约定是 **红 = 向右，绿 = 向上，蓝 = 朝观察者**。输出使用 Raw view transform，避免 sRGB 或 AgX 改写法线数值。第一轮目检发现 Z 方向不符后，已修正并重新实际渲染；大块朝前面应是蓝紫色，而非黄绿色。

Unity 中 Normal 的导入类型应为 **Normal map**，关闭 sRGB 色彩解释，作为 Sprite Secondary Texture `_NormalMap` 绑定。Color 采用 Sprite、透明 alpha、sRGB。AO 为线性灰度纹理；本身不是 URP 2D Sprite-Lit 的内置 PBR AO 槽，需要项目 shader 显式接入，例如命名 `_AOMap` 并温和地乘到 albedo。不可把 AO 任意塞入 `_MaskTex`，因为后者各通道可能已经用于不同的 2D Light blend style。

Light2D 的 Normal Map Quality 需要启用，光源高度/距离需要给出可见的法线响应。验证时在左、右、上方分别移动一盏点光：圆柱、倒角和压力表边缘的明暗应该随灯移动，顶部受光方向应与绿通道一致。Preview 的烘焙照明只供美术检查，不承担此测试。

## 已验证与边界

已用本机 Blender 实际完成 3 套 Color / Normal / AO / Preview 渲染，并检查图像：独立透明轮廓、压力表、散热片、管口、支脚、铜管、视窗和固定螺栓均已生成。法线与 AO 使用相同模型、相机和图像尺寸，能够逐像素配准。素材保持独立，地图仍由编辑器内实际对象摆放来制作。

另对每套 Color / Normal / AO 以 8px 间隔抽样检查：三图 alpha 不匹配数均为 **0**，AO 非灰度样本数均为 **0**，四角透明，尺寸均为 **512 × 512**。不透明法线样本最小蓝通道分别为 145 / 148 / 144，符合可见表面朝前的半球；重建法线平均单位长度误差分别约 0.021 / 0.015 / 0.009（倒角和微小细节的像素平均会缩短法线）。本验证检查的是渲染数据；Unity 动态灯光的最终效果仍应在实际场景中检查。

当前是少量工业设备的管线验证。尚未覆盖角色动画、设备破损状态、完整地形 tileset、全套管道转角/三通图集，也未提供互相遮挡的动态设备拆件。相机法线适合当前固定视角；若后续让设备图像在 3D 中自由旋转，需重新设计视角或采用真实网格。AO 烘焙了设备自身部件之间的遮挡，无法自动反映邻近建筑带来的遮挡。

## v0.8 仓库与运行表现修正

仓库原图 `crate_Color.png` 本身只有一个箱体。画面中右上方的小箱来自 `DeepCatalogBuilder` 自动添加的 `Stacked bin` 子物体，与 AO、法线或 PNG 透明边界无关。现在取消这段生成逻辑；`DeepPresentationUpgrade.ApplyToCatalogAndLoadedScenes` 精确移除旧预制体的该子物体，并把剩余箱体重新按可见底脚对齐。已有场景通过同一升级入口更新，不重建地形。

`DeepPressureArtImporter` 现在在导入 Color、Normal 或 AO 后自动匹配同名前缀的三个文件，不再依赖每次手动点击绑定。Color 显式使用 sRGB，Normal 使用原始 Normal Map 数据、线性空间，AO 使用线性灰度 Default 纹理；数据贴图关闭透明边缘颜色处理。已存在的其他 Secondary Texture（例如专门的灯光遮罩）保留。尺寸不一致时拒绝绑定并报告；工业资产优先读取配套 Layout 的 PPU 和 pivot。Preview 是带烘焙灯光的 Default 预览纹理，不作为运行时 Sprite 使用。

`Sprite-Lit-AO` 提供独立 AO 强度、饱和度和色调参数，AO 仍只进入 `_AOMap`。工业与人物共享材质使用 AO 强度 0.30、饱和度 0.90 和很轻的青绿统一色调，使暖白外壳、铜管和薄荷绿指示灯处于同一环境，保留材料和警报的区别；地形材质使用自己的参数。普通前向渲染路径也应用相同 AO，避免预览与 URP 2D 明暗规则相反。

设备底座不摆动。合成台和研究台按实际 `Working` 订单运行屏幕扫描与风扇，停工、关机、缺电时停下，完工有短暂暖色确认；手工设备无需耗电也具备运行表现。仓库物资变化有指示灯确认，打印舱有扫描线，气体设备有实际压力表指针。藻类制氧器的气泡只在该设备实际输出气体时出现，等待原料、关闭或达到供气上限时停止；运行状态由每台建筑的实际气体交换速率驱动。

本轮对 3 个工业设备和 5 个 Props 原始 PNG 每 8px 抽样：全部为 512×512，三贴图 alpha 不匹配数均为 0、AO 非灰度数均为 0；不透明法线样本最小蓝通道范围 142–195，符合可见表面朝向观察者。程序验收入口 `ValidateMachinePresentation(catalog)` 检查单箱仓库、动态部件绑定、无电力手工工作、阻塞、暂停、完工确认、真实供气与底座固定；Unity 实际执行结果由本轮总验收记录报告。

依据：[Unity 的 Sprite Secondary Textures](https://docs.unity.com/en-us/engine/6000.6/manual/unity2d/2d-urp/2d-index/secondary-textures)、[Blender Normal Map 节点](https://docs.blender.org/manual/en/3.0/render/shader_nodes/vector/normal_map.html)。当前项目使用 URP 14，具体着色器接入以项目安装的 URP 源码为准。
